using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TelemetryFillGauge : MonoBehaviour
{
    public enum Level { Safe = 0, Warning = 1, Danger = 2 }

    [SerializeField] private Image fillImage;

    [Tooltip("Value corresponding to fillAmount = 1.0")]
    [SerializeField] private float maxValue = 60f;

    [Header("Smoothing")]
    [SerializeField] private float lerpSpeed = 10f;

    [Header("Thresholds (0-1)")]
    [SerializeField] private float warningAt = 0.70f;
    [SerializeField] private float dangerAt = 0.90f;

    [Header("Colors")]
    [SerializeField] private Color32 safeColor = new Color32(0x2B, 0xA3, 0xFF, 0xFF);
    [SerializeField] private Color32 warningColor = new Color32(0xFF, 0xB0, 0x00, 0xFF);
    [SerializeField] private Color32 dangerColor = new Color32(0xFF, 0x3B, 0x30, 0xFF);

    public Level CurrentLevel { get; private set; } = Level.Safe;

    private float targetFill;
    private bool alertOverrideEnabled;
    private bool alertOverrideDanger;

    public void SetMax(float max) => maxValue = Mathf.Max(0.0001f, max);

    public void SetValue(float value)
    {
        if (!fillImage) return;

        targetFill = Mathf.Clamp01(value / maxValue);
        CurrentLevel = EvaluateLevel(targetFill);

        if (alertOverrideEnabled)
            CurrentLevel = alertOverrideDanger ? Level.Danger : Level.Warning;

        ApplyColorFromState();

        if (lerpSpeed <= 0f)
            fillImage.fillAmount = targetFill;
    }

    public void SetAlertOverride(bool enabled, bool danger)
    {
        alertOverrideEnabled = enabled;
        alertOverrideDanger = danger;

        if (enabled)
        {
            CurrentLevel = danger ? Level.Danger : Level.Warning;
        }
        else
        {
            CurrentLevel = EvaluateLevel(targetFill);
        }

        ApplyColorFromState();
    }

    private void Update()
    {
        if (!fillImage) return;
        if (lerpSpeed <= 0f) return;

        var current = fillImage.fillAmount;
        if (Mathf.Approximately(current, targetFill)) return;

        fillImage.fillAmount = Mathf.Lerp(current, targetFill, Time.deltaTime * lerpSpeed);
    }

    private Level EvaluateLevel(float fillFraction)
    {
        if (fillFraction >= dangerAt) return Level.Danger;
        if (fillFraction >= warningAt) return Level.Warning;
        return Level.Safe;
    }

    private void ApplyColorFromState()
    {
        if (!fillImage) return;

        if (alertOverrideEnabled)
        {
            fillImage.color = alertOverrideDanger ? dangerColor : warningColor;
            return;
        }

        fillImage.color = CurrentLevel switch
        {
            Level.Danger => dangerColor,
            Level.Warning => warningColor,
            _ => safeColor
        };
    }

    private void Reset()
    {
        if (!fillImage) fillImage = GetComponent<Image>();
        targetFill = fillImage ? Mathf.Clamp01(fillImage.fillAmount) : 0f;
        CurrentLevel = EvaluateLevel(targetFill);
    }

    private void OnValidate()
    {
        if (!fillImage) fillImage = GetComponent<Image>();
        if (maxValue <= 0f) maxValue = 60f;

        warningAt = Mathf.Clamp01(warningAt);
        dangerAt = Mathf.Clamp01(dangerAt);
        if (dangerAt < warningAt) dangerAt = warningAt;

        if (fillImage) targetFill = Mathf.Clamp01(fillImage.fillAmount);
        CurrentLevel = alertOverrideEnabled
            ? (alertOverrideDanger ? Level.Danger : Level.Warning)
            : EvaluateLevel(targetFill);
    }
}
