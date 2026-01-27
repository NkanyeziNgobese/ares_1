"""Subscribe to event topics and append to a JSONL log."""

from __future__ import annotations

import json
import logging
import os
import time
from datetime import datetime, timezone
from pathlib import Path

import paho.mqtt.client as mqtt
from dotenv import load_dotenv
from rich.logging import RichHandler


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def configure_logging() -> logging.Logger:
    logging.basicConfig(
        level=logging.INFO,
        format="%(message)s",
        datefmt="[%X]",
        handlers=[RichHandler(rich_tracebacks=True)],
    )
    return logging.getLogger("ares1.sub.events")


def main() -> None:
    load_dotenv()
    logger = configure_logging()

    broker_host = os.getenv("MQTT_BROKER_HOST", "localhost")
    broker_port = int(os.getenv("MQTT_BROKER_PORT", "1883"))

    root = Path(__file__).resolve().parents[2]
    outputs_dir = root / "outputs"
    outputs_dir.mkdir(parents=True, exist_ok=True)
    log_path = outputs_dir / "events_log.jsonl"

    def on_connect(client: mqtt.Client, userdata: object, flags: dict, rc: int) -> None:
        if rc != 0:
            logger.error("MQTT connect failed: rc=%s", rc)
            return
        logger.info("Connected to MQTT at %s:%s", broker_host, broker_port)
        client.subscribe("ares1/events/#")

    def on_message(client: mqtt.Client, userdata: object, msg: mqtt.MQTTMessage) -> None:
        try:
            payload = json.loads(msg.payload.decode("utf-8"))
        except json.JSONDecodeError:
            payload = {"raw": msg.payload.decode("utf-8", errors="replace")}

        payload["topic"] = msg.topic
        payload["received_at"] = utc_now_iso()

        with log_path.open("a", encoding="utf-8") as handle:
            handle.write(json.dumps(payload) + "\n")

        logger.warning("Event: %s", payload.get("event_type", "unknown"))

    client_id = f"ares1-sub-events-{int(time.time())}"
    client = mqtt.Client(client_id=client_id)
    client.on_connect = on_connect
    client.on_message = on_message

    logger.info("Connecting to MQTT at %s:%s", broker_host, broker_port)
    client.connect(broker_host, broker_port, keepalive=60)
    client.loop_forever()


if __name__ == "__main__":
    main()
