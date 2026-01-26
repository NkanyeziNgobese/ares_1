# Flow Anchor (Volve -> Karoo -> MQTT)

## Context
This anchor is the minimal reference for how data moves, how status is assigned, and how Karoo overrides are applied.

## Step-by-step anchor (ASCII)
```
[1 Raw Volve Spreadsheets]
        |
        v
[2 Aggregation/Extraction -> volve_drilling_best(_wide)]
        |
        v
[3 Ares-1 Ready Cleaning -> volve_drilling_ares1_ready]
        |
        v
[4 Karoo Transform (depth negation + overrides)]
        |
        v
[5 Replay Buffer / DataFrame Streamer]
        |
        v
[6 MQTT Broker]
        |
        v
[7 Unity Ares-1 Subscriber]
```

## Status enum (strict)
- OK
- DOLERITE_SILL
- ECCA_HAZARD

## Precedence rule (must be applied in this order)
1) If depth <= -1400, set status = ECCA_HAZARD.
2) Else if depth in [-1375, -1225], set status = DOLERITE_SILL.
3) Else set status = OK.

## Karoo override metrics (canonical values)
- TD (target depth): -1500
- Dolerite sill zone: [-1375, -1225]
- Ecca hazard zone: depth <= -1400

## Guardrails / Failure Modes
- Depth must be in meters and negated before applying override ranges.
- Status must always be derived; do not allow null or carryover values.
- Fixed-tick replay is required because TIME is missing.

## Closing Summary
Use this file as the fast reference: flow order, status enum, and override numbers are all here.
