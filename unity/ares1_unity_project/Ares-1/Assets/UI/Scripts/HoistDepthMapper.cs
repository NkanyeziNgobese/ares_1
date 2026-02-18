using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class HoistDepthMapper : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private RigBindings rig;
    [SerializeField] private TelemetryManager telemetry;

    [Header("Depth Mapping")]
    [SerializeField] private bool enableHoist = true;
    [FormerlySerializedAs("freezeWhenPaused")]
    [SerializeField] private bool respectPause = true;
    [SerializeField] private bool invertDepthSign = false;
    [SerializeField] private bool drillingDownMovesBlockDown = true;
    [SerializeField] private Vector3 worldDownAxis = Vector3.down;
    [FormerlySerializedAs("maxTravelMetersPerSecond")]
    [SerializeField] private float maxTravelSpeedMetersPerSec = 5f;
    [SerializeField] private float maxAbsDepthMeters = 5000f;

    [Header("Travel Limits (World Y)")]
    [SerializeField] private bool useTravelLimits = true;
    [SerializeField] private bool clampOnlyY = true;
    [SerializeField] private float minTravelWorldY;
    [SerializeField] private float maxTravelWorldY;
    [SerializeField] private bool autoInitLimitsFromRig = false;
    [SerializeField] private Transform minTravelMarker;
    [SerializeField] private Transform maxTravelMarker;
    [SerializeField] private float minMaxPadding = 0.0f;

    [Header("Debug")]
    [SerializeField] private bool debugDraw = false;
    [SerializeField] private KeyCode toggleDebugKey = KeyCode.F8;
    [SerializeField] private float debugLogIntervalSeconds = 0.25f;

    public float CurrentErrorMeters { get; private set; }

    private bool _calibrated;
    private float _refDepth;
    private Vector3 _refBitWorldPos;
    private Vector3 _cachedBitLocalInTravel;
    private float _lastDesiredBitY;
    private float _lastUnclampedDesiredTravelY;
    private float _lastClampedTravelY;
    private bool _lastWasClamped;
    private float _nextDebugLogTime;
    private bool _warnedMissingRefs;
    private bool _limitsInitWarned;
    private bool _limitsRangeWarned;

    private const string LogPrefix = "[HoistDepthMapper]";
    private const float FreshSampleMaxAgeSeconds = 2f;
    private const float MoveEpsilonSqr = 0.0000001f;

    private void Reset()
    {
        if (!rig) rig = FindFirstObjectByType<RigBindings>();
        if (!telemetry) telemetry = FindFirstObjectByType<TelemetryManager>();
    }

    private void OnValidate()
    {
        if (!rig) rig = FindFirstObjectByType<RigBindings>();
        if (!telemetry) telemetry = FindFirstObjectByType<TelemetryManager>();

        if (worldDownAxis.sqrMagnitude < 0.000001f)
            worldDownAxis = Vector3.down;

        if (maxTravelSpeedMetersPerSec < 0f)
            maxTravelSpeedMetersPerSec = 0f;

        if (maxAbsDepthMeters < 0f)
            maxAbsDepthMeters = 0f;

        if (minMaxPadding < 0f)
            minMaxPadding = 0f;

        if (debugLogIntervalSeconds < 0.05f)
            debugLogIntervalSeconds = 0.05f;

        ValidateBindings(false);
        ValidateTravelLimitConfig(true);
    }

    private void Start()
    {
        if (autoInitLimitsFromRig)
            InitLimitsFromMarkersOrDefaults(false);
    }

    private void LateUpdate()
    {
        if (Input.GetKeyDown(toggleDebugKey))
            debugDraw = !debugDraw;

        if (!enableHoist)
            return;

        if (!ValidateBindings(false))
            return;

        if (!HasValidDepthSample(out float depthNow))
            return;

        if (!_calibrated)
            Calibrate(depthNow);

        float depthSign = invertDepthSign ? -1f : 1f;
        float directionMultiplier = drillingDownMovesBlockDown ? 1f : -1f;
        float deltaDepth = (depthNow - _refDepth) * depthSign * directionMultiplier;
        if (maxAbsDepthMeters > 0f)
            deltaDepth = Mathf.Clamp(deltaDepth, -maxAbsDepthMeters, maxAbsDepthMeters);

        Vector3 downDir = NormalizeOrFallback(worldDownAxis, Vector3.down);
        Vector3 desiredBitWorldPos = _refBitWorldPos + downDir * deltaDepth;
        Vector3 desiredTravelWorldPos = desiredBitWorldPos - (rig.TravelingBlock.rotation * _cachedBitLocalInTravel);
        Vector3 unclampedDesiredTravelWorldPos = desiredTravelWorldPos;
        Vector3 currentTravelWorldPos = rig.TravelingBlock.position;

        _lastDesiredBitY = desiredBitWorldPos.y;
        _lastUnclampedDesiredTravelY = desiredTravelWorldPos.y;
        _lastClampedTravelY = desiredTravelWorldPos.y;
        _lastWasClamped = false;

        if (useTravelLimits)
        {
            float minY = minTravelWorldY + minMaxPadding;
            float maxY = maxTravelWorldY - minMaxPadding;
            if (minY > maxY)
            {
                float temp = minY;
                minY = maxY;
                maxY = temp;

                if (!_limitsRangeWarned)
                {
                    Debug.LogWarning($"{LogPrefix} Travel limits were invalid after padding; effective clamp range was swapped.", this);
                    _limitsRangeWarned = true;
                }
            }

            float clampedY = Mathf.Clamp(desiredTravelWorldPos.y, minY, maxY);
            _lastWasClamped = !Mathf.Approximately(_lastUnclampedDesiredTravelY, clampedY);
            _lastClampedTravelY = clampedY;

            if (clampOnlyY)
            {
                // Primary mode: constrain vertical hoist travel only.
                desiredTravelWorldPos = new Vector3(desiredTravelWorldPos.x, clampedY, desiredTravelWorldPos.z);
            }
            else
            {
                // Current implementation intentionally clamps Y only even in extended mode.
                desiredTravelWorldPos = new Vector3(desiredTravelWorldPos.x, clampedY, desiredTravelWorldPos.z);
            }
        }
        else
        {
            _limitsRangeWarned = false;
        }

        bool wouldMove = (unclampedDesiredTravelWorldPos - currentTravelWorldPos).sqrMagnitude > MoveEpsilonSqr;
        bool paused = respectPause && ScrollPauseController.IsPaused;

        if (debugDraw && wouldMove)
            TryLogMapping(depthNow, deltaDepth, _lastUnclampedDesiredTravelY, currentTravelWorldPos.y, _lastWasClamped);

        if (paused)
            return;

        float step = maxTravelSpeedMetersPerSec * Time.deltaTime;
        rig.TravelingBlock.position = Vector3.MoveTowards(currentTravelWorldPos, desiredTravelWorldPos, step);
        CurrentErrorMeters = Vector3.Distance(rig.BitTipMarker.position, desiredBitWorldPos);

        if (debugDraw)
            Debug.DrawLine(rig.BitTipMarker.position, desiredBitWorldPos, Color.white);
    }

    [ContextMenu("ValidateNow")]
    public void ValidateNow()
    {
        bool bindingsOk = ValidateBindings(true);
        bool limitsOk = ValidateTravelLimitConfig(true);
        Debug.Log(
            $"{LogPrefix} ValidateNow: bindingsOk={bindingsOk}, limitsEnabled={useTravelLimits}, " +
            $"limitsOk={limitsOk}, minY={minTravelWorldY:0.###}, maxY={maxTravelWorldY:0.###}, padding={minMaxPadding:0.###}",
            this);

        if (!clampOnlyY)
        {
            Debug.Log($"{LogPrefix} clampOnlyY=false is set, but this mapper currently clamps Y only by design.", this);
        }
    }

    [ContextMenu("Recalibrate")]
    public void Recalibrate()
    {
        _calibrated = false;
        Debug.Log($"{LogPrefix} Calibration cleared. Waiting for next valid telemetry sample.", this);
    }

    [ContextMenu("Print State")]
    public void PrintState()
    {
        float depthNow = telemetry ? telemetry.CurrentDepthMeters : float.NaN;
        float travelY = (rig && rig.TravelingBlock) ? rig.TravelingBlock.position.y : float.NaN;

        Debug.Log(
            $"{LogPrefix} calibrated={_calibrated}, depthNow={depthNow:0.000}, refDepth={_refDepth:0.000}, " +
            $"worldDownAxis=({worldDownAxis.x:0.###},{worldDownAxis.y:0.###},{worldDownAxis.z:0.###}), " +
            $"invertDepthSign={invertDepthSign}, drillingDownMovesBlockDown={drillingDownMovesBlockDown}, " +
            $"useTravelLimits={useTravelLimits}, minY={minTravelWorldY:0.000}, maxY={maxTravelWorldY:0.000}, " +
            $"lastDesiredBitY={_lastDesiredBitY:0.000}, desiredTravelY={_lastUnclampedDesiredTravelY:0.000}, " +
            $"clampedY={_lastClampedTravelY:0.000}, wasClamped={_lastWasClamped}, currentTravelY={travelY:0.000}",
            this);
    }

    [ContextMenu("Set Min From Current")]
    public void SetMinFromCurrent()
    {
        if (!rig || !rig.TravelingBlock)
        {
            Debug.LogWarning($"{LogPrefix} Cannot set min limit: RigBindings or TravelingBlock is missing.", this);
            return;
        }

        minTravelWorldY = rig.TravelingBlock.position.y;
        Debug.Log($"{LogPrefix} minTravelWorldY set from current traveling block Y: {minTravelWorldY:0.###}", this);
    }

    [ContextMenu("Set Max From Current")]
    public void SetMaxFromCurrent()
    {
        if (!rig || !rig.TravelingBlock)
        {
            Debug.LogWarning($"{LogPrefix} Cannot set max limit: RigBindings or TravelingBlock is missing.", this);
            return;
        }

        maxTravelWorldY = rig.TravelingBlock.position.y;
        Debug.Log($"{LogPrefix} maxTravelWorldY set from current traveling block Y: {maxTravelWorldY:0.###}", this);
    }

    [ContextMenu("Init Limits From Markers/Defaults")]
    public void InitLimitsFromMarkersOrDefaultsContext()
    {
        InitLimitsFromMarkersOrDefaults(true);
    }

    private void Calibrate(float depthNow)
    {
        _refDepth = depthNow;
        _refBitWorldPos = rig.BitTipMarker.position;
        _cachedBitLocalInTravel = rig.TravelingBlock.InverseTransformPoint(rig.BitTipMarker.position);
        _calibrated = true;
    }

    private bool HasValidDepthSample(out float depthNow)
    {
        depthNow = float.NaN;
        if (!telemetry)
            return false;

        depthNow = telemetry.CurrentDepthMeters;
        if (float.IsNaN(depthNow) || float.IsInfinity(depthNow))
            return false;

        if (maxAbsDepthMeters > 0f && Mathf.Abs(depthNow) > maxAbsDepthMeters)
            return false;

        if (telemetry.LastSampleAgeSeconds > FreshSampleMaxAgeSeconds)
            return false;

        return true;
    }

    private bool ValidateBindings(bool verbose)
    {
        bool ok = true;
        bool shouldLog = verbose || !_warnedMissingRefs;

        if (!rig)
        {
            ok = false;
            if (shouldLog) Debug.LogWarning($"{LogPrefix} Missing RigBindings reference.", this);
        }

        if (!telemetry)
        {
            ok = false;
            if (shouldLog) Debug.LogWarning($"{LogPrefix} Missing TelemetryManager reference.", this);
        }

        if (rig && !rig.TravelingBlock)
        {
            ok = false;
            if (shouldLog) Debug.LogWarning($"{LogPrefix} RigBindings.TravelingBlock is missing.", this);
        }

        if (rig && !rig.BitTipMarker)
        {
            ok = false;
            if (shouldLog) Debug.LogWarning($"{LogPrefix} RigBindings.BitTipMarker is missing.", this);
        }

        if (!ok)
        {
            _warnedMissingRefs = true;
            _calibrated = false;
            return false;
        }

        if (verbose)
            Debug.Log($"{LogPrefix} ValidateNow OK.", this);

        _warnedMissingRefs = false;
        return true;
    }

    private bool ValidateTravelLimitConfig(bool logWarnings)
    {
        if (!useTravelLimits)
            return true;

        bool ok = true;
        if (minTravelWorldY >= maxTravelWorldY)
        {
            ok = false;
            if (logWarnings)
            {
                Debug.LogWarning(
                    $"{LogPrefix} Travel limits look invalid: minTravelWorldY ({minTravelWorldY:0.###}) >= maxTravelWorldY ({maxTravelWorldY:0.###}).",
                    this);
            }
        }

        float minY = minTravelWorldY + minMaxPadding;
        float maxY = maxTravelWorldY - minMaxPadding;
        if (minY > maxY)
        {
            ok = false;
            if (logWarnings)
            {
                Debug.LogWarning(
                    $"{LogPrefix} Effective travel limits are invalid after padding: minY ({minY:0.###}) > maxY ({maxY:0.###}).",
                    this);
            }
        }

        return ok;
    }

    private void InitLimitsFromMarkersOrDefaults(bool force)
    {
        bool uninitialized = Mathf.Approximately(minTravelWorldY, 0f) && Mathf.Approximately(maxTravelWorldY, 0f);
        if (!force && !uninitialized)
            return;

        bool setAny = false;

        if (maxTravelMarker)
        {
            maxTravelWorldY = maxTravelMarker.position.y;
            setAny = true;
        }
        else if (rig && rig.CrownBlock)
        {
            maxTravelWorldY = rig.CrownBlock.position.y - 0.25f;
            setAny = true;
        }

        if (minTravelMarker)
        {
            minTravelWorldY = minTravelMarker.position.y;
            setAny = true;
        }
        else if (rig && rig.TravelingBlock)
        {
            minTravelWorldY = rig.TravelingBlock.position.y - 2.0f;
            setAny = true;

            if (!_limitsInitWarned)
            {
                Debug.LogWarning(
                    $"{LogPrefix} Bottom travel limit initialized from TravelingBlock - 2.0m. " +
                    "Use a dedicated bottom stop marker for production.",
                    this);
                _limitsInitWarned = true;
            }
        }

        if (force)
        {
            if (setAny)
            {
                Debug.Log(
                    $"{LogPrefix} Initialized limits. minTravelWorldY={minTravelWorldY:0.###}, maxTravelWorldY={maxTravelWorldY:0.###}.",
                    this);
            }
            else
            {
                Debug.LogWarning(
                    $"{LogPrefix} Could not initialize travel limits from markers/defaults. Assign markers or RigBindings refs.",
                    this);
            }
        }
    }

    private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
    {
        if (value.sqrMagnitude < 0.000001f)
            return fallback.normalized;
        return value.normalized;
    }

    private void TryLogMapping(float depthNow, float deltaDepth, float desiredTravelY, float travelY, bool wasClamped)
    {
        if (Time.unscaledTime < _nextDebugLogTime)
            return;

        Debug.Log(
            $"{LogPrefix} depthNow={depthNow:0.000} refDepth={_refDepth:0.000} deltaDepth={deltaDepth:0.000} " +
            $"desiredY={desiredTravelY:0.000} travelY={travelY:0.000} clamped={wasClamped}",
            this);

        _nextDebugLogTime = Time.unscaledTime + debugLogIntervalSeconds;
    }
}
