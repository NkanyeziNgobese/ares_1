"""Physics baseline models for torque and drag."""

from __future__ import annotations


def torque_baseline(depth_m: float, mu: float, r_m: float, fn_per_m: float) -> float:
    """Return a simple torque baseline in N*m.

    Model:
        N_total = fn_per_m * depth_m
        torque = mu * N_total * r_m

    Inputs:
        depth_m: measured depth in meters
        mu: friction coefficient (dimensionless)
        r_m: effective radius in meters
        fn_per_m: distributed normal force per meter in newtons/m

    This is a first-order stub intended for anomaly residuals and unit tests.
    """
    if depth_m < 0:
        raise ValueError("depth_m must be non-negative")
    if mu < 0:
        raise ValueError("mu must be non-negative")
    if r_m <= 0:
        raise ValueError("r_m must be positive")
    if fn_per_m < 0:
        raise ValueError("fn_per_m must be non-negative")

    n_total = fn_per_m * depth_m
    return mu * n_total * r_m
