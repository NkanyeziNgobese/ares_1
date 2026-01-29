using UnityEngine;

[DisallowMultipleComponent]
public class UiFreshnessFader : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TelemetryManager telemetryManager;

    [Header("Alphas")]
    [Range(0f, 1f)][SerializeField] private float liveAlpha = 1.0f;
    [Range(0f, 1f)][SerializeField] private float staleAlpha = 0.75f;
    [Range(0f, 1f)][SerializeField] private float disconnectedAlpha = 0.45f;

    [Header("Smoothing")]
    [SerializeField] private float fadeSpeed = 6f;

    private void Reset()
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (!telemetryManager) telemetryManager = FindFirstObjectByType<TelemetryManager>();
    }

    private void OnValidate()
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (!telemetryManager) telemetryManager = FindFirstObjectByType<TelemetryManager>();
        if (fadeSpeed < 0f) fadeSpeed = 0f;
    }

    private void Update()
    {
        if (!canvasGroup || !telemetryManager) return;

        float target =
            telemetryManager.IsDisconnected ? disconnectedAlpha :
            telemetryManager.IsStale ? staleAlpha :
                                             liveAlpha;

        if (fadeSpeed <= 0f)
        {
            canvasGroup.alpha = target;
        }
        else
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, target, Time.deltaTime * fadeSpeed);
        }
    }
}
