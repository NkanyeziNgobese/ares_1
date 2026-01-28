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

    [Header("Units / Formatting")]
    [SerializeField] private string depthUnit = "m";
    [SerializeField] private string ropUnit = "m/hr";
    [SerializeField] private string wobUnit = "t";
    [SerializeField] private string rpmUnit = "rpm";
    [SerializeField] private string torqueUnit = "kNm";
    [SerializeField] private string flowUnit = "L/min";

    // Thread-safe queue in case your receiver runs on another thread
    private readonly ConcurrentQueue<string> _jsonQueue = new ConcurrentQueue<string>();

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
    }

    private void Update()
    {
        // Keep only the latest packet each frame (real-time telemetry pattern)
        string json = null;
        while (_jsonQueue.TryDequeue(out var next))
            json = next;

        if (json == null) return;

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

    private void Apply(TelemetryPayload t)
    {
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

        if (stratigraphyTrack) stratigraphyTrack.SetDepth(t.depth);
    }

    [ContextMenu("DEV: Push Sample JSON")]
    private void Dev_PushSampleJson()
    {
        EnqueueJson("{\"depth\":-1250.5,\"rop\":28.3,\"wob\":14.7,\"rpm\":132,\"torque\":24.9,\"flowIn\":805,\"flowOut\":796}");
    }

    private void TryAutoWireIfMissing()
    {
        // Only attempt in Editor, only if missing refs
        if (depthWidget && ropWidget && wobWidget && rpmWidget && torqueWidget && flowInWidget && flowOutWidget && ropGauge)
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
