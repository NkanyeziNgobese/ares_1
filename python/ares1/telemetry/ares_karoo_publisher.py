"""
Ares-1 Karoo Telemetry Publisher (MQTT, 1 Hz)

Dependencies:
  - pandas
  - paho-mqtt
  - openpyxl (for Excel metrics)

How to test:
  1) Start a local broker (mosquitto).
  2) Run: mosquitto_sub -t ares1/telemetry/main -v
  3) Run: python python/scripts/ares_karoo_publisher.py --csv path\\to\\volve.csv

Next steps:
  - Validate column mappings for your Volve export (depth/ROP/vibration/torque).
  - Add schema config for alternate datasets.
  - Calibrate torque synthesis with field data if torque column is missing.
"""

from __future__ import annotations

import argparse
import json
import os
import random
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple

import pandas as pd
import paho.mqtt.client as mqtt


DEFAULTS = {
    "origin_depth": -2.9999,
    "td_depth": -3500.0,
    "zones": {
        "GEO_Beaufort": (0.0, -800.0),
        "GEO_Ecca_Shale_Upper": (-800.0, -1200.0),
        "GEO_Dolerite_Sill": (-1200.0, -1400.0),
        "GEO_Ecca_Shale_Lower": (-1400.0, -2500.0),
        "GEO_Dwyka": (-2500.0, -3500.0),
    },
}

STATUS_MAP = {
    "GEO_Beaufort": "OVERBURDEN",
    "GEO_Ecca_Shale_Upper": "UPPER GAS TARGET",
    "GEO_Dolerite_Sill": "HAZARD: DOLERITE SILL",
    "GEO_Ecca_Shale_Lower": "LOWER GAS TARGET (HIGH PRESSURE)",
    "GEO_Dwyka": "BASIN TERMINUS",
}


@dataclass
class Metrics:
    origin_depth: float
    td_depth: float
    zones: Dict[str, Tuple[float, float]]
    source: str


def find_repo_root(start: Path) -> Path:
    for candidate in [start] + list(start.parents):
        if (candidate / ".git").exists():
            return candidate
    return start


def find_metrics_xlsx(root: Path) -> Optional[Path]:
    target = "Ares-1 terrain metrics.xlsx"
    direct = root / target
    if direct.exists():
        return direct
    skip = {".git", ".venv", ".pytest_cache", "__pycache__", "Library", "Temp", "Logs", "UserSettings"}
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in skip]
        if target in filenames:
            return Path(dirpath) / target
    return None


def _pick_column(df: pd.DataFrame, name: str) -> Optional[str]:
    for col in df.columns:
        if str(col).strip().lower() == name.lower():
            return col
    return None


def load_metrics_from_excel(path: Path) -> Optional[Metrics]:
    try:
        df = pd.read_excel(path, sheet_name="Well_Structure_Depths")
    except Exception:
        return None

    asset_col = _pick_column(df, "Asset_Name")
    top_col = _pick_column(df, "Top_Z (m)")
    bottom_col = _pick_column(df, "Bottom_Z (m)")
    if not asset_col or not top_col or not bottom_col:
        return None

    def get_zone_bounds(asset_name: str) -> Optional[Tuple[float, float]]:
        rows = df[df[asset_col] == asset_name]
        if rows.empty:
            return None
        row = rows.iloc[0]
        return float(row[top_col]), float(row[bottom_col])

    zones: Dict[str, Tuple[float, float]] = {}
    for key in DEFAULTS["zones"].keys():
        bounds = get_zone_bounds(key)
        if bounds:
            zones[key] = bounds

    wellbore = get_zone_bounds("WEL_Wellbore_Main")
    origin_depth = DEFAULTS["origin_depth"]
    td_depth = DEFAULTS["td_depth"]
    if wellbore:
        origin_depth = float(wellbore[0])
        td_depth = float(wellbore[1])

    if zones:
        return Metrics(origin_depth=origin_depth, td_depth=td_depth, zones=zones, source=str(path))
    return None


def resolve_column_name(columns: List[str], override: Optional[str], candidates: List[str]) -> Optional[str]:
    if override:
        if override in columns:
            return override
        raise ValueError(f"Column override not found: {override}")

    normalized = {normalize_name(c): c for c in columns}
    for cand in candidates:
        key = normalize_name(cand)
        if key in normalized:
            return normalized[key]

    for col in columns:
        norm = normalize_name(col)
        for cand in candidates:
            if normalize_name(cand) in norm:
                return col
    return None


def normalize_name(name: str) -> str:
    return "".join(ch.lower() for ch in str(name) if ch.isalnum())


def compute_min_max(csv_path: Path, depth_col: str, chunksize: int, sep: str) -> Tuple[float, float]:
    min_val = None
    max_val = None
    for chunk in pd.read_csv(csv_path, usecols=[depth_col], chunksize=chunksize, sep=sep, low_memory=False):
        series = pd.to_numeric(chunk[depth_col], errors="coerce").dropna()
        if series.empty:
            continue
        cmin = float(series.min())
        cmax = float(series.max())
        min_val = cmin if min_val is None else min(min_val, cmin)
        max_val = cmax if max_val is None else max(max_val, cmax)
    if min_val is None or max_val is None:
        raise ValueError("Depth column has no numeric values.")
    return min_val, max_val


def map_depth(value: float, src_min: float, src_max: float, dst_top: float, dst_bottom: float) -> float:
    if src_max == src_min:
        return dst_top
    ratio = (value - src_min) / (src_max - src_min)
    return dst_top + ratio * (dst_bottom - dst_top)


def clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


def synthesize_torque(rop: float, vibration: float, rng: random.Random) -> float:
    noise = rng.uniform(-2.0, 2.0)
    torque = (rop * 6.0) + (vibration * 1.8) + noise
    return max(0.0, torque)


def find_zone(depth: float, zones: Dict[str, Tuple[float, float]]) -> Optional[str]:
    for name, (top, bottom) in zones.items():
        low = min(top, bottom)
        high = max(top, bottom)
        if low <= depth <= high:
            return name
    return None


def apply_zone_logic(
    depth: float,
    rop: float,
    vibration: float,
    torque: float,
    zones: Dict[str, Tuple[float, float]],
    rng: random.Random,
) -> Tuple[float, float, float, str]:
    zone = find_zone(depth, zones)
    if not zone:
        return rop, vibration, torque, "DRILLING"

    status = STATUS_MAP.get(zone, zone)

    if zone == "GEO_Ecca_Shale_Upper":
        torque *= 1.0 + rng.uniform(-0.05, 0.05)
    elif zone == "GEO_Dolerite_Sill":
        rop *= 0.25
        vibration = clamp(vibration, 75.0, 98.0)
        torque *= 1.40
    elif zone == "GEO_Ecca_Shale_Lower":
        rop *= 0.85
        status = f"{status} | maturity: increasing"

    status = f"{status} | {zone}"
    return rop, vibration, torque, status


def make_mqtt_client(client_id: str) -> mqtt.Client:
    if hasattr(mqtt, "CallbackAPIVersion"):
        return mqtt.Client(callback_api_version=mqtt.CallbackAPIVersion.VERSION2, client_id=client_id)
    return mqtt.Client(client_id=client_id)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Ares-1 Karoo MQTT telemetry publisher (1 Hz).")
    parser.add_argument("--csv", required=True, help="Path to Volve CSV data.")
    parser.add_argument("--host", default="127.0.0.1", help="MQTT broker host.")
    parser.add_argument("--port", type=int, default=1883, help="MQTT broker port.")
    parser.add_argument("--topic", default="ares1/telemetry/main", help="MQTT topic.")
    parser.add_argument("--hz", type=float, default=1.0, help="Publish frequency in Hz.")
    parser.add_argument("--seed", type=int, default=None, help="Random seed for deterministic noise.")
    parser.add_argument("--chunksize", type=int, default=5000, help="CSV chunksize for streaming.")
    parser.add_argument("--sep", default=",", help="CSV delimiter.")
    parser.add_argument("--depth-col", default=None, help="Depth column override.")
    parser.add_argument("--rop-col", default=None, help="ROP column override.")
    parser.add_argument("--vibration-col", default=None, help="Vibration column override.")
    parser.add_argument("--torque-col", default=None, help="Torque column override.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    csv_path = Path(args.csv)
    if not csv_path.exists():
        print(f"CSV not found: {csv_path}", file=sys.stderr)
        return 1

    if args.hz <= 0:
        print("--hz must be > 0", file=sys.stderr)
        return 1

    repo_root = find_repo_root(Path(__file__).resolve())
    metrics_path = find_metrics_xlsx(repo_root)
    metrics = None
    if metrics_path:
        metrics = load_metrics_from_excel(metrics_path)

    if metrics:
        origin_depth = metrics.origin_depth
        td_depth = metrics.td_depth
        zones = metrics.zones
        print(f"Metrics source: {metrics.source}")
    else:
        origin_depth = DEFAULTS["origin_depth"]
        td_depth = DEFAULTS["td_depth"]
        zones = DEFAULTS["zones"]
        print("Metrics source: defaults (Excel not found or unreadable)")

    header = pd.read_csv(csv_path, nrows=0, sep=args.sep)
    columns = list(header.columns)

    depth_col = resolve_column_name(columns, args.depth_col, ["BIT_DEPTH", "BITDEPTH", "DEPTH"])
    if not depth_col:
        print("Depth column not found. Provide --depth-col.", file=sys.stderr)
        return 1

    rop_col = resolve_column_name(columns, args.rop_col, ["ROP", "RATE_OF_PENETRATION"])
    vib_col = resolve_column_name(columns, args.vibration_col, ["VIBRATION", "VIB"])
    torque_col = resolve_column_name(columns, args.torque_col, ["TORQUE"])

    src_min, src_max = compute_min_max(csv_path, depth_col, args.chunksize, args.sep)
    print(f"Depth scaling: {depth_col} min={src_min} max={src_max} -> [{origin_depth}, {td_depth}]")

    rng = random.Random(args.seed)
    client_id = f"ares1-karoo-{os.getpid()}"
    client = make_mqtt_client(client_id)
    client.connect(args.host, args.port)
    client.loop_start()

    usecols = [depth_col]
    if rop_col:
        usecols.append(rop_col)
    if vib_col:
        usecols.append(vib_col)
    if torque_col:
        usecols.append(torque_col)

    idx_depth = usecols.index(depth_col)
    idx_rop = usecols.index(rop_col) if rop_col else None
    idx_vib = usecols.index(vib_col) if vib_col else None
    idx_torque = usecols.index(torque_col) if torque_col else None

    period = 1.0 / args.hz
    next_tick = time.monotonic()

    try:
        for chunk in pd.read_csv(csv_path, usecols=usecols, chunksize=args.chunksize, sep=args.sep, low_memory=False):
            for row in chunk.itertuples(index=False, name=None):
                raw_depth = row[idx_depth]
                if pd.isna(raw_depth):
                    continue

                depth_val = float(raw_depth)
                mapped_depth = map_depth(depth_val, src_min, src_max, origin_depth, td_depth)
                mapped_depth = round(mapped_depth, 4)

                rop = float(row[idx_rop]) if idx_rop is not None and not pd.isna(row[idx_rop]) else 0.0
                vibration = (
                    float(row[idx_vib]) if idx_vib is not None and not pd.isna(row[idx_vib]) else 0.0
                )

                if idx_torque is not None and not pd.isna(row[idx_torque]):
                    torque = float(row[idx_torque])
                else:
                    torque = synthesize_torque(rop, vibration, rng)

                rop, vibration, torque, status = apply_zone_logic(
                    mapped_depth, rop, vibration, torque, zones, rng
                )

                payload = {
                    "depth": mapped_depth,
                    "rop": float(rop),
                    "vibration": float(vibration),
                    "torque": float(torque),
                    "status": status,
                }

                client.publish(args.topic, json.dumps(payload))

                next_tick += period
                sleep_for = next_tick - time.monotonic()
                if sleep_for > 0:
                    time.sleep(sleep_for)

    except KeyboardInterrupt:
        pass
    finally:
        client.loop_stop()
        client.disconnect()

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
