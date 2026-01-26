"""
=== Context ===
File: scripts/analyze_volve_dataset_health.py
System loop: Volve dataset health assessment and reporting
Owner/Subsystem: data_health

=== Where This Fits ===
Upstream: data/volve_logs/*.csv
Downstream: docs/dataset_quick_stats.json, docs/dataset_health_report.md
Role: compute dataset quality metrics and provide fit-for-purpose scoring guidance.
Primary dataset: data/volve_logs/volve_drilling_ares1_ready.csv
Reference: docs/planned_data_flow.md (pipeline context)

=== Inputs / Outputs ===
Inputs: CSV datasets under data/volve_logs
Outputs: docs/dataset_quick_stats.json
Side effects: console logs with progress/heartbeats

=== Invariants / Assumptions ===
- Datasets are CSV files with headers.
- Large files must be processed in chunks to avoid memory spikes.
- No dataset files are modified.

=== Configuration ===
- Env vars: none
- Paths: data/volve_logs, docs/dataset_quick_stats.json
- Constants: chunk size, duplicate sample size, fit-score thresholds

=== Dependencies ===
- Internal: none
- External: pandas

=== Main Flow Narration ===
1) Discover Volve-derived datasets, starting with the primary Ares-1 ready file.
2) Stream each dataset in chunks to compute missingness and numeric stats.
3) Flag unit anomalies and compute a fit score for Ares-1 replay.
4) Write a machine-readable JSON summary for documentation.

=== Guardrails & Failure Modes ===
- Missing datasets are logged and skipped.
- Extremely large files are still processed in chunks, with warnings.
- Duplicate detection uses sampling for huge datasets.

=== Determinism & Reproducibility ===
- Sampling is deterministic (first N rows) for duplicate checks.
- Chunk processing is deterministic given file order.

=== How to Test This File ===
1) python scripts/analyze_volve_dataset_health.py
2) Verify docs/dataset_quick_stats.json exists and includes all datasets found.

=== End-of-File Summary ===
Success looks like: dataset_quick_stats.json contains per-file metrics with fit scores and flags.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
import json
import math
import re
from pathlib import Path
from typing import Dict, Iterable, List, Optional, Tuple

import pandas as pd

REPO_ROOT = Path(__file__).resolve().parents[1]

CHUNK_SIZE = 200_000
DUPLICATE_SAMPLE_ROWS = 200_000
LARGE_FILE_BYTES = 500 * 1024 * 1024
PRIMARY_DATASET_NAME = "volve_drilling_ares1_ready.csv"


@dataclass
class RunningStat:
    count: int = 0
    mean: float = 0.0
    m2: float = 0.0
    min_value: Optional[float] = None
    max_value: Optional[float] = None

    def update(self, series: pd.Series) -> None:
        """
        Name: update
        Why it exists: incrementally update numeric stats for a streaming series.
        Args:
          - series: pd.Series
        Returns:
          - None
        Raises:
          - None
        Assumptions:
          - series can be coerced to numeric values
        Edge cases:
          - empty or all-null series (no update applied)
        Example I/O:
          - Input: pd.Series([1, 2, 3])
          - Output: count/mean/min/max updated in place
        """
        values = pd.to_numeric(series, errors="coerce").dropna()
        if values.empty:
            return

        chunk_count = int(values.count())
        chunk_mean = float(values.mean())
        chunk_var = float(values.var(ddof=0))
        chunk_m2 = chunk_var * chunk_count

        if self.count == 0:
            self.count = chunk_count
            self.mean = chunk_mean
            self.m2 = chunk_m2
        else:
            delta = chunk_mean - self.mean
            total = self.count + chunk_count
            self.mean = self.mean + delta * chunk_count / total
            self.m2 = self.m2 + chunk_m2 + delta * delta * self.count * chunk_count / total
            self.count = total

        chunk_min = float(values.min())
        chunk_max = float(values.max())
        if self.min_value is None or chunk_min < self.min_value:
            self.min_value = chunk_min
        if self.max_value is None or chunk_max > self.max_value:
            self.max_value = chunk_max

    def std(self) -> float:
        """
        Name: std
        Why it exists: compute standard deviation from accumulated statistics.
        Args:
          - None
        Returns:
          - float
        Raises:
          - None
        Assumptions:
          - count reflects total numeric observations
        Edge cases:
          - count <= 1 returns 0.0
        Example I/O:
          - Input: none
          - Output: 4.2
        """
        if self.count <= 1:
            return 0.0
        return math.sqrt(self.m2 / self.count)


def utc_now() -> datetime:
    """
    Name: utc_now
    Why it exists: provide a timezone-aware UTC timestamp for logs.
    Args:
      - None
    Returns:
      - datetime
    Raises:
      - None
    Assumptions:
      - system clock is accurate
    Edge cases:
      - system time skew affects timestamps
    Example I/O:
      - Input: none
      - Output: datetime(..., tzinfo=UTC)
    """
    return datetime.now(timezone.utc)


def log(message: str) -> None:
    """
    Name: log
    Why it exists: emit timestamped log messages for long-running analysis.
    Args:
      - message: str
    Returns:
      - None
    Raises:
      - None
    Assumptions:
      - message is safe to print
    Edge cases:
      - non-string inputs are stringified by f-string
    Example I/O:
      - Input: "Analyzing file"
      - Output: "2026-01-24T18:00:00Z | Analyzing file"
    """
    timestamp = utc_now().isoformat().replace("+00:00", "Z")
    print(f"{timestamp} | {message}")


def normalize_name(value: str) -> str:
    """
    Name: normalize_name
    Why it exists: normalize column names for fuzzy matching.
    Args:
      - value: str
    Returns:
      - str
    Raises:
      - None
    Assumptions:
      - value is a column name
    Edge cases:
      - None/NaN values are stringified before normalization
    Example I/O:
      - Input: "Bit Measured Depth m"
      - Output: "bit measured depth m"
    """
    return re.sub(r"[^a-z0-9]+", " ", str(value).lower()).strip()


def find_column(columns: Iterable[str], candidates: List[str]) -> Optional[str]:
    """
    Name: find_column
    Why it exists: choose a column name based on normalized candidate hints.
    Args:
      - columns: Iterable[str]
      - candidates: List[str]
    Returns:
      - Optional[str]
    Raises:
      - None
    Assumptions:
      - candidates reflect preferred column names
    Edge cases:
      - no candidate matches returns None
    Example I/O:
      - Input: ["BIT_DEPTH", "ROP"], ["BIT_DEPTH", "DEPTH"]
      - Output: "BIT_DEPTH"
    """
    normalized = {normalize_name(col): col for col in columns}
    for candidate in candidates:
        key = normalize_name(candidate)
        if key in normalized:
            return normalized[key]
    for candidate in candidates:
        key = normalize_name(candidate)
        for norm, original in normalized.items():
            if key and key in norm:
                return original
    return None


def detect_standard_columns(columns: List[str]) -> Dict[str, Optional[str]]:
    """
    Name: detect_standard_columns
    Why it exists: locate depth, ROP, vibration, and time columns.
    Args:
      - columns: List[str]
    Returns:
      - Dict[str, Optional[str]]
    Raises:
      - None
    Assumptions:
      - column names are descriptive
    Edge cases:
      - missing columns return None for that key
    Example I/O:
      - Input: ["BIT_DEPTH", "ROP", "VIBRATION_0_5"]
      - Output: {"depth": "BIT_DEPTH", "rop": "ROP", "vibration": "VIBRATION_0_5"}
    """
    depth = find_column(
        columns,
        ["BIT_DEPTH", "BIT_DEPTH_M", "BIT MEASURED DEPTH", "DEPTH", "MD", "HOLE DEPTH"],
    )
    rop = find_column(
        columns,
        ["ROP", "ROP_MH", "ROP_M/H", "RATE OF PENETRATION", "TIME AVERAGED ROP"],
    )
    vibration = find_column(
        columns,
        ["VIBRATION_0_5", "VIBRATION", "VIBRATION_RAW", "LATERAL VIBRATION", "VIBRATION_PROXY"],
    )
    time_col = find_column(columns, ["TIME", "TIMESTAMP", "DATETIME", "DATE TIME"])

    return {"depth": depth, "rop": rop, "vibration": vibration, "time": time_col}


def expect_vibration_range(column_name: Optional[str]) -> Optional[Tuple[float, float]]:
    """
    Name: expect_vibration_range
    Why it exists: infer expected vibration range based on column naming.
    Args:
      - column_name: Optional[str]
    Returns:
      - Optional[Tuple[float, float]]
    Raises:
      - None
    Assumptions:
      - normalized vibration columns imply 0-5 range
    Edge cases:
      - raw/proxy vibration returns None (no enforced range)
    Example I/O:
      - Input: "VIBRATION_0_5"
      - Output: (0.0, 5.0)
    """
    if not column_name:
        return None
    normalized = normalize_name(column_name)
    if "vibration 0 5" in normalized or ("vibration" in normalized and "raw" not in normalized):
        return (0.0, 5.0)
    return None


def discover_datasets() -> List[Path]:
    """
    Name: discover_datasets
    Why it exists: enumerate Volve-derived datasets in priority order.
    Args:
      - None
    Returns:
      - List[Path]
    Raises:
      - None
    Assumptions:
      - datasets reside under data/volve_logs
    Edge cases:
      - missing datasets are logged and skipped
    Example I/O:
      - Input: none
      - Output: [Path("data/volve_logs/volve_drilling_ares1_ready.csv"), ...]
    """
    candidates = [
        PRIMARY_DATASET_NAME,
        "volve_drilling_best.csv",
        "volve_drilling_best_wide.csv",
    ]
    base = REPO_ROOT / "data" / "volve_logs"
    paths = []
    for name in candidates:
        path = base / name
        if path.exists():
            paths.append(path)
        else:
            log(f"Dataset not found (skipping): {path.as_posix()}")
    return paths


def analyze_dataset(path: Path) -> Dict[str, object]:
    """
    Name: analyze_dataset
    Why it exists: compute health metrics and fit score for a dataset.
    Args:
      - path: Path
    Returns:
      - Dict[str, object]
    Raises:
      - None
    Assumptions:
      - CSV has headers and can be streamed in chunks
    Edge cases:
      - empty datasets produce zero rows and empty stats
    Example I/O:
      - Input: Path("data/volve_logs/volve_drilling_best.csv")
      - Output: metrics dictionary with missingness and stats
    """
    file_size = path.stat().st_size
    if file_size > LARGE_FILE_BYTES:
        log(f"Large file detected ({file_size} bytes). Processing in chunks.")

    columns = pd.read_csv(path, nrows=0).columns.tolist()
    standard_cols = detect_standard_columns(columns)

    missing_counts = {col: 0 for col in columns}
    numeric_stats = {col: RunningStat() for col in columns}

    row_count = 0
    fully_empty_rows = 0
    depth_negative = 0
    vibration_out_of_range = 0

    sample_frames = []
    sample_remaining = DUPLICATE_SAMPLE_ROWS

    vibration_range = expect_vibration_range(standard_cols.get("vibration"))

    for index, chunk in enumerate(pd.read_csv(path, chunksize=CHUNK_SIZE, low_memory=False)):
        row_count += len(chunk)
        fully_empty_rows += int(chunk.isna().all(axis=1).sum())

        for col in columns:
            missing_counts[col] += int(chunk[col].isna().sum())
            numeric_stats[col].update(chunk[col])

        depth_col = standard_cols.get("depth")
        if depth_col and depth_col in chunk:
            depth_series = pd.to_numeric(chunk[depth_col], errors="coerce")
            depth_negative += int((depth_series < 0).sum())

        vibration_col = standard_cols.get("vibration")
        if vibration_col and vibration_col in chunk and vibration_range:
            vib_series = pd.to_numeric(chunk[vibration_col], errors="coerce")
            lower, upper = vibration_range
            vibration_out_of_range += int(((vib_series < lower) | (vib_series > upper)).sum())

        if sample_remaining > 0:
            take = min(sample_remaining, len(chunk))
            sample_frames.append(chunk.iloc[:take])
            sample_remaining -= take

        if (index + 1) % 10 == 0:
            log(f"Processed {row_count} rows from {path.name}")

    missingness = {}
    for col in columns:
        missing = missing_counts[col]
        non_null = row_count - missing
        rate = missing / row_count if row_count else 0.0
        missingness[col] = {
            "missing": missing,
            "missing_rate": rate,
            "non_null": non_null,
            "non_null_rate": non_null / row_count if row_count else 0.0,
        }

    numeric_summary = {}
    for col, stat in numeric_stats.items():
        if stat.count == 0:
            continue
        numeric_summary[col] = {
            "count": stat.count,
            "min": stat.min_value,
            "max": stat.max_value,
            "mean": stat.mean,
            "std": stat.std(),
        }

    duplicate_sample = {
        "method": "sampled",
        "sample_rows": 0,
        "duplicate_rows": 0,
        "duplicate_rate": 0.0,
    }
    if sample_frames:
        sample_df = pd.concat(sample_frames, ignore_index=True)
        duplicate_rows = int(sample_df.duplicated().sum())
        duplicate_sample = {
            "method": "first_rows",
            "sample_rows": len(sample_df),
            "duplicate_rows": duplicate_rows,
            "duplicate_rate": duplicate_rows / len(sample_df) if len(sample_df) else 0.0,
        }

    depth_col = standard_cols.get("depth")
    rop_col = standard_cols.get("rop")
    vibration_col = standard_cols.get("vibration")
    time_col = standard_cols.get("time")

    depth_stats = numeric_summary.get(depth_col, {}) if depth_col else {}
    rop_stats = numeric_summary.get(rop_col, {}) if rop_col else {}
    vibration_stats = numeric_summary.get(vibration_col, {}) if vibration_col else {}

    unit_flags = []
    if depth_negative > 0:
        unit_flags.append("Negative depth values detected")
    if rop_stats:
        rop_max = rop_stats.get("max", 0)
        rop_mean = rop_stats.get("mean", 0)
        if rop_max and rop_max > 300:
            unit_flags.append("ROP max exceeds 300 m/h (unit check)")
        if rop_mean is not None and rop_mean < 0.01:
            unit_flags.append("ROP mean is extremely low (possible unit mismatch)")
    if vibration_out_of_range > 0:
        unit_flags.append("Vibration values outside expected 0-5 range")

    fully_empty_pct = fully_empty_rows / row_count if row_count else 0.0

    depth_rate = missingness.get(depth_col, {}).get("non_null_rate", 0.0) if depth_col else 0.0
    rop_rate = missingness.get(rop_col, {}).get("non_null_rate", 0.0) if rop_col else 0.0
    vib_rate = missingness.get(vibration_col, {}).get("non_null_rate", 0.0) if vibration_col else 0.0
    time_rate = missingness.get(time_col, {}).get("non_null_rate", 0.0) if time_col else 0.0

    fit_score = 0
    score_notes = []
    if depth_col and depth_rate > 0.9:
        fit_score += 30
        score_notes.append("Depth coverage >90% (+30)")
    elif depth_col and depth_rate > 0.7:
        fit_score += 15
        score_notes.append("Depth coverage >70% (+15)")

    if rop_col and rop_rate > 0.7:
        fit_score += 20
        score_notes.append("ROP coverage >70% (+20)")
    elif rop_col and rop_rate > 0.4:
        fit_score += 10
        score_notes.append("ROP coverage >40% (+10)")

    if vibration_col and vib_rate > 0.7:
        fit_score += 20
        score_notes.append("Vibration coverage >70% (+20)")
    elif vibration_col and vib_rate > 0.4:
        fit_score += 10
        score_notes.append("Vibration coverage >40% (+10)")

    if time_col and time_rate > 0.5:
        fit_score += 10
        score_notes.append("TIME coverage >50% (+10)")

    if fully_empty_pct > 0.1:
        fit_score -= 10
        score_notes.append("Fully empty rows >10% (-10)")

    if depth_negative > 0:
        fit_score -= 10
        score_notes.append("Negative depth values (-10)")

    fit_score = max(0, min(100, fit_score))

    return {
        "path": path.as_posix().replace(str(REPO_ROOT.as_posix()), "").lstrip("/"),
        "size_bytes": file_size,
        "rows": row_count,
        "cols": len(columns),
        "columns": columns,
        "missingness": missingness,
        "numeric_stats": numeric_summary,
        "fully_empty_rows": fully_empty_rows,
        "fully_empty_row_pct": fully_empty_pct,
        "duplicate_sample": duplicate_sample,
        "standard_columns": standard_cols,
        "depth": {
            "column": depth_col,
            "min": depth_stats.get("min"),
            "max": depth_stats.get("max"),
            "negative_count": depth_negative,
        },
        "rop": {
            "column": rop_col,
            "min": rop_stats.get("min"),
            "max": rop_stats.get("max"),
            "mean": rop_stats.get("mean"),
            "std": rop_stats.get("std"),
        },
        "vibration": {
            "column": vibration_col,
            "min": vibration_stats.get("min"),
            "max": vibration_stats.get("max"),
            "mean": vibration_stats.get("mean"),
            "std": vibration_stats.get("std"),
            "expected_range": vibration_range,
            "out_of_range_count": vibration_out_of_range,
        },
        "time": {
            "column": time_col,
            "non_null_rate": time_rate,
        },
        "unit_flags": unit_flags,
        "fit_score": fit_score,
        "fit_score_notes": score_notes,
    }


def main() -> None:
    """
    Name: main
    Why it exists: entry point that generates dataset_quick_stats.json.
    Args:
      - None
    Returns:
      - None
    Raises:
      - Exception: if analysis fails
    Assumptions:
      - data/volve_logs contains CSV datasets
    Edge cases:
      - no datasets found exits early without writing JSON
    Example I/O:
      - Input: none
      - Output: docs/dataset_quick_stats.json
    """
    try:
        log("Starting Volve dataset health analysis.")
        log(
            "Config: chunk_size={chunk}, duplicate_sample_rows={sample}, large_file_bytes={large}".format(
                chunk=CHUNK_SIZE,
                sample=DUPLICATE_SAMPLE_ROWS,
                large=LARGE_FILE_BYTES,
            )
        )
        datasets = discover_datasets()
        primary_path = REPO_ROOT / "data" / "volve_logs" / PRIMARY_DATASET_NAME
        if not primary_path.exists():
            log(
                "Primary dataset missing: data/volve_logs/{name}. "
                "Analysis will proceed with upstream artifacts only.".format(
                    name=PRIMARY_DATASET_NAME
                )
            )
        if not datasets:
            log("No datasets found under data/volve_logs.")
            return

        results = []
        for path in datasets:
            role = "primary" if path.name == PRIMARY_DATASET_NAME else "upstream"
            log(f"Analyzing {path.name} ({role} dataset)")
            results.append(analyze_dataset(path))

        output = {
            "generated_at": utc_now().isoformat().replace("+00:00", "Z"),
            "datasets": results,
        }

        output_path = REPO_ROOT / "docs" / "dataset_quick_stats.json"
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(output, indent=2), encoding="utf-8")
        log(f"Wrote dataset quick stats: {output_path.as_posix()}")
    except Exception as exc:
        log(f"ERROR: {type(exc).__name__}: {exc}")
        raise


if __name__ == "__main__":
    main()

"""
=== Closing Summary ===
Success looks like: docs/dataset_quick_stats.json contains complete metrics for each dataset.
Outputs: docs/dataset_quick_stats.json
"""
