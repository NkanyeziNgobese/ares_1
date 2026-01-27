# Physics Baseline: Torque and Drag (Stub)

## Purpose
Provide a simple, explainable baseline torque model for anomaly residuals and unit tests.

## Model
Assume a distributed normal force along the string:

```
N_total = fn_per_m * depth_m
Torque = mu * N_total * r_m
```

Where:
- `depth_m` is measured depth in meters.
- `fn_per_m` is distributed normal force per meter (N/m).
- `mu` is a friction coefficient (dimensionless).
- `r_m` is an effective radius in meters.

The output torque is in N*m. The model is intentionally simple and meant to be
replaced by a higher-fidelity stiff-string or torque-and-drag model later.

## Limitations
- Ignores curvature, buoyancy, and changing contact conditions.
- Uses a constant `mu` and `fn_per_m` over depth.
- Suitable only as a first-order residual baseline.

## Reference Implementation
See `python/ares1/physics/torque_drag.py`.

## Test Coverage
See `python/tests/test_torque_drag.py`.
