# NotebookLM Upload Checklist (Ares-1 Volve -> Karoo -> MQTT)

## Context
This pack is a focused set of files that explain the Volve -> Karoo -> MQTT pipeline without shipping raw datasets. Use it as a narrated walkthrough for the explainer video.

## Step-by-step file list (what to read and why)
- `README.md` — Why this file matters: project purpose, MQTT workflow, and repo map.
- `docs/planned_data_flow.md` — Why this file matters: canonical pipeline stages, assumptions, and failure modes.
- `docs/dataset_health_report.md` — Why this file matters: data readiness, missing TIME, and vibration sparsity.
- `docs/dataset_usage_plan.md` — Why this file matters: dataset selection logic, column-to-telemetry mapping, status enum.
- `docs/dataset_quick_stats.json` — Why this file matters: numeric summary behind the health report.
- `data/volve_logs/ARES1_READY_README.md` — Why this file matters: how the Ares-1 ready dataset was derived and its limits.
- `scripts/analyze_volve_dataset_health.py` — Why this file matters: scoring rubric and dataset quality checks.
- `python/scripts/ares_karoo_publisher.py` — Why this file matters: example MQTT publisher that demonstrates Karoo-style telemetry.
- `docs/architecture/system_overview.md` — Why this file matters: high-level tiered system overview and MQTT flow.
- `agents/context/mission_context.md` — Why this file matters: topic map and run context for replay.
- `notebooklm_pack/PROJECT_STAGE_SUMMARY.md` — Why this file matters: condensed "where we are" and "what's next" snapshot.
- `notebooklm_pack/FLOW_ANCHOR.md` — Why this file matters: ASCII pipeline anchor, status enum, override metrics.
- `notebooklm_pack/MANIFEST.json` — Why this file matters: index of pack contents with sizes and timestamps.
- `notebooklm_pack/UPLOAD_CHECKLIST.md` — Why this file matters: single checklist for upload readiness.

## Guardrails / Failure Modes
- Source anchor and assumption docs are referenced in multiple files but are not present in this repo (`docs/vole_source_anchors.md`, `docs/assumptions.md`, `docs/volve_ares1_ready_report.md`).
- No dedicated Karoo transform script is present; the transform is described in docs and implied by the publisher.
- Raw CSV datasets are intentionally excluded to keep the pack small; use the summaries instead.

## Closing Summary
This checklist is the upload plan: read the files in order, narrate the pipeline, and lean on the summary and flow anchor for the video structure.
