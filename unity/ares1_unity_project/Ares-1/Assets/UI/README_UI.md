# Ares-1 Mission Dashboard (HMI) — Unity UGUI

## Purpose
Operator-facing mission dashboard for real-time drilling telemetry. Built in Unity UGUI with TextMeshPro, gauge fills, sparklines, and a stratigraphy/ depth scale viewport.

## Scene + Source of Truth
- Primary scene: `Assets/Scenes/Main Scene.unity`
- Note: anything under `Assets/_Recovery/` is not canonical.

## UI Architecture Overview
- Canvas Scaler: reference resolution **1920×1080**
- Left panel: telemetry widgets (label/value/gauges/trendlines)
- Right panel: Stratigraphy viewport + Depth scale viewport (marker fixed)
- DevTools: debug + simulation toggles
- AlarmSystem: thresholds + audio alarm

## Key Objects (Hierarchy Map)
- Canvas
  - LeftPanel (Vertical Layout Group + Use Child Scale Width/Height)
  - RightPanel
    - StratigraphyViewport (RectMask2D) → StratigraphyTrack → StratigraphyRaw (RawImage)
    - DepthScaleViewport (RectMask2D) → DepthScaleContent (spawns PF_DepthTick)
    - DepthMarkerLine (fixed)
- DevTools
  - TelemetryManager
  - DevTelemetryDriver (F5)
  - ScrollPauseController (F7, F8)
  - DirectionProofHUD (F6)
  - AlarmSystem (TelemetryAlarmApplier + AlarmSoundController)
  - MissionStateSystem (MissionStateClassifier)
  - UiWiringGuard

## Runtime Controls (Keys)
- F5: toggle/push dev telemetry
- F6: direction proof HUD toggle
- F7: Pause Visuals (freezes: depth scale + stratigraphy + trendlines + gauges; numbers remain live)
- F8: Scroll HUD toggle
- F9: smoothing debug toggle (if present)
- F10: mission state debug toggle
- F11: mute alarm toggle

## Data Contract (JSON)
Required fields:
`depth`, `rop`, `wob`, `rpm`, `torque`, `flowIn`, `flowOut`

Units (as currently displayed):
- depth: meters
- rop: m/hr
- wob: t
- rpm: rpm
- torque: kNm
- flow: L/min

## Setup Checklist (Wiring)
- TelemetryManager: TMP value refs + gauges + sparklines assigned
- StratigraphyScroller: RawImage assigned and texture present
- DepthScaleController: tick prefab assigned + viewport/root set
- Alarm targets configured (keys + thresholds)
- Alarm audio clip assigned to AudioSource
- UiWiringGuard: “Validate Wiring Now” shows OK (or warnings to fix)

## Self‑Test Checklist
1) Play → F5 → verify telemetry values change
2) F7 pause/resume visuals (trendlines freeze, geology freeze, ticks freeze; numbers keep updating)
3) Force alarm by lowering thresholds → sound + color; mute with F11
4) Telemetry health pill: live / stale / disconnected behavior
5) Mission state pill changes or shows Unknown; debug with F10
