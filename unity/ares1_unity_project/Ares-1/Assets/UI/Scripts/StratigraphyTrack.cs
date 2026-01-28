using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StratigraphyTrack : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage rawImage;

    [Header("Scroll Tuning")]
    [Tooltip("How much the texture scrolls (UV units) per 1 meter of depth.")]
    [SerializeField] private float uvPerMeter = 0.0015f;

    [Tooltip("Enable if depth increases downward (e.g., -1200m).")]
    [SerializeField] private bool invertDirection = true;

    [Header("Live Value (read-only)")]
    [SerializeField] private float currentDepthMeters;

    public void SetDepth(float depthMeters)
    {
        currentDepthMeters = depthMeters;
    }

    private void Reset()
    {
        // Auto-grab RawImage from children if not assigned
        if (!rawImage) rawImage = GetComponentInChildren<RawImage>(true);
    }

    private void OnValidate()
    {
        if (!rawImage) rawImage = GetComponentInChildren<RawImage>(true);
        if (uvPerMeter <= 0f) uvPerMeter = 0.0015f;
    }

    private void Update()
    {
        if (!rawImage || rawImage.texture == null) return;

        float d = invertDirection ? -currentDepthMeters : currentDepthMeters;

        Rect uv = rawImage.uvRect;
        uv.y = d * uvPerMeter;
        rawImage.uvRect = uv;
    }

    [ContextMenu("DEV: Test Scroll (+100m)")]
    private void Dev_TestScroll()
    {
        currentDepthMeters += 100f;
    }
}
