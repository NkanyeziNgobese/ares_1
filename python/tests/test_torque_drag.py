from ares1.physics.torque_drag import torque_baseline


def test_torque_positive() -> None:
    torque = torque_baseline(depth_m=1000.0, mu=0.3, r_m=0.1, fn_per_m=3000.0)
    assert torque > 0.0


def test_torque_increases_with_depth_and_mu() -> None:
    t1 = torque_baseline(depth_m=1000.0, mu=0.3, r_m=0.1, fn_per_m=3000.0)
    t2 = torque_baseline(depth_m=1500.0, mu=0.3, r_m=0.1, fn_per_m=3000.0)
    t3 = torque_baseline(depth_m=1500.0, mu=0.5, r_m=0.1, fn_per_m=3000.0)
    assert t2 > t1
    assert t3 > t2
