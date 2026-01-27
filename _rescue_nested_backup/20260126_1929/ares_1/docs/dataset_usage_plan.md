# Dataset Usage Plan (Ares-1 Telemetry + Karoo Pipeline)

## Which File We Will Use (Decision Tree)
- If `data/volve_logs/volve_drilling_ares1_ready.csv` exists -> **use it**.
- Else -> use `data/volve_logs/volve_drilling_best.csv` and **derive an Ares-1 ready dataset**
  - Remove negative depths
  - Fill ROP and compute vibration proxy
  - Save as `volve_drilling_ares1_ready.csv`

## Column -> Telemetry JSON Mapping
The publisher will emit JSON payloads similar to `python/scripts/ares_karoo_publisher.py`:

| Dataset Column | Telemetry JSON Field | Units | Notes |
| --- | --- | --- | --- |
| `BIT_DEPTH_m` | `depth` | m (negative after Karoo transform) | Depth is negated in Karoo stage to represent subsurface depth |
| `ROP_mh` | `rop` | m/h | Derived from Volve ROP, clipped and interpolated |
| `VIBRATION_0_5` | `vibration` | 0-5 (scaled) | Karoo stage may multiply in dolerite zone |
| `STATUS` | `status` | enum | Set in Karoo stage (see below) |

## Status Enum and Assignment
Status is assigned in the Karoo transform stage:
- `OK`: default for normal shale drilling
- `DOLERITE_SILL`: depth in [-1375, -1225]
- `ECCA_HAZARD`: depth <= -1400

## Missing Data Handling (Explicit Rules)
- **Depth missing** -> drop row (hard requirement).
- **ROP missing** -> interpolate along depth; fallback to rolling median.
- **Vibration missing** -> use proxy from WOB/RPM/TORQUE/SPP/FLOW; fallback to rolling std of ROP.
- **STATUS** -> always derived; never missing in output.

## Determinism & Replay Timing
- TIME is missing -> fixed tick replay only.
- Sort by depth using stable ordering; emit rows sequentially.
- No randomness in the replay order; the publisher defines replay Hz.

## Source Anchors and Assumptions
- Source anchors: `docs/vole_source_anchors.md`
- Assumptions: `docs/assumptions.md`
- Planned flow: `docs/planned_data_flow.md`

## How To Run
1) `python scripts/analyze_volve_dataset_health.py`
2) Open `docs/dataset_health_report.md`
3) Open `docs/planned_data_flow.md`
4) Open `docs/dataset_usage_plan.md`
