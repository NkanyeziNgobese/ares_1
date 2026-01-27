# MQTT Broker (Mosquitto)

## Run

```bash
docker compose -f mqtt/docker-compose.yml up -d
```

## Stop

```bash
docker compose -f mqtt/docker-compose.yml down
```

## Ports
- 1883: MQTT
- 9001: WebSockets (optional)
