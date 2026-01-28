using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TelemetryFillGauge : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    [Tooltip("Value corresponding to fillAmount = 1.0")]
    [SerializeField] private float maxValue = 60f;

    public void SetMax(float max) => maxValue = Mathf.Max(0.0001f, max);

    public void SetValue(float value)
    {
        if (!fillImage) return;
        fillImage.fillAmount = Mathf.Clamp01(value / maxValue);
    }

    private void Reset()
    {
        if (!fillImage) fillImage = GetComponent<Image>();
    }

    private void OnValidate()
    {
        if (!fillImage) fillImage = GetComponent<Image>();
        if (maxValue <= 0f) maxValue = 60f;
    }
}
