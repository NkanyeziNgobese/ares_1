using System;
using System.Collections.Concurrent;
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
        CurrentDepth = t.depth;

        if (depthWidget)  depthWidget.SetValue($"{t.depth:0.0} {depthUnit}");
        if (ropWidget)    ropWidget.SetValue($"{t.rop:0.0} {ropUnit}");
        if (wobWidget)    wobWidget.SetValue($"{t.wob:0.0} {wobUnit}");
        if (rpmWidget)    rpmWidget.SetValue($"{t.rpm:0} {rpmUnit}");
        if (torqueWidget) torqueWidget.SetValue($"{t.torque:0.0} {torqueUnit}");
        if (flowInWidget) flowInWidget.SetValue($"{t.flowIn:0.0} {flowUnit}");
        if (flowOutWidget)flowOutWidget.SetValue($"{t.flowOut:0.0} {flowUnit}");

        if (ropGauge)
        {
            ropGauge.SetMax(ropMax);
            ropGauge.SetValue(t.rop);
        }

        if (wobGauge)
        {
            wobGauge.SetMax(wobMax);
            wobGauge.SetValue(t.wob);
        }

        if (rpmGauge)
        {
            rpmGauge.SetMax(rpmMax);
            rpmGauge.SetValue(t.rpm);
        }

        if (torqueGauge)
        {
            torqueGauge.SetMax(torqueMax);
            torqueGauge.SetValue(t.torque);
        }

        if (flowInGauge)
        {
            flowInGauge.SetMax(flowInMax);
            flowInGauge.SetValue(t.flowIn);
        }

        if (flowOutGauge)
        {
            flowOutGauge.SetMax(flowOutMax);
            flowOutGauge.SetValue(t.flowOut);

            var delta = Mathf.Abs(t.flowIn - t.flowOut);
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

        Push01(ropSparkline, t.rop, ropMax);
        Push01(wobSparkline, t.wob, wobMax);
        Push01(rpmSparkline, t.rpm, rpmMax);
        Push01(torqueSparkline, t.torque, torqueMax);
        Push01(flowInSparkline, t.flowIn, flowInMax);
        Push01(flowOutSparkline, t.flowOut, flowOutMax);

        if (stratigraphyTrack) stratigraphyTrack.SetDepth(t.depth);
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
        float denom = Mathf.Max(0.0001f, max);
        s.PushSample01(Mathf.Clamp01(value / denom));
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
