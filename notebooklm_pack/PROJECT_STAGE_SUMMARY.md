# Project Stage Summary (Ares-1 Volve -> Karoo -> MQTT)

## Context
This snapshot captures the current state of the Volve-derived pipeline and what is needed to reach a clean, repeatable Karoo-to-MQTT replay.

## Step-by-step flow snapshot
1) Raw Volve spreadsheets are extracted into `volve_drilling_best(_wide)` artifacts.
2) Cleaning/proxy fill yields `volve_drilling_ares1_ready.csv` as the primary replay dataset.
3) Karoo transform negates depth and applies geology overrides for dolerite and Ecca.
4) A replay buffer streams rows at a fixed tick to MQTT.
5) Unity subscribes to MQTT topics for digital twin playback.

## Where we are now
- The Ares-1 ready dataset exists and is the recommended primary input.
- Health and usage docs define the column mapping, status enum, and failure modes.
- Karoo override rules are documented, but no dedicated transform script is checked in.

## What's next
- Implement or formalize the Karoo transform step (script or module) so overrides are reproducible.
- Create missing traceability docs (`docs/vole_source_anchors.md`, `docs/assumptions.md`).
- Validate replay timing behavior against a known tick rate and check MQTT topic schemas.

## Main risks
- TIME missing in source datasets forces fixed-tick replay.
- Vibration is sparse in raw datasets and relies on proxy fill.
- Negative depth handling and override zones can drift if sorting or units change.

## How replay timing works
- The publisher defines a fixed tick (Hz) and emits rows in stable depth order.
- TIME is not used because the Volve datasets do not provide a reliable time index.

## Guardrails / Failure Modes
- If depth is missing, rows must be dropped before any Karoo override logic.
- If vibration proxy rules change, re-run health checks and update quick stats.
- If MQTT topic names diverge from the mission context, subscribers will silently miss data.

## Closing Summary
We are replay-ready with `volve_drilling_ares1_ready.csv`, but the Karoo transform step needs a concrete script and the missing source/assumption docs should be created to lock traceability.
