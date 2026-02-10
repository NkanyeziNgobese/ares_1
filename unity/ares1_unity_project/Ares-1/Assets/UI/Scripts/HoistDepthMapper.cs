using UnityEngine;

[DisallowMultipleComponent]
public class HoistDepthMapper : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private RigBindings rig;
    [SerializeField] private TelemetryManager telemetry;

    [Header("Depth Mapping")]
    [SerializeField] private bool enableHoist = true;
    [SerializeField] private bool calibrateOnFirstSample = true;
    [SerializeField] private bool freezeWhenPaused = true;
    [SerializeField] private bool invertDepthSign = true;
    [SerializeField] private bool drillingDownMovesBlockDown = true;
    [SerializeField] private Vector3 worldDownAxis = Vector3.down;
    [SerializeField] private float smoothing = 0f; // 0 = no smoothing
    [SerializeField] private float maxTravelMetersPerSecond = 5f;
    [SerializeField] private float maxAbsDepthMeters = 5000f;

    [Header("Debug")]
    [SerializeField] private bool debugDraw = false;
    [SerializeField] private KeyCode toggleDebugKey = KeyCode.F8;

    public float CurrentErrorMeters { get; private set; }

    private bool _calibrated;
    private float _refDepth;
    private Vector3 _wellboreStartWorld;
    private Vector3 _bitTipLocalInTravel;
    private Vector3 _lastTravelWorld;
    private bool _warnedMissingRefs;
    private float _nextDebugLogTime;

    private const string LogPrefix = "[HoistDepthMapper]";

    private void Reset()
    {
        if (!rig) rig = FindFirstObjectByType<RigBindings>();
        if (!telemetry) telemetry = FindFirstObjectByType<TelemetryManager>();
    }

    private void OnValidate()
    {
        if (!rig) rig = FindFirstObjectByType<RigBindings>();
        if (!telemetry) telemetry = FindFirstObjectByType<TelemetryManager>();

        if (worldDownAxis.sqrMagnitude < 0.000001f) worldDownAxis = Vector3.down;
        if (smoothing < 0f) smoothing = 0f;
        if (maxTravelMetersPerSecond < 0f) maxTravelMetersPerSecond = 0f;
        if (maxAbsDepthMeters < 0f) maxAbsDepthMeters = 0f;

        if (!rig) Debug.LogWarning($"{LogPrefix} Missing RigBindings reference.", this);
        if (!telemetry) Debug.LogWarning($"{LogPrefix} Missing TelemetryManager reference.", this);
    }

    private void Start()
    {
        if (rig && rig.TravelingBlock)
            _lastTravelWorld = rig.TravelingBlock.position;

        if (!calibrateOnFirstSample)
            RecalibrateNow();
    }

    private void LateUpdate()
    {
        if (Input.GetKeyDown(toggleDebugKey))
            debugDraw = !debugDraw;

        if (!enableHoist)
            return;

        if (freezeWhenPaused && ScrollPauseController.IsPaused)
            return;

        if (!HasRequiredRefs())
            return;

        float depthNow = telemetry.CurrentDepthMeters;

        if (calibrateOnFirstSample && !_calibrated && HasValidSample())
            RecalibrateNow();

        if (!_calibrated)
            return;

        float deltaDepthMeters = depthNow - _refDepth;
        if (invertDepthSign) deltaDepthMeters *= -1f;
        if (maxAbsDepthMeters > 0f)
            deltaDepthMeters = Mathf.Clamp(deltaDepthMeters, -maxAbsDepthMeters, maxAbsDepthMeters);
        float dir = drillingDownMovesBlockDown ? 1f : -1f;

        Vector3 down = worldDownAxis;
        if (down.sqrMagnitude < 0.000001f) down = Vector3.down;
        down.Normalize();

        // Desired bit position is driven from the wellbore marker by depth delta.
        Vector3 desiredBitWorld = _wellboreStartWorld + down * (deltaDepthMeters * dir);
        Vector3 desiredTravelWorld = desiredBitWorld - (rig.TravelingBlock.rotation * _bitTipLocalInTravel);
        Vector3 currentTravelWorld = rig.TravelingBlock.position;

        if (smoothing > 0f)
            desiredTravelWorld = Vector3.Lerp(currentTravelWorld, desiredTravelWorld, Time.deltaTime * smoothing);

        float maxStep = maxTravelMetersPerSecond * Time.deltaTime;
        rig.TravelingBlock.position = Vector3.MoveTowards(currentTravelWorld, desiredTravelWorld, maxStep);

        _lastTravelWorld = rig.TravelingBlock.position;

        CurrentErrorMeters = Vector3.Distance(rig.BitTipMarker.position, desiredBitWorld);

        if (debugDraw)
        {
            Debug.DrawLine(rig.BitTipMarker.position, rig.WellboreStartMarker.position, Color.white);
            Debug.DrawLine(rig.BitTipMarker.position, desiredBitWorld, Color.white);

            if (Time.unscaledTime >= _nextDebugLogTime)
            {
                Debug.Log(
                    $"{LogPrefix} depthNow={depthNow:0.00}, refDepth={_refDepth:0.00}, delta={deltaDepthMeters:0.00}, " +
                    $"localBitOffsetY={_bitTipLocalInTravel.y:0.000}, localBitOffsetMag={_bitTipLocalInTravel.magnitude:0.000}, " +
                    $"desiredBitY={desiredBitWorld.y:0.000}, travelY={rig.TravelingBlock.position.y:0.000}, bitY={rig.BitTipMarker.position.y:0.000}, " +
                    $"age={telemetry.LastSampleAgeSeconds:0.00}s, calibrated={_calibrated}",
                    this);
                _nextDebugLogTime = Time.unscaledTime + 0.5f;
            }
        }
    }

    [ContextMenu("Recalibrate Now")]
    public void RecalibrateNow()
    {
        if (TryCalibrate())
            Debug.Log($"{LogPrefix} Recalibrated successfully.", this);
        else
            Debug.LogWarning($"{LogPrefix} Recalibration failed due to missing references.", this);
    }

    [ContextMenu("Print State")]
    public void PrintState()
    {
        string rigState = rig ? "OK" : "MISSING";
        string tmState = telemetry ? "OK" : "MISSING";
        float depthNow = telemetry ? telemetry.CurrentDepthMeters : 0f;
        float deltaDepthMeters = depthNow - _refDepth;
        if (invertDepthSign) deltaDepthMeters *= -1f;
        if (maxAbsDepthMeters > 0f)
            deltaDepthMeters = Mathf.Clamp(deltaDepthMeters, -maxAbsDepthMeters, maxAbsDepthMeters);
        float dir = drillingDownMovesBlockDown ? 1f : -1f;
        Vector3 down = worldDownAxis.sqrMagnitude < 0.000001f ? Vector3.down : worldDownAxis.normalized;
        Vector3 desiredBitWorld = _wellboreStartWorld + down * (deltaDepthMeters * dir);
        Vector3 desiredTravelWorld = desiredBitWorld - (rig && rig.TravelingBlock ? (rig.TravelingBlock.rotation * _bitTipLocalInTravel) : Vector3.zero);
        float travelY = (rig && rig.TravelingBlock) ? rig.TravelingBlock.position.y : 0f;
        float bitY = (rig && rig.BitTipMarker) ? rig.BitTipMarker.position.y : 0f;

        Debug.Log(
            $"{LogPrefix} rig={rigState}, telemetry={tmState}, calibrated={_calibrated}, " +
            $"depthNow={depthNow:0.00}, refDepth={_refDepth:0.00}, delta={deltaDepthMeters:0.00}, " +
            $"bitTipLocalMag={_bitTipLocalInTravel.magnitude:0.000}, travelY={travelY:0.000}, bitY={bitY:0.000}, " +
            $"desiredBitY={desiredBitWorld.y:0.000}, desiredTravelY={desiredTravelWorld.y:0.000}, " +
            $"error={CurrentErrorMeters:0.000}m, age={ (telemetry ? telemetry.LastSampleAgeSeconds : float.PositiveInfinity):0.00}s, " +
            $"enableHoist={enableHoist}, freezeWhenPaused={freezeWhenPaused}, calibrateOnFirstSample={calibrateOnFirstSample}",
            this);
    }

    private bool TryCalibrate()
    {
        if (!HasRequiredRefs())
            return false;

        _refDepth = telemetry.CurrentDepthMeters;
        _wellboreStartWorld = rig.WellboreStartMarker.position;
        _bitTipLocalInTravel = rig.TravelingBlock.InverseTransformPoint(rig.BitTipMarker.position);
        _lastTravelWorld = rig.TravelingBlock.position;
        _calibrated = true;

        if (_bitTipLocalInTravel.magnitude > 50f)
        {
            Debug.LogWarning(
                $"{LogPrefix} Local bit offset is large ({_bitTipLocalInTravel.magnitude:0.00}m). Check RigBindings assignments.",
                this);
        }

        return true;
    }

    [ContextMenu("Print Current Mapping")]
    public void PrintCurrentMapping()
    {
        PrintState();
    }

    private bool HasValidSample()
    {
        if (!telemetry) return false;
        return telemetry.HasSampleReceived && telemetry.LastSampleAgeSeconds <= 2f;
    }

    private bool HasRequiredRefs()
    {
        if (!rig || !telemetry || !rig.TravelingBlock || !rig.BitTipMarker || !rig.WellboreStartMarker)
        {
            if (!_warnedMissingRefs)
            {
                if (!rig) Debug.LogWarning($"{LogPrefix} Missing RigBindings.", this);
                if (!telemetry) Debug.LogWarning($"{LogPrefix} Missing TelemetryManager.", this);
                if (rig && !rig.TravelingBlock) Debug.LogWarning($"{LogPrefix} RigBindings.TravelingBlock is missing.", this);
                if (rig && !rig.BitTipMarker) Debug.LogWarning($"{LogPrefix} RigBindings.BitTipMarker is missing.", this);
                if (rig && !rig.WellboreStartMarker) Debug.LogWarning($"{LogPrefix} RigBindings.WellboreStartMarker is missing.", this);
                _warnedMissingRefs = true;
            }

            _calibrated = false;
            return false;
        }

        _warnedMissingRefs = false;
        return true;
    }
}
