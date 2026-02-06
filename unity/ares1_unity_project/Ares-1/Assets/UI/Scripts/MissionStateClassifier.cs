using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MissionStateClassifier : MonoBehaviour
{
    public enum MissionState
    {
        Unknown,
        Drilling,
        Circulating,
        Connection,
        OffBottom
    }

    [Header("Refs")]
    [SerializeField] private TelemetryManager telemetryManager;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private Image stateDot;
    [SerializeField] private TMP_Text debugText;

    [Header("Debug")]
    [SerializeField] private KeyCode debugToggleKey = KeyCode.F10;
    [SerializeField] private bool debugEnabled = false;

    [Header("Colors")]
    [SerializeField] private Color safeBlue = new Color32(0, 148, 255, 255);
    [SerializeField] private Color warningAmber = new Color32(255, 191, 0, 255);
    [SerializeField] private Color dangerRed = new Color32(255, 59, 48, 255);
    [SerializeField] private Color neutralGrey = new Color32(154, 167, 178, 255);

    [Header("Rule Thresholds")]
    [SerializeField] private float ropDrillingMin = 1.0f;
    [SerializeField] private float wobDrillingMin = 2.0f;
    [SerializeField] private float rpmRotatingMin = 30.0f;
    [SerializeField] private float flowCirculatingMin = 200.0f;
    [SerializeField] private float torqueRotatingMin = 1.0f;
    [SerializeField] private float flowDiffLossWarn = 100.0f;
    [SerializeField] private float stableSeconds = 2.0f;

    private MissionState _currentState = MissionState.Unknown;
    private MissionState _candidateState = MissionState.Unknown;
    private float _candidateTimer;

    private void Reset()
    {
        telemetryManager = FindFirstObjectByType<TelemetryManager>();
        if (!stateText) stateText = GetComponentInChildren<TMP_Text>(true);
        if (!stateDot) stateDot = GetComponentInChildren<Image>(true);
    }

    private void OnValidate()
    {
        if (!telemetryManager) telemetryManager = FindFirstObjectByType<TelemetryManager>();
        if (stableSeconds < 0f) stableSeconds = 0f;
        if (debugText) debugText.gameObject.SetActive(debugEnabled);
    }

    private void Update()
    {
        if (Input.GetKeyDown(debugToggleKey))
        {
            debugEnabled = !debugEnabled;
            if (debugText) debugText.gameObject.SetActive(debugEnabled);
        }

        if (!telemetryManager)
        {
            ApplyState(MissionState.Unknown);
            return;
        }

        MissionState candidate = EvaluateCandidate();

        if (candidate != _candidateState)
        {
            _candidateState = candidate;
            _candidateTimer = 0f;
        }
        else
        {
            _candidateTimer += Time.deltaTime;
            if (_candidateTimer >= stableSeconds && _currentState != _candidateState)
            {
                _currentState = _candidateState;
            }
        }

        ApplyState(_currentState);
        UpdateDebug(candidate);
    }

    private MissionState EvaluateCandidate()
    {
        if (telemetryManager.IsDisconnected) return MissionState.Unknown;

        float rop = telemetryManager.RawRop;
        float wob = telemetryManager.RawWob;
        float rpm = telemetryManager.RawRpm;
        float torque = telemetryManager.RawTorque;
        float flowIn = telemetryManager.RawFlowIn;
        float flowOut = telemetryManager.RawFlowOut;

        bool drillingLike = (rop > ropDrillingMin) &&
                            (wob > wobDrillingMin) &&
                            (rpm > rpmRotatingMin || torque > torqueRotatingMin);

        bool circulatingLike = (flowIn > flowCirculatingMin) &&
                                (rpm < rpmRotatingMin) &&
                                (rop < ropDrillingMin);

        bool offBottomLike = (wob < wobDrillingMin) &&
                             (rop < ropDrillingMin) &&
                             (flowIn > flowCirculatingMin);

        bool connectionLike = (rpm < rpmRotatingMin) &&
                              (flowIn < flowCirculatingMin) &&
                              (wob < wobDrillingMin) &&
                              (rop < ropDrillingMin);

        if (drillingLike) return MissionState.Drilling;
        if (circulatingLike) return MissionState.Circulating;
        if (offBottomLike) return MissionState.OffBottom;
        if (connectionLike) return MissionState.Connection;
        return MissionState.Unknown;
    }

    private void ApplyState(MissionState state)
    {
        if (stateText) stateText.text = $"STATE: {state}";

        if (stateDot)
        {
            stateDot.color = state switch
            {
                MissionState.Drilling => safeBlue,
                MissionState.Circulating => safeBlue,
                MissionState.OffBottom => warningAmber,
                MissionState.Connection => warningAmber,
                _ => neutralGrey
            };
        }
    }

    private void UpdateDebug(MissionState candidate)
    {
        if (!debugEnabled || !debugText || !telemetryManager) return;

        float depth = telemetryManager.RawDepth;
        float rop = telemetryManager.RawRop;
        float wob = telemetryManager.RawWob;
        float rpm = telemetryManager.RawRpm;
        float torque = telemetryManager.RawTorque;
        float flowIn = telemetryManager.RawFlowIn;
        float flowOut = telemetryManager.RawFlowOut;
        float flowDiff = flowIn - flowOut;

        debugText.text =
            $"[MissionState]\n" +
            $"raw depth={depth:0.0} rop={rop:0.0} wob={wob:0.0} rpm={rpm:0} torque={torque:0.0}\n" +
            $"flowIn={flowIn:0.0} flowOut={flowOut:0.0} diff={flowDiff:0.0}\n" +
            $"candidate={candidate} current={_currentState} t={_candidateTimer:0.00}/{stableSeconds:0.00}\n" +
            $"IsDisconnected={telemetryManager.IsDisconnected} IsStale={telemetryManager.IsStale}\n" +
            $"lossWarn>{flowDiffLossWarn:0.0}";
    }
}
