# Ares-1 Python

This folder contains the core Python code for telemetry, physics baselines, and ML stubs.

## Windows PowerShell

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r python/requirements.txt -r python/requirements-dev.txt
```

## Bash

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r python/requirements.txt -r python/requirements-dev.txt
```

## Run (example)

```bash
python python/scripts/publish_telemetry.py
```

Copy `.env.example` to `.env` and adjust broker settings as needed.
