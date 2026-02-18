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
    private float _nextDebugLogTime;
    private bool _warnedMissingRefs;

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

        if (debugLogIntervalSeconds < 0.05f)
            debugLogIntervalSeconds = 0.05f;

        ValidateBindings(false);
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
        Vector3 currentTravelWorldPos = rig.TravelingBlock.position;

        _lastDesiredBitY = desiredBitWorldPos.y;

        bool wouldMove = (desiredTravelWorldPos - currentTravelWorldPos).sqrMagnitude > MoveEpsilonSqr;
        bool paused = respectPause && ScrollPauseController.IsPaused;

        if (debugDraw && wouldMove)
            TryLogMapping(depthNow, deltaDepth, desiredBitWorldPos.y, currentTravelWorldPos.y);

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
        ValidateBindings(true);
    }

    [ContextMenu("Recalibrate")]
    public void Recalibrate()
    {
        _calibrated = false;
        Debug.Log($"{LogPrefix} Calibration cleared. Waiting for next valid telemetry sample.", this);
    }

    [ContextMenu("PrintState")]
    public void PrintState()
    {
        float depthNow = telemetry ? telemetry.CurrentDepthMeters : float.NaN;
        float travelY = (rig && rig.TravelingBlock) ? rig.TravelingBlock.position.y : float.NaN;

        Debug.Log(
            $"{LogPrefix} calibrated={_calibrated}, depthNow={depthNow:0.000}, refDepth={_refDepth:0.000}, " +
            $"worldDownAxis=({worldDownAxis.x:0.###},{worldDownAxis.y:0.###},{worldDownAxis.z:0.###}), " +
            $"invertDepthSign={invertDepthSign}, lastDesiredBitY={_lastDesiredBitY:0.000}, currentTravelY={travelY:0.000}",
            this);
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

    private static Vector3 NormalizeOrFallback(Vector3 value, Vector3 fallback)
    {
        if (value.sqrMagnitude < 0.000001f)
            return fallback.normalized;
        return value.normalized;
    }

    private void TryLogMapping(float depthNow, float deltaDepth, float desiredBitY, float travelY)
    {
        if (Time.unscaledTime < _nextDebugLogTime)
            return;

        Debug.Log(
            $"{LogPrefix} depthNow={depthNow:0.000} refDepth={_refDepth:0.000} deltaDepth={deltaDepth:0.000} desiredBitY={desiredBitY:0.000} travelY={travelY:0.000}",
            this);

        _nextDebugLogTime = Time.unscaledTime + debugLogIntervalSeconds;
    }
}
