# Ares-1

Ares-1 is a local, reproducible dev environment for drilling telemetry streaming, physics
baselines, and ML anomaly placeholders. It publishes synthetic 20 Hz telemetry over MQTT
and logs outputs for review.

## Quickstart

### Windows PowerShell

```powershell
# broker
docker compose -f mqtt/docker-compose.yml up -d

# python
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r python/requirements.txt -r python/requirements-dev.txt
Copy-Item .env.example .env

# terminals (run each in its own terminal)
python python/scripts/subscribe_telemetry.py
python python/scripts/subscribe_events.py
python python/scripts/run_anomaly_detector.py
python python/scripts/publish_telemetry.py
```

### Bash

```bash
# broker
docker compose -f mqtt/docker-compose.yml up -d

# python
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r python/requirements.txt -r python/requirements-dev.txt
cp .env.example .env

# terminals (run each in its own terminal)
python python/scripts/subscribe_telemetry.py
python python/scripts/subscribe_events.py
python python/scripts/run_anomaly_detector.py
python python/scripts/publish_telemetry.py
```

Outputs land in `outputs/`:
- `outputs/telemetry_log.csv`
- `outputs/telemetry_latest.json`
- `outputs/events_log.jsonl`

## Karoo Telemetry Publisher (Excel-driven)

- Metrics source of truth: `Ares-1 terrain metrics.xlsx` (repo root)
- Publisher: `python/ares1/telemetry/ares_karoo_publisher.py`

## Folder Map

- `docs/`: architecture, runbooks, grounding templates
- `agents/`: mission context and run config SoT
- `mqtt/`: local Mosquitto broker
- `python/`: telemetry publisher/subscribers, physics baseline, ML stubs
- `unity/`: Unity bridge stubs
- `outputs/`: logs and reports (git-ignored)

## Next Milestones

- LSTM autoencoder for torque anomalies
- Volve ingestion and feature alignment
- Unity visualization with MQTT playback
- Agentic MCP links to doc sources

## Safety and Constraints

- Grounding documents are required for any real well context.
- Engineering docs are the single source of truth (SoT).
- Keep secrets out of git and use `.env` locally.
