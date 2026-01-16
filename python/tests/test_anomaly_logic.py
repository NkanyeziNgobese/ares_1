from ares1.ml.anomaly import RollingZScoreDetector


def test_anomaly_triggers_on_spike() -> None:
    detector = RollingZScoreDetector(window_size=20, z_threshold=3.0, min_samples=10)
    baseline = [0.05, -0.02, 0.03, -0.04, 0.06, -0.05, 0.02, -0.01, 0.04, -0.03]

    for value in baseline:
        assert detector.update(value) is None

    event = detector.update(1.0)
    assert event is not None
    assert abs(event["z_score"]) >= 3.0
