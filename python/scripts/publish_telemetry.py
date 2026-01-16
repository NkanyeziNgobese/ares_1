"""Publish synthetic telemetry at 20Hz to MQTT with local CSV logging."""

from __future__ import annotations

import csv
import json
import logging
import os
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict, Tuple

import numpy as np
import paho.mqtt.client as mqtt
from dotenv import load_dotenv
from rich.logging import RichHandler

ROOT = Path(__file__).resolve().parents[2]
PYTHON_DIR = ROOT / "python"
if str(PYTHON_DIR) not in sys.path:
    sys.path.insert(0, str(PYTHON_DIR))

from ares1.physics.torque_drag import torque_baseline


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def configure_logging() -> logging.Logger:
    logging.basicConfig(
        level=logging.INFO,
        format="%(message)s",
        datefmt="[%X]",
        handlers=[RichHandler(rich_tracebacks=True)],
    )
    return logging.getLogger("ares1.publish")


def build_payload(value: float, unit: str) -> Dict[str, object]:
    return {
        "timestamp": utc_now_iso(),
        "value": value,
        "unit": unit,
        "source": "synthetic",
    }


def main() -> None:
    load_dotenv()
    logger = configure_logging()

    broker_host = os.getenv("MQTT_BROKER_HOST", "localhost")
    broker_port = int(os.getenv("MQTT_BROKER_PORT", "1883"))
    hz = float(os.getenv("TELEMETRY_HZ", "20"))
    salt_depth_m = float(os.getenv("SALT_DEPTH_M", "2000"))

    period = 1.0 / hz
    rng = np.random.default_rng()

    outputs_dir = ROOT / "outputs"
    outputs_dir.mkdir(parents=True, exist_ok=True)
    csv_path = outputs_dir / "telemetry_log.csv"
    write_header = not csv_path.exists()

    client_id = f"ares1-pub-{int(time.time())}"
    client = mqtt.Client(client_id=client_id)

    logger.info("Connecting to MQTT at %s:%s", broker_host, broker_port)
    client.connect(broker_host, broker_port, keepalive=60)
    client.loop_start()

    depth_m = 0.0
    start_time = time.perf_counter()
    next_tick = start_time
    last_log = time.time()

    with csv_path.open("a", newline="") as csv_file:
        writer = csv.DictWriter(
            csv_file,
            fieldnames=[
                "timestamp",
                "depth_m",
                "hookload_kn",
                "wob_kn",
                "rpm",
                "torque_nm",
                "rop_m_per_hr",
                "in_salt_zone",
            ],
        )
        if write_header:
            writer.writeheader()

        logger.info("Publishing telemetry at %.1f Hz", hz)
        logger.info("Logging CSV to %s", csv_path)

        try:
            while True:
                now = time.perf_counter()
                if now < next_tick:
                    time.sleep(next_tick - now)
                    continue
                next_tick += period

                t = now - start_time

                rop_m_per_hr = max(5.0, 18.0 + 2.5 * np.sin(t / 30.0) + rng.normal(0, 0.4))
                rop_m_per_s = rop_m_per_hr / 3600.0
                depth_m += rop_m_per_s * period

                rpm = 120.0 + 8.0 * np.sin(t / 20.0) + rng.normal(0, 1.0)
                wob_kn = 90.0 + 6.0 * np.sin(t / 15.0) + rng.normal(0, 1.2)
                hookload_kn = 210.0 + wob_kn + (depth_m / 1000.0) * 5.0 + rng.normal(0, 0.8)

                base_mu = 0.35
                salt_mu = 0.55
                mu = salt_mu if depth_m >= salt_depth_m else base_mu
                baseline = torque_baseline(depth_m, mu, r_m=0.1, fn_per_m=3500.0)
                noise_scale = 0.02 if depth_m < salt_depth_m else 0.06
                torque_nm = baseline + rng.normal(0, baseline * noise_scale)

                metrics: Dict[str, Tuple[float, str]] = {
                    "hookload": (float(hookload_kn), "kN"),
                    "wob": (float(wob_kn), "kN"),
                    "rpm": (float(rpm), "rpm"),
                    "torque": (float(torque_nm), "N*m"),
                    "rop": (float(rop_m_per_hr), "m/hr"),
                    "depth": (float(depth_m), "m"),
                }

                for name, (value, unit) in metrics.items():
                    topic = f"ares1/telemetry/{name}"
                    payload = build_payload(value=value, unit=unit)
                    client.publish(topic, json.dumps(payload), qos=0, retain=False)

                writer.writerow(
                    {
                        "timestamp": utc_now_iso(),
                        "depth_m": depth_m,
                        "hookload_kn": hookload_kn,
                        "wob_kn": wob_kn,
                        "rpm": rpm,
                        "torque_nm": torque_nm,
                        "rop_m_per_hr": rop_m_per_hr,
                        "in_salt_zone": depth_m >= salt_depth_m,
                    }
                )

                now_wall = time.time()
                if now_wall - last_log >= 1.0:
                    logger.info(
                        "depth=%.1f m torque=%.1f N*m rop=%.1f m/hr",
                        depth_m,
                        torque_nm,
                        rop_m_per_hr,
                    )
                    last_log = now_wall
        except KeyboardInterrupt:
            logger.info("Stopping telemetry publisher")
        finally:
            client.loop_stop()
            client.disconnect()


if __name__ == "__main__":
    main()
