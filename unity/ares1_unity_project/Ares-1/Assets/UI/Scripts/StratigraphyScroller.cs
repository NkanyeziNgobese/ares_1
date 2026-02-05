using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class StratigraphyScroller : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TelemetryManager telemetryManager;
    [SerializeField] private RawImage rawImage;

    [Header("Scroll Mapping")]
    [Tooltip("How much UV offset per meter of depth. Smaller = slower scroll.")]
    [SerializeField] private float uvPerMeter = 0.0025f;

    [Tooltip("If true, flips direction.")]
    [SerializeField] private bool invertScroll = false;

    [Tooltip("Optional: set the UV Y offset zero at this depth (helps if your depth is negative).")]
    [SerializeField] private float depthZero = 0f;

    [Header("Debug (toggle anytime)")]
    [SerializeField] private bool debugEnabled = false;
    [SerializeField] private TMP_Text debugText;
    [SerializeField] private KeyCode debugToggleKey = KeyCode.F4;

    private void Reset()
    {
        telemetryManager = FindFirstObjectByType<TelemetryManager>();
        rawImage = GetComponent<RawImage>();
    }

    private void OnValidate()
    {
        if (!telemetryManager) telemetryManager = FindFirstObjectByType<TelemetryManager>();
        if (!rawImage) rawImage = GetComponent<RawImage>();
        if (uvPerMeter <= 0f) uvPerMeter = 0.0025f;
    }

    private void Update()
    {
        if (Input.GetKeyDown(debugToggleKey))
        {
            debugEnabled = !debugEnabled;
            if (debugText) debugText.gameObject.SetActive(debugEnabled);
        }

        if (!telemetryManager || !rawImage) return;
<<<<<<< HEAD
        if (ScrollPauseController.IsPaused) return;
=======
>>>>>>> 166b2df92058ed6c3937a4080f723aaaa63f5f19

        float depth = telemetryManager.CurrentDepth;

        // Convention: drilling down (depth increases / becomes "more deep") => geology moves UP.
        // We implement "UP" by decreasing uvRect.y by default (dir = -1).
        float dir = invertScroll ? 1f : -1f;

        // If your depth is negative (e.g., -1200), depthZero lets you set a reference.
        float metersFromZero = depth - depthZero;
        float uvY = metersFromZero * uvPerMeter * dir;

        Rect r = rawImage.uvRect;
        r.y = uvY;
        rawImage.uvRect = r;

        if (debugEnabled && debugText)
        {
            debugText.text =
                $"[Stratigraphy]\n" +
                $"depth={depth:0.0} m\n" +
                $"depthZero={depthZero:0.0}\n" +
                $"uvPerMeter={uvPerMeter:0.000000}\n" +
                $"uvY={uvY:0.000}\n" +
                $"dir={(invertScroll ? "INVERT" : "NORMAL")} (down->up)";
        }
    }
}
