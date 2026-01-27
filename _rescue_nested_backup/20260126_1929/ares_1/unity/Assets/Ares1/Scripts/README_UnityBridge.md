# Unity MQTT Bridge Plan

## Goals
- Subscribe to `ares1/telemetry/#` and `ares1/events/#`.
- Deserialize JSON to `TelemetryMessage` objects.
- Drive UI and playback from MQTT in real time.

## Client Options
- MQTTnet (recommended for Unity C#)
- Alternative Unity MQTT clients (evaluate license and maintenance)

## Integration Notes
- Use `MqttTopicMap` constants to avoid topic drift.
- Keep a rate limiter for UI updates (10-20 Hz).
- Consider a replay mode from `outputs/telemetry_log.csv`.
