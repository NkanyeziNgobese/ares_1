using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DirectionProofHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TelemetryManager telemetryManager;

    [Tooltip("Assign your DepthScaleContent RectTransform (the moving tick container).")]
    [SerializeField] private RectTransform depthScaleContent;

    [Tooltip("Assign StratigraphyRaw (RawImage).")]
    [SerializeField] private RawImage stratigraphyRaw;

    [Header("UI")]
    [SerializeField] private TMP_Text hudText;

    [Header("Toggle")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F6;
    [SerializeField] private bool visible = false;

    private float _prevDepth;
    private float _prevTickProbeY;
    private float _prevUvY;
    private bool _hasPrev;

    private void Reset()
    {
        telemetryManager = FindFirstObjectByType<TelemetryManager>();
        hudText = GetComponent<TMP_Text>();
    }

    private void OnValidate()
    {
        if (!telemetryManager) telemetryManager = FindFirstObjectByType<TelemetryManager>();
        if (!hudText) hudText = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        SetVisible(visible);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            visible = !visible;
            SetVisible(visible);
        }

        if (!visible) return;
        if (!telemetryManager || !hudText) return;

        float depth = telemetryManager.CurrentDepth;

        // Probe tick motion: sample first child tick's anchoredPosition.y if available
        float tickY = float.NaN;
        if (depthScaleContent && depthScaleContent.childCount > 0)
        {
            var child = depthScaleContent.GetChild(0) as RectTransform;
            if (child) tickY = child.anchoredPosition.y;
        }

        // Probe strat UV
        float uvY = float.NaN;
        if (stratigraphyRaw)
            uvY = stratigraphyRaw.uvRect.y;

        if (!_hasPrev)
        {
            _prevDepth = depth;
            _prevTickProbeY = tickY;
            _prevUvY = uvY;
            _hasPrev = true;
        }

        float dDepth = depth - _prevDepth;
        float dTickY = (!float.IsNaN(tickY) && !float.IsNaN(_prevTickProbeY)) ? (tickY - _prevTickProbeY) : float.NaN;
        float dUvY   = (!float.IsNaN(uvY) && !float.IsNaN(_prevUvY)) ? (uvY - _prevUvY) : float.NaN;

        // Interpretation helpers (simple, not theoretical):
        // - drilling down = depth increasing (dDepth > 0)
        // - "ticks moving up" on screen often corresponds to anchoredPosition.y increasing (less negative)
        // - strat "moving up" depends on your chosen uv sign; we just show delta
        string tickDir = float.IsNaN(dTickY) ? "N/A" : (dTickY > 0f ? "UP-ish" : (dTickY < 0f ? "DOWN-ish" : "STILL"));
        string uvDir   = float.IsNaN(dUvY) ? "N/A" : (dUvY > 0f ? "uvY++" : (dUvY < 0f ? "uvY--" : "STILL"));

        hudText.text =
            "[Direction Proof HUD]\n" +
            $"Depth: {depth:0.00} m   (Δ {dDepth:+0.000;-0.000;0.000})\n" +
            $"TickProbeY: {(float.IsNaN(tickY) ? "N/A" : tickY.ToString("0.0"))}   (Δ {(float.IsNaN(dTickY) ? 0f : dTickY):+0.00;-0.00;0.00}) => {tickDir}\n" +
            $"Strat uvY: {(float.IsNaN(uvY) ? "N/A" : uvY.ToString("0.000"))}   (Δ {(float.IsNaN(dUvY) ? 0f : dUvY):+0.000;-0.000;0.000}) => {uvDir}\n" +
            $"Fresh: {(telemetryManager.IsDisconnected ? "DISCONNECTED" : telemetryManager.IsStale ? "STALE" : "LIVE")}\n" +
            "F5: Sim Telemetry   F6: Toggle HUD";

        _prevDepth = depth;
        _prevTickProbeY = tickY;
        _prevUvY = uvY;
    }

    private void SetVisible(bool on)
    {
        if (hudText) hudText.gameObject.SetActive(on);
    }
}
