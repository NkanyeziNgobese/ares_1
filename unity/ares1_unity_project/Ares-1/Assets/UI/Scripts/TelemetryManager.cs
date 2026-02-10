using System;
using System.Collections.Concurrent;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class TelemetryManager : MonoBehaviour
{
    [Serializable]
    public class TelemetryPayload
    {
        public float depth;
        public float rop;
        public float wob;
        public float rpm;
        public float torque;
        public float flowIn;
        public float flowOut;
    }

    [Header("Telemetry Widgets (Text)")]
    [SerializeField] private TelemetryWidgetText depthWidget;
    [SerializeField] private TelemetryWidgetText ropWidget;
    [SerializeField] private TelemetryWidgetText wobWidget;
    [SerializeField] private TelemetryWidgetText rpmWidget;
    [SerializeField] private TelemetryWidgetText torqueWidget;
    [SerializeField] private TelemetryWidgetText flowInWidget;
    [SerializeField] private TelemetryWidgetText flowOutWidget;

    [Header("Right Track (Scrolling Geology)")]
    [SerializeField] private StratigraphyTrack stratigraphyTrack;

    // IMPORTANT UI NOTE (TW_ROP):
    // Ensure sibling order so TMP text stays on top:
    //   1) RopFill (Image) first
    //   2) Content (TMP container) last
    // Optional: lower RopFill alpha or add padding to Content.
    [Header("Gauges (Image Fill)")]
    [SerializeField] private TelemetryFillGauge ropGauge;
    [SerializeField] private float ropMax = 60f;
    [SerializeField] private TelemetryFillGauge wobGauge;
    [SerializeField] private float wobMax = 25f;
    [SerializeField] private TelemetryFillGauge rpmGauge;
    [SerializeField] private float rpmMax = 200f;
    [SerializeField] private TelemetryFillGauge torqueGauge;
    [SerializeField] private float torqueMax = 50f;
    [SerializeField] private TelemetryFillGauge flowInGauge;
    [SerializeField] private float flowInMax = 1200f;
    [SerializeField] private TelemetryFillGauge flowOutGauge;
    [SerializeField] private float flowOutMax = 1200f;

    [Header("Sparklines")]
    [SerializeField] private UiSparkline ropSparkline;
    [SerializeField] private UiSparkline wobSparkline;
    [SerializeField] private UiSparkline rpmSparkline;
    [SerializeField] private UiSparkline torqueSparkline;
    [SerializeField] private UiSparkline flowInSparkline;
    [SerializeField] private UiSparkline flowOutSparkline;

    [Header("Flow Imbalance Alert")]
    [SerializeField] private float flowDeltaWarning = 25f;
    [SerializeField] private float flowDeltaDanger = 60f;

    [Header("Data Freshness")]
    [SerializeField] private float staleAfterSeconds = 1.0f;
    [SerializeField] private float disconnectedAfterSeconds = 3.0f;

    public float SecondsSinceLastPacket { get; private set; } = float.PositiveInfinity;
    public bool IsStale => SecondsSinceLastPacket >= staleAfterSeconds;
    public bool IsDisconnected => SecondsSinceLastPacket >= disconnectedAfterSeconds;

    [Header("Telemetry Rate")]
    [SerializeField] private float hzSmoothing = 0.2f;
    public float RxHz { get; private set; }
    public float CurrentDepth { get; private set; }
    public float CurrentDepthMeters => CurrentDepth;
    public bool HasSampleReceived { get; private set; }
    public float LastSampleAgeSeconds { get; private set; } = float.PositiveInfinity;
    public float RawDepth { get; private set; }
    public float RawRop { get; private set; }
    public float RawWob { get; private set; }
    public float RawRpm { get; private set; }
    public float RawTorque { get; private set; }
    public float RawFlowIn { get; private set; }
    public float RawFlowOut { get; private set; }
    public float SmoothedDepth { get; private set; }
    public float SmoothedRop { get; private set; }
    public float SmoothedWob { get; private set; }
    public float SmoothedRpm { get; private set; }
    public float SmoothedTorque { get; private set; }
    public float SmoothedFlowIn { get; private set; }
    public float SmoothedFlowOut { get; private set; }

    [Header("Smoothing (EMA)")]
    [SerializeField] private bool smoothingEnabled = true;
    [SerializeField, Range(0.01f, 1f)] private float emaAlpha = 0.2f;
    [SerializeField] private bool smoothRop = true;
    [SerializeField] private bool smoothWob = true;
    [SerializeField] private bool smoothRpm = true;
    [SerializeField] private bool smoothTorque = true;
    [SerializeField] private bool smoothFlowIn = true;
    [SerializeField] private bool smoothFlowOut = true;
    [SerializeField] private bool smoothDepth = false;
    [SerializeField] private bool useSmoothedForGauges = true;
    [SerializeField] private bool useSmoothedForTrendlines = true;
    [SerializeField] private bool showSmoothedNumbers = false;

    [Header("Smoothing Debug (F9)")]
    [SerializeField] private TMP_Text smoothingDebugText;
    [SerializeField] private KeyCode smoothingDebugToggleKey = KeyCode.F9;
    [SerializeField] private bool smoothingDebugEnabled = false;

    [Header("Units / Formatting")]
    [SerializeField] private string depthUnit = "m";
    [SerializeField] private string ropUnit = "m/hr";
    [SerializeField] private string wobUnit = "t";
    [SerializeField] private string rpmUnit = "rpm";
    [SerializeField] private string torqueUnit = "kNm";
    [SerializeField] private string flowUnit = "L/min";

    // Thread-safe queue in case your receiver runs on another thread
    private readonly ConcurrentQueue<string> _jsonQueue = new ConcurrentQueue<string>();
    private int _packetsThisWindow = 0;
    private float _hzWindowTimer = 0f;

    private float _rawDepth;
    private float _rawRop;
    private float _rawWob;
    private float _rawRpm;
    private float _rawTorque;
    private float _rawFlowIn;
    private float _rawFlowOut;

    private EmaFilter _depthEma;
    private EmaFilter _ropEma;
    private EmaFilter _wobEma;
    private EmaFilter _rpmEma;
    private EmaFilter _torqueEma;
    private EmaFilter _flowInEma;
    private EmaFilter _flowOutEma;

    /// <summary>
    /// Call this from your Python receiver whenever JSON arrives.
    /// Safe to call from background threads.
    /// </summary>
    public void EnqueueJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        _jsonQueue.Enqueue(json);
    }

    private void OnValidate()
    {
        // Guard: Try to auto-find widgets by name if unassigned (helpful early on)
        // This is optional safety, not required for production.
        TryAutoWireIfMissing();

        if (hzSmoothing <= 0f) hzSmoothing = 0.2f;
        if (emaAlpha < 0.01f) emaAlpha = 0.01f;
        if (emaAlpha > 1f) emaAlpha = 1f;
        if (smoothingDebugText) smoothingDebugText.gameObject.SetActive(smoothingDebugEnabled);

        if (!ropGauge)
        {
            var twRop = GameObject.Find("TW_ROP");
            if (twRop)
            {
                Debug.LogWarning(
                    "ROP gauge not wired. Assign TelemetryFillGauge (RopFill) to TelemetryManager. Ensure TW_ROP contains RopFill Image.",
                    this);
            }
        }

        if (!wobGauge)
        {
            var twWob = GameObject.Find("TW_WOB");
            if (twWob)
            {
                Debug.LogWarning(
                    "WOB gauge missing. Add child Image 'WobFill' under TW_WOB, attach TelemetryFillGauge, and ensure sibling order: Fill first, Content last.",
                    this);
            }
        }

        if (!rpmGauge)
        {
            var twRpm = GameObject.Find("TW_RPM");
            if (twRpm)
            {
                Debug.LogWarning(
                    "RPM gauge missing. Add child Image 'RpmFill' under TW_RPM, attach TelemetryFillGauge, and ensure sibling order: Fill first, Content last.",
                    this);
            }
        }

        if (!torqueGauge)
        {
            var twTorque = GameObject.Find("TW_Torque");
            if (twTorque)
            {
                Debug.LogWarning(
                    "Torque gauge missing. Add child Image 'TorqueFill' under TW_Torque, attach TelemetryFillGauge, and ensure sibling order: Fill first, Content last.",
                    this);
            }
        }

        if (!flowInGauge)
        {
            var twFlowIn = GameObject.Find("TW_FlowIn");
            if (twFlowIn)
            {
                Debug.LogWarning(
                    "Flow In gauge missing. Add child Image 'FlowInFill' under TW_FlowIn, attach TelemetryFillGauge, and ensure sibling order: Fill first, Content last.",
                    this);
            }
        }

        if (!flowOutGauge)
        {
            var twFlowOut = GameObject.Find("TW_FlowOut");
            if (twFlowOut)
            {
                Debug.LogWarning(
                    "Flow Out gauge missing. Add child Image 'FlowOutFill' under TW_FlowOut, attach TelemetryFillGauge, and ensure sibling order: Fill first, Content last.",
                    this);
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(smoothingDebugToggleKey))
        {
            smoothingDebugEnabled = !smoothingDebugEnabled;
            if (smoothingDebugText) smoothingDebugText.gameObject.SetActive(smoothingDebugEnabled);
            if (smoothingDebugEnabled) UpdateSmoothingDebug();
        }

        LastSampleAgeSeconds += Time.unscaledDeltaTime;
        SecondsSinceLastPacket += Time.deltaTime;
        _hzWindowTimer += Time.deltaTime;

        // Keep only the latest packet each frame (real-time telemetry pattern)
        string json = null;
        while (_jsonQueue.TryDequeue(out var next))
            json = next;

        if (json != null)
        {
            SecondsSinceLastPacket = 0f;
            _packetsThisWindow++;

            try
            {
                var t = JsonUtility.FromJson<TelemetryPayload>(json);
                Apply(t);
                LastSampleAgeSeconds = 0f;
                HasSampleReceived = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Telemetry JSON parse failed: {e.Message}\n{json}");
            }
        }

        if (_hzWindowTimer >= hzSmoothing)
        {
            RxHz = _packetsThisWindow / _hzWindowTimer;
            _packetsThisWindow = 0;
            _hzWindowTimer = 0f;
        }
    }

    private void Apply(TelemetryPayload t)
    {
        _rawDepth = t.depth;
        _rawRop = t.rop;
        _rawWob = t.wob;
        _rawRpm = t.rpm;
        _rawTorque = t.torque;
        _rawFlowIn = t.flowIn;
        _rawFlowOut = t.flowOut;

        CurrentDepth = _rawDepth;

        float alpha = Mathf.Clamp(emaAlpha, 0.01f, 1f);
        float smDepth = UpdateFilter(ref _depthEma, _rawDepth, smoothDepth, alpha);
        float smRop = UpdateFilter(ref _ropEma, _rawRop, smoothRop, alpha);
        float smWob = UpdateFilter(ref _wobEma, _rawWob, smoothWob, alpha);
        float smRpm = UpdateFilter(ref _rpmEma, _rawRpm, smoothRpm, alpha);
        float smTorque = UpdateFilter(ref _torqueEma, _rawTorque, smoothTorque, alpha);
        float smFlowIn = UpdateFilter(ref _flowInEma, _rawFlowIn, smoothFlowIn, alpha);
        float smFlowOut = UpdateFilter(ref _flowOutEma, _rawFlowOut, smoothFlowOut, alpha);

        RawDepth = _rawDepth;
        RawRop = _rawRop;
        RawWob = _rawWob;
        RawRpm = _rawRpm;
        RawTorque = _rawTorque;
        RawFlowIn = _rawFlowIn;
        RawFlowOut = _rawFlowOut;

        SmoothedDepth = smDepth;
        SmoothedRop = smRop;
        SmoothedWob = smWob;
        SmoothedRpm = smRpm;
        SmoothedTorque = smTorque;
        SmoothedFlowIn = smFlowIn;
        SmoothedFlowOut = smFlowOut;

        float depthForText = (showSmoothedNumbers && smoothingEnabled && smoothDepth) ? smDepth : _rawDepth;
        float ropForText = (showSmoothedNumbers && smoothingEnabled && smoothRop) ? smRop : _rawRop;
        float wobForText = (showSmoothedNumbers && smoothingEnabled && smoothWob) ? smWob : _rawWob;
        float rpmForText = (showSmoothedNumbers && smoothingEnabled && smoothRpm) ? smRpm : _rawRpm;
        float torqueForText = (showSmoothedNumbers && smoothingEnabled && smoothTorque) ? smTorque : _rawTorque;
        float flowInForText = (showSmoothedNumbers && smoothingEnabled && smoothFlowIn) ? smFlowIn : _rawFlowIn;
        float flowOutForText = (showSmoothedNumbers && smoothingEnabled && smoothFlowOut) ? smFlowOut : _rawFlowOut;

        if (depthWidget)  depthWidget.SetValue($"{depthForText:0.0} {depthUnit}");
        if (ropWidget)    ropWidget.SetValue($"{ropForText:0.0} {ropUnit}");
        if (wobWidget)    wobWidget.SetValue($"{wobForText:0.0} {wobUnit}");
        if (rpmWidget)    rpmWidget.SetValue($"{rpmForText:0} {rpmUnit}");
        if (torqueWidget) torqueWidget.SetValue($"{torqueForText:0.0} {torqueUnit}");
        if (flowInWidget) flowInWidget.SetValue($"{flowInForText:0.0} {flowUnit}");
        if (flowOutWidget)flowOutWidget.SetValue($"{flowOutForText:0.0} {flowUnit}");

        bool freezeGauges = ScrollPauseController.IsPaused && useSmoothedForGauges;
        if (!freezeGauges)
        {
            float ropForGauge = (smoothingEnabled && useSmoothedForGauges && smoothRop) ? smRop : _rawRop;
            float wobForGauge = (smoothingEnabled && useSmoothedForGauges && smoothWob) ? smWob : _rawWob;
            float rpmForGauge = (smoothingEnabled && useSmoothedForGauges && smoothRpm) ? smRpm : _rawRpm;
            float torqueForGauge = (smoothingEnabled && useSmoothedForGauges && smoothTorque) ? smTorque : _rawTorque;
            float flowInForGauge = (smoothingEnabled && useSmoothedForGauges && smoothFlowIn) ? smFlowIn : _rawFlowIn;
            float flowOutForGauge = (smoothingEnabled && useSmoothedForGauges && smoothFlowOut) ? smFlowOut : _rawFlowOut;

            if (ropGauge)
            {
                ropGauge.SetMax(ropMax);
                ropGauge.SetValue(ropForGauge);
            }

            if (wobGauge)
            {
                wobGauge.SetMax(wobMax);
                wobGauge.SetValue(wobForGauge);
            }

            if (rpmGauge)
            {
                rpmGauge.SetMax(rpmMax);
                rpmGauge.SetValue(rpmForGauge);
            }

            if (torqueGauge)
            {
                torqueGauge.SetMax(torqueMax);
                torqueGauge.SetValue(torqueForGauge);
            }

            if (flowInGauge)
            {
                flowInGauge.SetMax(flowInMax);
                flowInGauge.SetValue(flowInForGauge);
            }

            if (flowOutGauge)
            {
                flowOutGauge.SetMax(flowOutMax);
                flowOutGauge.SetValue(flowOutForGauge);

                var delta = Mathf.Abs(_rawFlowIn - _rawFlowOut);
                if (delta >= flowDeltaDanger)
                {
                    flowOutGauge.SetAlertOverride(true, true);
                }
                else if (delta >= flowDeltaWarning)
                {
                    flowOutGauge.SetAlertOverride(true, false);
                }
                else
                {
                    flowOutGauge.SetAlertOverride(false, false);
                }
            }
        }

        float ropForTrend = (smoothingEnabled && useSmoothedForTrendlines && smoothRop) ? smRop : _rawRop;
        float wobForTrend = (smoothingEnabled && useSmoothedForTrendlines && smoothWob) ? smWob : _rawWob;
        float rpmForTrend = (smoothingEnabled && useSmoothedForTrendlines && smoothRpm) ? smRpm : _rawRpm;
        float torqueForTrend = (smoothingEnabled && useSmoothedForTrendlines && smoothTorque) ? smTorque : _rawTorque;
        float flowInForTrend = (smoothingEnabled && useSmoothedForTrendlines && smoothFlowIn) ? smFlowIn : _rawFlowIn;
        float flowOutForTrend = (smoothingEnabled && useSmoothedForTrendlines && smoothFlowOut) ? smFlowOut : _rawFlowOut;

        Push01(ropSparkline, ropForTrend, ropMax);
        Push01(wobSparkline, wobForTrend, wobMax);
        Push01(rpmSparkline, rpmForTrend, rpmMax);
        Push01(torqueSparkline, torqueForTrend, torqueMax);
        Push01(flowInSparkline, flowInForTrend, flowInMax);
        Push01(flowOutSparkline, flowOutForTrend, flowOutMax);

        if (stratigraphyTrack) stratigraphyTrack.SetDepth(_rawDepth);

        if (smoothingDebugEnabled) UpdateSmoothingDebug();
    }

    [ContextMenu("DEV: Push Sample JSON")]
    private void Dev_PushSampleJson()
    {
        EnqueueJson("{\"depth\":-1250.5,\"rop\":28.3,\"wob\":14.7,\"rpm\":132,\"torque\":24.9,\"flowIn\":805,\"flowOut\":796}");
    }

    [ContextMenu("DEV: Push WARNING JSON")]
    private void Dev_PushWarningJson()
    {
        EnqueueJson("{\"depth\":-1400.0,\"rop\":45.0,\"wob\":18.0,\"rpm\":160,\"torque\":38.0,\"flowIn\":900,\"flowOut\":860}");
    }

    [ContextMenu("DEV: Push DANGER JSON")]
    private void Dev_PushDangerJson()
    {
        EnqueueJson("{\"depth\":-1600.0,\"rop\":58.0,\"wob\":24.0,\"rpm\":195,\"torque\":48.0,\"flowIn\":1000,\"flowOut\":880}");
    }

    private static void Push01(UiSparkline s, float value, float max)
    {
        if (!s) return;
        if (ScrollPauseController.IsPaused) return;
        float denom = Mathf.Max(0.0001f, max);
        s.PushSample01(Mathf.Clamp01(value / denom));
    }

    public void ResetSmoothingToRaw()
    {
        _depthEma.Reset(_rawDepth);
        _ropEma.Reset(_rawRop);
        _wobEma.Reset(_rawWob);
        _rpmEma.Reset(_rawRpm);
        _torqueEma.Reset(_rawTorque);
        _flowInEma.Reset(_rawFlowIn);
        _flowOutEma.Reset(_rawFlowOut);
        SmoothedDepth = _rawDepth;
        SmoothedRop = _rawRop;
        SmoothedWob = _rawWob;
        SmoothedRpm = _rawRpm;
        SmoothedTorque = _rawTorque;
        SmoothedFlowIn = _rawFlowIn;
        SmoothedFlowOut = _rawFlowOut;
        if (smoothingDebugEnabled) UpdateSmoothingDebug();
    }

    private float UpdateFilter(ref EmaFilter filter, float raw, bool channelEnabled, float alpha)
    {
        if (!smoothingEnabled || !channelEnabled)
        {
            filter.Reset(raw);
            return raw;
        }
        return filter.Update(raw, alpha);
    }

    private void UpdateSmoothingDebug()
    {
        if (!smoothingDebugEnabled || !smoothingDebugText) return;

        float depthSm = SmoothedDepth;
        float ropSm = SmoothedRop;
        float torqueSm = SmoothedTorque;
        float flowInSm = SmoothedFlowIn;

        smoothingDebugText.text =
            $"[Smoothing]\n" +
            $"Enabled: {smoothingEnabled}  Alpha: {emaAlpha:0.###}\n" +
            $"Depth: {_rawDepth:0.0} -> {depthSm:0.0}\n" +
            $"ROP: {_rawRop:0.0} -> {ropSm:0.0}\n" +
            $"Torque: {_rawTorque:0.0} -> {torqueSm:0.0}\n" +
            $"FlowIn: {_rawFlowIn:0.0} -> {flowInSm:0.0}\n" +
            $"Pause Visuals freezes visuals only; numbers stay live.";
    }

    private void TryAutoWireIfMissing()
    {
        // Only attempt in Editor, only if missing refs
        if (depthWidget && ropWidget && wobWidget && rpmWidget && torqueWidget && flowInWidget && flowOutWidget
            && ropGauge && wobGauge && rpmGauge && torqueGauge && flowInGauge && flowOutGauge
            && ropSparkline && wobSparkline && rpmSparkline && torqueSparkline && flowInSparkline && flowOutSparkline)
            return;

        // Find by exact names you used in the scene
        depthWidget  ??= FindWidget("TW_Depth");
        ropWidget    ??= FindWidget("TW_ROP");
        wobWidget    ??= FindWidget("TW_WOB");
        rpmWidget    ??= FindWidget("TW_RPM");
        torqueWidget ??= FindWidget("TW_Torque");
        flowInWidget ??= FindWidget("TW_FlowIn");
        flowOutWidget??= FindWidget("TW_FlowOut");

        if (!ropGauge)
        {
            var ropFill = GameObject.Find("RopFill");
            if (ropFill)
            {
                ropGauge = ropFill.GetComponent<TelemetryFillGauge>();
            }
            else
            {
                var ropRoot = GameObject.Find("TW_ROP");
                if (ropRoot)
                {
                    ropGauge = ropRoot.GetComponentInChildren<TelemetryFillGauge>(true);
                }
            }
        }

        if (!wobGauge)
        {
            var wobFill = GameObject.Find("WobFill");
            if (wobFill)
            {
                wobGauge = wobFill.GetComponent<TelemetryFillGauge>();
            }
            else
            {
                var wobRoot = GameObject.Find("TW_WOB");
                if (wobRoot)
                {
                    wobGauge = wobRoot.GetComponentInChildren<TelemetryFillGauge>(true);
                }
            }
        }

        if (!rpmGauge)
        {
            var rpmFill = GameObject.Find("RpmFill");
            if (rpmFill)
            {
                rpmGauge = rpmFill.GetComponent<TelemetryFillGauge>();
            }
            else
            {
                var rpmRoot = GameObject.Find("TW_RPM");
                if (rpmRoot)
                {
                    rpmGauge = rpmRoot.GetComponentInChildren<TelemetryFillGauge>(true);
                }
            }
        }

        if (!torqueGauge)
        {
            var torqueFill = GameObject.Find("TorqueFill");
            if (torqueFill)
            {
                torqueGauge = torqueFill.GetComponent<TelemetryFillGauge>();
            }
            else
            {
                var torqueRoot = GameObject.Find("TW_Torque");
                if (torqueRoot)
                {
                    torqueGauge = torqueRoot.GetComponentInChildren<TelemetryFillGauge>(true);
                }
            }
        }

        if (!flowInGauge)
        {
            var flowInFill = GameObject.Find("FlowInFill");
            if (flowInFill)
            {
                flowInGauge = flowInFill.GetComponent<TelemetryFillGauge>();
            }
            else
            {
                var flowInRoot = GameObject.Find("TW_FlowIn");
                if (flowInRoot)
                {
                    flowInGauge = flowInRoot.GetComponentInChildren<TelemetryFillGauge>(true);
                }
            }
        }

        if (!flowOutGauge)
        {
            var flowOutFill = GameObject.Find("FlowOutFill");
            if (flowOutFill)
            {
                flowOutGauge = flowOutFill.GetComponent<TelemetryFillGauge>();
            }
            else
            {
                var flowOutRoot = GameObject.Find("TW_FlowOut");
                if (flowOutRoot)
                {
                    flowOutGauge = flowOutRoot.GetComponentInChildren<TelemetryFillGauge>(true);
                }
            }
        }

        if (!ropSparkline)
        {
            var ropRoot = GameObject.Find("TW_ROP");
            if (ropRoot)
            {
                ropSparkline = ropRoot.GetComponentInChildren<UiSparkline>(true);
            }
        }

        if (!wobSparkline)
        {
            var wobRoot = GameObject.Find("TW_WOB");
            if (wobRoot)
            {
                wobSparkline = wobRoot.GetComponentInChildren<UiSparkline>(true);
            }
        }

        if (!rpmSparkline)
        {
            var rpmRoot = GameObject.Find("TW_RPM");
            if (rpmRoot)
            {
                rpmSparkline = rpmRoot.GetComponentInChildren<UiSparkline>(true);
            }
        }

        if (!torqueSparkline)
        {
            var torqueRoot = GameObject.Find("TW_Torque");
            if (torqueRoot)
            {
                torqueSparkline = torqueRoot.GetComponentInChildren<UiSparkline>(true);
            }
        }

        if (!flowInSparkline)
        {
            var flowInRoot = GameObject.Find("TW_FlowIn");
            if (flowInRoot)
            {
                flowInSparkline = flowInRoot.GetComponentInChildren<UiSparkline>(true);
            }
        }

        if (!flowOutSparkline)
        {
            var flowOutRoot = GameObject.Find("TW_FlowOut");
            if (flowOutRoot)
            {
                flowOutSparkline = flowOutRoot.GetComponentInChildren<UiSparkline>(true);
            }
        }

        // Stratigraphy
        if (!stratigraphyTrack)
            stratigraphyTrack = FindFirstObjectByType<StratigraphyTrack>();
    }

    private static TelemetryWidgetText FindWidget(string goName)
    {
        var go = GameObject.Find(goName);
        if (!go) return null;
        return go.GetComponent<TelemetryWidgetText>();
    }
}
