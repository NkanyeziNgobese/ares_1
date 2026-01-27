"""Subscribe to telemetry topics and persist latest values."""

from __future__ import annotations

import json
import logging
import os
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Dict

import paho.mqtt.client as mqtt
from dotenv import load_dotenv
from rich.logging import RichHandler

ROOT = Path(__file__).resolve().parents[2]
PYTHON_DIR = ROOT / "python"
if str(PYTHON_DIR) not in sys.path:
    sys.path.insert(0, str(PYTHON_DIR))


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def configure_logging() -> logging.Logger:
    logging.basicConfig(
        level=logging.INFO,
        format="%(message)s",
        datefmt="[%X]",
        handlers=[RichHandler(rich_tracebacks=True)],
    )
    return logging.getLogger("ares1.sub.telemetry")


def main() -> None:
    load_dotenv()
    logger = configure_logging()

    broker_host = os.getenv("MQTT_BROKER_HOST", "localhost")
    broker_port = int(os.getenv("MQTT_BROKER_PORT", "1883"))

    outputs_dir = ROOT / "outputs"
    outputs_dir.mkdir(parents=True, exist_ok=True)
    latest_path = outputs_dir / "telemetry_latest.json"

    state: Dict[str, Dict[str, object]] = {}
    last_print = 0.0

    def on_connect(client: mqtt.Client, userdata: object, flags: dict, rc: int) -> None:
        if rc != 0:
            logger.error("MQTT connect failed: rc=%s", rc)
            return
        logger.info("Connected to MQTT at %s:%s", broker_host, broker_port)
        client.subscribe("ares1/telemetry/#")

    def on_message(client: mqtt.Client, userdata: object, msg: mqtt.MQTTMessage) -> None:
        nonlocal last_print
        try:
            payload = json.loads(msg.payload.decode("utf-8"))
        except json.JSONDecodeError:
            return

        key = msg.topic.split("/")[-1]
        payload["topic"] = msg.topic
        payload["received_at"] = utc_now_iso()
        state[key] = payload

        latest_path.write_text(json.dumps(state, indent=2))

        now = time.time()
        if now - last_print >= 1.0:
            summary = " ".join(
                f"{k}={state[k].get('value')}" for k in sorted(state.keys())
            )
            logger.info("Latest: %s", summary)
            last_print = now

    client_id = f"ares1-sub-telemetry-{int(time.time())}"
    client = mqtt.Client(client_id=client_id)
    client.on_connect = on_connect
    client.on_message = on_message

    logger.info("Connecting to MQTT at %s:%s", broker_host, broker_port)
    client.connect(broker_host, broker_port, keepalive=60)
    client.loop_forever()


if __name__ == "__main__":
    main()
