using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TelemetryFillGauge : MonoBehaviour
{
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

    private float targetFill;
    private bool alertOverride;
    private bool alertDanger;

    public void SetMax(float max) => maxValue = Mathf.Max(0.0001f, max);

    public void SetValue(float value)
    {
        if (!fillImage) return;
        targetFill = Mathf.Clamp01(value / maxValue);
        ApplyColor(targetFill);

        if (lerpSpeed <= 0f)
            fillImage.fillAmount = targetFill;
    }

    public void SetAlertOverride(bool enabled, bool danger)
    {
        alertOverride = enabled;
        alertDanger = danger;
        if (!fillImage) return;
        ApplyColor(targetFill);
    }

    private void Update()
    {
        if (!fillImage) return;
        if (lerpSpeed <= 0f) return;

        var current = fillImage.fillAmount;
        if (Mathf.Approximately(current, targetFill)) return;

        fillImage.fillAmount = Mathf.Lerp(current, targetFill, Time.deltaTime * lerpSpeed);
    }

    private void ApplyColor(float fillFraction)
    {
        if (!fillImage) return;

        if (alertOverride)
        {
            fillImage.color = alertDanger ? dangerColor : warningColor;
            return;
        }

        if (fillFraction >= dangerAt)
            fillImage.color = dangerColor;
        else if (fillFraction >= warningAt)
            fillImage.color = warningColor;
        else
            fillImage.color = safeColor;
    }

    private void Reset()
    {
        if (!fillImage) fillImage = GetComponent<Image>();
        targetFill = fillImage ? Mathf.Clamp01(fillImage.fillAmount) : 0f;
    }

    private void OnValidate()
    {
        if (!fillImage) fillImage = GetComponent<Image>();
        if (maxValue <= 0f) maxValue = 60f;

        warningAt = Mathf.Clamp01(warningAt);
        dangerAt = Mathf.Clamp01(dangerAt);
        if (dangerAt < warningAt) dangerAt = warningAt;

        if (fillImage) targetFill = Mathf.Clamp01(fillImage.fillAmount);
    }
}
