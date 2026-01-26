# Mission Context (Current Run)

## Run Identity
- Run name: Ares-1 Dev Telemetry
- Owner:
- Date:
- Notes:

## Broker
- URL: mqtt://localhost:1883
- WebSockets (optional): ws://localhost:9001

## Topic Map
- Telemetry:
  - ares1/telemetry/hookload
  - ares1/telemetry/wob
  - ares1/telemetry/rpm
  - ares1/telemetry/torque
  - ares1/telemetry/rop
  - ares1/telemetry/depth
- Events:
  - ares1/events/anomaly

## Anomaly Detection (Placeholder)
- Method: rolling z-score of torque residual vs physics baseline
- z-threshold: 3.0
- window size: 60 samples
- min samples: 30

## Salt Zone (Synthetic)
- Salt depth threshold (m): 2000
- Behavior: increased torque mean and noise above threshold

## Run Checklist
- [ ] `docker compose` broker up
- [ ] `.env` configured (see `.env.example`)
- [ ] Telemetry publisher running
- [ ] Telemetry subscriber running
- [ ] Anomaly detector running
- [ ] Event subscriber running
- [ ] Outputs verified under `outputs/`

## Change Log
- YYYY-MM-DD: Initial context created
