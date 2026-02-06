using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TelemetryAlarmApplier : MonoBehaviour
{
    [System.Serializable]
    public class MetricTarget
    {
        public string key;
        public TMP_Text valueText;
        public Image ring;
        public ThresholdRule rule;
        public string displayName;
    }

    [SerializeField] private TelemetryManager telemetryManager;
    [SerializeField] private bool useSmoothedValues = false;
    [SerializeField] private bool freezeColorsWhenPaused = false;
    [SerializeField] private MetricTarget[] targets;

    [Header("Colors")]
    [SerializeField] private Color safeBlue = new Color32(0, 148, 255, 255);
    [SerializeField] private Color warningAmber = new Color32(255, 191, 0, 255);
    [SerializeField] private Color dangerRed = new Color32(255, 59, 48, 255);

    [Header("Active Alarms (optional)")]
    [SerializeField] private TMP_Text activeAlarmsText;
    [SerializeField] private bool showOnlyWarningAndDanger = true;
    [SerializeField] private int maxLines = 6;

    private readonly StringBuilder _sb = new StringBuilder(256);

    public bool HasWarningActive { get; private set; }
    public bool HasDangerActive { get; private set; }

    private void Reset()
    {
        telemetryManager = FindFirstObjectByType<TelemetryManager>();
    }

    private void OnValidate()
    {
        if (!telemetryManager) telemetryManager = FindFirstObjectByType<TelemetryManager>();
        if (maxLines < 1) maxLines = 1;
    }

    private void Update()
    {
        if (!telemetryManager) return;
        if (freezeColorsWhenPaused && ScrollPauseController.IsPaused) return;

        HasWarningActive = false;
        HasDangerActive = false;

        int lines = 0;
        if (activeAlarmsText) _sb.Clear();

        if (targets != null)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                var t = targets[i];
                if (t == null) continue;

                if (!TryGetMetric(t.key, useSmoothedValues, out float value))
                    continue;

                TelemetrySeverity severity = t.rule != null ? t.rule.Evaluate(value) : TelemetrySeverity.Safe;
                Color c = ColorFor(severity);

                if (severity == TelemetrySeverity.Danger) HasDangerActive = true;
                else if (severity == TelemetrySeverity.Warning) HasWarningActive = true;

                if (t.valueText) t.valueText.color = c;
                if (t.ring) t.ring.color = c;

                if (activeAlarmsText)
                {
                    bool include = !showOnlyWarningAndDanger || severity != TelemetrySeverity.Safe;
                    if (include && lines < maxLines)
                    {
                        string name = string.IsNullOrWhiteSpace(t.displayName) ? t.key : t.displayName;
                        _sb.Append(name)
                            .Append(": ")
                            .Append(severity)
                            .Append(" (")
                            .Append(value.ToString("0.0"))
                            .Append(")\n");
                        lines++;
                    }
                }
            }
        }

        if (activeAlarmsText)
            activeAlarmsText.text = _sb.ToString();
    }

    private Color ColorFor(TelemetrySeverity s)
    {
        return s switch
        {
            TelemetrySeverity.Danger => dangerRed,
            TelemetrySeverity.Warning => warningAmber,
            _ => safeBlue
        };
    }

    private bool TryGetMetric(string key, bool smoothed, out float value)
    {
        value = 0f;
        if (telemetryManager == null || string.IsNullOrWhiteSpace(key)) return false;

        if (EqualsKey(key, "rop"))
        {
            value = smoothed ? telemetryManager.SmoothedRop : telemetryManager.RawRop;
            return true;
        }
        if (EqualsKey(key, "wob"))
        {
            value = smoothed ? telemetryManager.SmoothedWob : telemetryManager.RawWob;
            return true;
        }
        if (EqualsKey(key, "rpm"))
        {
            value = smoothed ? telemetryManager.SmoothedRpm : telemetryManager.RawRpm;
            return true;
        }
        if (EqualsKey(key, "torque"))
        {
            value = smoothed ? telemetryManager.SmoothedTorque : telemetryManager.RawTorque;
            return true;
        }
        if (EqualsKey(key, "flowin"))
        {
            value = smoothed ? telemetryManager.SmoothedFlowIn : telemetryManager.RawFlowIn;
            return true;
        }
        if (EqualsKey(key, "flowout"))
        {
            value = smoothed ? telemetryManager.SmoothedFlowOut : telemetryManager.RawFlowOut;
            return true;
        }
        if (EqualsKey(key, "depth"))
        {
            value = smoothed ? telemetryManager.SmoothedDepth : telemetryManager.RawDepth;
            return true;
        }

        return false;
    }

    private static bool EqualsKey(string key, string expected)
    {
        return string.Equals(key, expected, System.StringComparison.OrdinalIgnoreCase);
    }
}
