# Ares-1 System Overview

## Triple-Tier Ecosystem (Mermaid)

```mermaid
flowchart LR
  subgraph TIER_T[" "]
    direction TB
    TT["<b>Telemetry Tier</b>"]
    Tsub["Synthetic + Field Sensors (20 Hz)"]
  end

  subgraph TIER_A[" "]
    direction TB
    AT["<b>Analytics Tier</b>"]
    Asub["Physics Baseline + Anomaly Logic"]
  end

  subgraph TIER_V[" "]
    direction TB
    VT["<b>Visualization Tier</b>"]
    Vsub["Unity Bridge + Topic Map + UI"]
  end

  TT -- MQTT --> AT
  AT -- MQTT --> VT

  TT --> O1["outputs/telemetry_log.csv"]
  AT --> O2["outputs/events_log.jsonl"]
  VT --> O3["Unity scene playback"]

  classDef tierTitle font-size:20px;
  classDef tierSub font-size:12px;
  classDef out font-size:14px;
  class TT,AT,VT tierTitle;
  class Tsub,Asub,Vsub tierSub;
  class O1,O2,O3 out;
```

## Explanation

- Telemetry Tier: a synthetic publisher (or future rig data) emits JSON telemetry at 20 Hz.
- Analytics Tier: physics baseline + rolling z-score placeholder detects anomalies and
  publishes events to `ares1/events/anomaly`.
- Visualization Tier: Unity stubs consume the same topic map for playback or live views.

## Data Flow

1) Publisher writes to `ares1/telemetry/*`.
2) Subscriber writes `outputs/telemetry_latest.json`.
3) Anomaly detector emits `ares1/events/anomaly`.
4) Event subscriber appends to `outputs/events_log.jsonl`.
