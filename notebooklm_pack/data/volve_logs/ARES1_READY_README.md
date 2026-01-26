# Ares-1 Ready Dataset

## What to use
- data/volve_logs/volve_drilling_ares1_ready.csv

## How it was made
- Derived from Volve drilling logs (wide preferred, best fallback).
- Dropped fully empty rows and negative/NaN depths.
- ROP cleaned via clipping and depth-based interpolation.
- Vibration normalized to [0,5] using measured data or proxy signals.

## Key assumptions
- TIME missing -> fixed tick replay ordered by depth.
- ROP in m/h unless column name indicates m/s conversion.
- Vibration proxy uses robust z-score across available signals.

## Known limitations
- Original TIME data is sparse; no time-ordered replay.
- Proxy quality depends on available WOB/RPM/TORQUE/SPP/flow signals.

## Documentation
- docs/volve_ares1_ready_report.md
- docs/vole_source_anchors.md
- docs/assumptions.md

