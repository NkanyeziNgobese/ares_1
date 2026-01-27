"""Lightweight rolling z-score anomaly detector for torque residuals."""

from __future__ import annotations

import json
import logging
import os
import statistics
import time
from collections import deque
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Deque, Optional

import paho.mqtt.client as mqtt
from dotenv import load_dotenv
from rich.logging import RichHandler

from ares1.physics.torque_drag import torque_baseline


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


@dataclass
class RollingZScoreDetector:
    window_size: int = 60
    z_threshold: float = 3.0
    min_samples: int = 30
    _window: Deque[float] = field(default_factory=lambda: deque(maxlen=60))

    def __post_init__(self) -> None:
        if self.window_size <= 1:
            raise ValueError("window_size must be > 1")
        if self.min_samples < 1:
            raise ValueError("min_samples must be >= 1")
        if self.min_samples > self.window_size:
            raise ValueError("min_samples cannot exceed window_size")
        self._window = deque(maxlen=self.window_size)

    def update(self, value: float) -> Optional[dict]:
        if len(self._window) < self.min_samples:
            self._window.append(value)
            return None

        mean = statistics.fmean(self._window)
        stdev = statistics.pstdev(self._window)
        if stdev <= 1e-9:
            self._window.append(value)
            return None

        z_score = (value - mean) / stdev
        self._window.append(value)

        if abs(z_score) >= self.z_threshold:
            return {
                "value": value,
                "mean": mean,
                "stdev": stdev,
                "z_score": z_score,
            }
        return None


@dataclass
class TorqueAnomalyDetector:
    mu: float = 0.35
    r_m: float = 0.1
    fn_per_m: float = 3500.0
    z_threshold: float = 3.0
    window_size: int = 60
    min_samples: int = 30
    detector: RollingZScoreDetector = field(init=False)

    def __post_init__(self) -> None:
        self.detector = RollingZScoreDetector(
            window_size=self.window_size,
            z_threshold=self.z_threshold,
            min_samples=self.min_samples,
        )

    def update(self, depth_m: float, torque_nm: float) -> Optional[dict]:
        baseline = torque_baseline(depth_m, self.mu, self.r_m, self.fn_per_m)
        residual = torque_nm - baseline
        result = self.detector.update(residual)
        if result is None:
            return None

        return {
            "event_type": "torque_anomaly",
            "timestamp": utc_now_iso(),
            "depth_m": depth_m,
            "torque_nm": torque_nm,
            "baseline_nm": baseline,
            "residual_nm": residual,
            "z_score": result["z_score"],
            "z_threshold": self.z_threshold,
            "model": {
                "mu": self.mu,
                "r_m": self.r_m,
                "fn_per_m": self.fn_per_m,
                "window_size": self.window_size,
                "min_samples": self.min_samples,
            },
        }


def configure_logging() -> logging.Logger:
    logging.basicConfig(
        level=logging.INFO,
        format="%(message)s",
        datefmt="[%X]",
        handlers=[RichHandler(rich_tracebacks=True)],
    )
    return logging.getLogger("ares1.anomaly")


def run_mqtt() -> None:
    load_dotenv()
    logger = configure_logging()

    broker_host = os.getenv("MQTT_BROKER_HOST", "localhost")
    broker_port = int(os.getenv("MQTT_BROKER_PORT", "1883"))

    detector = TorqueAnomalyDetector(
        mu=float(os.getenv("ANOMALY_MU", "0.35")),
        r_m=float(os.getenv("ANOMALY_R_M", "0.1")),
        fn_per_m=float(os.getenv("ANOMALY_FN_PER_M", "3500")),
        z_threshold=float(os.getenv("ANOMALY_Z_THRESHOLD", "3.0")),
        window_size=int(os.getenv("ANOMALY_WINDOW", "60")),
        min_samples=int(os.getenv("ANOMALY_MIN_SAMPLES", "30")),
    )

    state = {"depth_m": None}

    def on_connect(client: mqtt.Client, userdata: object, flags: dict, rc: int) -> None:
        if rc != 0:
            logger.error("MQTT connect failed: rc=%s", rc)
            return
        logger.info("Connected to MQTT at %s:%s", broker_host, broker_port)
        client.subscribe("ares1/telemetry/depth")
        client.subscribe("ares1/telemetry/torque")

    def on_message(client: mqtt.Client, userdata: object, msg: mqtt.MQTTMessage) -> None:
        try:
            payload = json.loads(msg.payload.decode("utf-8"))
        except json.JSONDecodeError:
            logger.warning("Non-JSON payload on %s", msg.topic)
            return

        value = payload.get("value")
        if value is None:
            return

        if msg.topic.endswith("/depth"):
            state["depth_m"] = float(value)
            return

        if msg.topic.endswith("/torque"):
            depth_m = state.get("depth_m")
            if depth_m is None:
                return
            event = detector.update(depth_m=depth_m, torque_nm=float(value))
            if event is None:
                return
            client.publish("ares1/events/anomaly", json.dumps(event), qos=0, retain=False)
            logger.warning(
                "Anomaly: z=%.2f depth=%.1f torque=%.1f",
                event["z_score"],
                event["depth_m"],
                event["torque_nm"],
            )

    client_id = f"ares1-anomaly-{int(time.time())}"
    client = mqtt.Client(client_id=client_id)
    client.on_connect = on_connect
    client.on_message = on_message

    logger.info("Connecting to MQTT at %s:%s", broker_host, broker_port)
    client.connect(broker_host, broker_port, keepalive=60)
    client.loop_forever()


def main() -> None:
    run_mqtt()


if __name__ == "__main__":
    main()
