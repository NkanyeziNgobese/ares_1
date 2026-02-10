using UnityEngine;

[DisallowMultipleComponent]
public class HoistDepthMapper : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private RigBindings rig;
    [SerializeField] private TelemetryManager telemetry;

    [Header("Depth Mapping")]
    [SerializeField] private bool invertDepthSign = true;
    [SerializeField] private Vector3 worldDownAxis = Vector3.down;
    [SerializeField] private bool respectPause = false;
    [SerializeField] private float smoothing = 0f; // 0 = no smoothing
    [SerializeField] private float maxAbsDepth = 5000f;

    [Header("Debug")]
    [SerializeField] private bool debugDraw = false;
    [SerializeField] private KeyCode toggleDebugKey = KeyCode.F8;

    public float CurrentErrorMeters { get; private set; }

    private float _refDepth;
    private Vector3 _refBitToTravelOffset;
    private Vector3 _wellboreStartWorld;
    private bool _isCalibrated;
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
        if (maxAbsDepth < 0f) maxAbsDepth = 0f;

        if (!rig) Debug.LogWarning($"{LogPrefix} Missing RigBindings reference.", this);
        if (!telemetry) Debug.LogWarning($"{LogPrefix} Missing TelemetryManager reference.", this);
    }

    private void Start()
    {
        TryCalibrate();
    }

    private void LateUpdate()
    {
        if (Input.GetKeyDown(toggleDebugKey))
            debugDraw = !debugDraw;

        if (respectPause && ScrollPauseController.IsPaused)
            return;

        if (!HasRequiredRefs())
            return;

        if (!_isCalibrated && !TryCalibrate())
            return;

        float depthNow = telemetry.CurrentDepth;
        float deltaDepthMeters = depthNow - _refDepth;
        if (invertDepthSign) deltaDepthMeters *= -1f;
        if (maxAbsDepth > 0f) deltaDepthMeters = Mathf.Clamp(deltaDepthMeters, -maxAbsDepth, maxAbsDepth);

        Vector3 down = worldDownAxis;
        if (down.sqrMagnitude < 0.000001f) down = Vector3.down;
        down.Normalize();

        // Desired bit position is driven from the wellbore marker by depth delta.
        Vector3 desiredBitWorld = _wellboreStartWorld + down * deltaDepthMeters;
        Vector3 desiredTravelWorld = desiredBitWorld - _refBitToTravelOffset;

        if (smoothing > 0f)
            rig.TravelingBlock.position = Vector3.Lerp(rig.TravelingBlock.position, desiredTravelWorld, Time.deltaTime * smoothing);
        else
            rig.TravelingBlock.position = desiredTravelWorld;

        CurrentErrorMeters = Vector3.Distance(rig.BitTipMarker.position, desiredBitWorld);

        if (debugDraw)
        {
            Debug.DrawLine(rig.BitTipMarker.position, rig.WellboreStartMarker.position, Color.white);
            Debug.DrawLine(rig.BitTipMarker.position, desiredBitWorld, Color.white);

            if (Time.unscaledTime >= _nextDebugLogTime)
            {
                Debug.Log($"{LogPrefix} depth={depthNow:0.00} targetErr={CurrentErrorMeters:0.000}m", this);
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

    [ContextMenu("Print Current Mapping")]
    public void PrintCurrentMapping()
    {
        string rigState = rig ? "OK" : "MISSING";
        string tmState = telemetry ? "OK" : "MISSING";
        float depth = telemetry ? telemetry.CurrentDepth : 0f;

        Debug.Log(
            $"{LogPrefix} rig={rigState}, telemetry={tmState}, calibrated={_isCalibrated}, " +
            $"depth={depth:0.00}, refDepth={_refDepth:0.00}, error={CurrentErrorMeters:0.000}m, " +
            $"invertDepthSign={invertDepthSign}, smoothing={smoothing:0.###}",
            this);
    }

    private bool TryCalibrate()
    {
        if (!HasRequiredRefs())
            return false;

        _refDepth = telemetry.CurrentDepth;
        _refBitToTravelOffset = rig.BitTipMarker.position - rig.TravelingBlock.position;
        _wellboreStartWorld = rig.WellboreStartMarker.position;
        _isCalibrated = true;
        return true;
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

            _isCalibrated = false;
            return false;
        }

        _warnedMissingRefs = false;
        return true;
    }
}
