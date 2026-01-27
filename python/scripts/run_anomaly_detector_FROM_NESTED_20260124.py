"""Run the anomaly detector with repo-local imports."""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PYTHON_DIR = ROOT / "python"
if str(PYTHON_DIR) not in sys.path:
    sys.path.insert(0, str(PYTHON_DIR))

from ares1.ml.anomaly import run_mqtt


def main() -> None:
    run_mqtt()


if __name__ == "__main__":
    main()
