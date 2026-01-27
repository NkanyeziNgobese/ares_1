# Volve Dataset Health + Usability Report

## What We Have
The repo currently contains three Volve-derived drilling telemetry datasets under `data/volve_logs/`.
The primary dataset for Ares-1 replay and all downstream explanations is
`data/volve_logs/volve_drilling_ares1_ready.csv`. The other datasets are upstream extraction
artifacts used to derive or validate it.

| Dataset | Role | Size | Rows | Cols | Fit Score | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `data/volve_logs/volve_drilling_ares1_ready.csv` | Primary | 95.39 MB | 2,836,277 | 4 | 70 | Main dataset, dense, fixed-tick ready, no TIME |
| `data/volve_logs/volve_drilling_best.csv` | Upstream | 46.23 MB | 3,888,262 | 4 | 5 | Sparse vibration, negative depths, many empty rows |
| `data/volve_logs/volve_drilling_best_wide.csv` | Upstream | 167.56 MB | 3,888,262 | 13 | 15 | More signals (WOB/RPM/TORQUE/SPP/etc.), TIME sparse |

Fit scores come from the rubric in `scripts/analyze_volve_dataset_health.py` and are summarized in
`docs/dataset_quick_stats.json`.

## Dataset Snapshots (Quality + Distribution)
Snapshots focus on the primary Ares-1 ready dataset first, then list upstream sources for
lineage and diagnostics.

### `data/volve_logs/volve_drilling_ares1_ready.csv`
- Columns: `BIT_DEPTH_m`, `ROP_mh`, `VIBRATION_0_5`, `STATUS`
- Missingness: 0% for depth/ROP/vibration
- Depth: 0.0000 to 3465.1919 m (no negative depths)
- ROP (m/h): min 0.0000, max 30.5258, mean 0.9320, std 4.4300
- Vibration (0-5): min 0.0000, max 5.0000, mean 0.7387, std 1.2795
- TIME: not present (fixed tick replay only)
- Duplicate sample rate: 31.35% (sample-based; repeated depths are expected after sorting)

### `data/volve_logs/volve_drilling_best.csv`
- Columns: `BIT_DEPTH`, `TIME`, `ROP`, `VIBRATION`
- Missingness: depth 77.43% non-null, ROP 50.45% non-null, vibration 0.10% non-null, TIME ~0% non-null
- Depth: -90.8097 to 3465.1919 m (negative depths present; 174,507 rows)
- ROP (m/h): min 0.0000, max 47.2851, mean 0.6409, std 4.0026
- Vibration (0-5): min 1.5164, max 5.0000, mean 2.4961, std 0.8109
- Fully empty rows: 22.45%
- Duplicate sample rate: 77.48% (sample-based; high due to empty/constant rows)

### `data/volve_logs/volve_drilling_best_wide.csv`
- Columns include `BIT_DEPTH`, `ROP`, `VIBRATION`, plus WOB/RPM/TORQUE/SPP/FLOW/HKLD
- Missingness: depth 77.43% non-null, ROP 50.45% non-null, vibration 0.10% non-null, TIME ~0% non-null
- Depth: -90.8097 to 3465.1919 m (negative depths present; 174,507 rows)
- ROP (m/h): min 0.0000, max 47.2851, mean 0.6409, std 4.0026
- Vibration (0-5): min 1.5164, max 5.0000, mean 2.4961, std 0.8109
- Fully empty rows: 0.00%
- Duplicate sample rate: 0.11% (sample-based)

## Key Quality Issues (Teaching Notes)
1) TIME is effectively missing in both best and wide datasets.
   - Impact: replay must use fixed tick timing (depth-sorted order).
2) Vibration is extremely sparse in best and wide datasets.
   - Impact: vibration must be proxy-filled before physically meaningful replay.
3) Negative depths exist in best and wide.
   - Impact: remove or transform these rows before Ares-1/Karoo steps.
4) Best dataset has a high fraction of fully empty rows.
   - Impact: drop empty rows before any analytics or replay.
5) Duplicate rates are sample-based (first 200k rows per file).
   - Impact: treat duplicates as a heuristic; depth repeats are expected.

## Is It Usable?
### Ares-1 Telemetry Replay (fixed tick)
- **Yes** for `data/volve_logs/volve_drilling_ares1_ready.csv` (primary dataset).
  - Dense depth/ROP/vibration, status column present, and already cleaned.
- **No (directly)** for `data/volve_logs/volve_drilling_best.csv`.
  - Needs negative depth filtering, vibration proxy fill, and row cleanup first.

### Karoo Transformation (depth scaling + geology overrides)
- **Yes** if using the Ares-1 ready dataset.
  - Depth is clean and suitable for the negative-depth Karoo transform.
  - Ares-1 depth scaling and zone overrides should follow `Ares-1 terrain metrics.xlsx` (repo root).
- **Conditional** if using best/wide.
  - Must remove negative depths and fill vibration before applying geology overrides.

### Physically Meaningful Vibration
- **Measured vs proxy:**
  - Best/wide contain sparse measured vibration values.
  - Ares-1 ready uses proxy-filled vibration for continuity; treat as simulation-grade, not lab-grade.

## Fit Score Rubric (Summary)
Scoring used in `scripts/analyze_volve_dataset_health.py`:
- +30 if depth exists and >90% non-null
- +20 if ROP exists and >70% non-null
- +20 if vibration exists and >70% non-null
- +10 if TIME exists and >50% non-null
- -10 if fully empty rows >10%
- -10 if negative depths detected

Results:
- Ares-1 ready: **70** (strongest fit for replay)
- Best: **5** (not replay-ready without cleaning)
- Best wide: **15** (has proxy signals but still sparse core channels)

## Recommended Path Forward
1) **Use `data/volve_logs/volve_drilling_ares1_ready.csv` as the primary replay input**.
2) Keep `volve_drilling_best_wide.csv` as the proxy-signal source of record.
3) If TIME becomes available, re-derive Ares-1 ready with a real time index.
4) Maintain explicit links to source anchors and assumptions for traceability.
5) Use `Ares-1 terrain metrics.xlsx` (repo root) as the depth/zone source of truth for Karoo scaling.
6) If `docs/vole_source_anchors.md` or `docs/assumptions.md` are missing, create them now.

## References
- Planned flow: `docs/planned_data_flow.md`
- Usage plan: `docs/dataset_usage_plan.md`
- Quick stats: `docs/dataset_quick_stats.json`
- Source anchors (if present): `docs/vole_source_anchors.md`
- Assumptions (if present): `docs/assumptions.md`

## How To Run
1) `python scripts/analyze_volve_dataset_health.py`
2) Open `docs/dataset_health_report.md`
3) Open `docs/planned_data_flow.md`
4) Open `docs/dataset_usage_plan.md`
