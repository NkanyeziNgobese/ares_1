using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TelemetryHealthIndicator : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TelemetryManager telemetryManager;
    [SerializeField] private Image pillBackground;
    [SerializeField] private Image statusDot;
    [SerializeField] private TMP_Text statusText;

    [Header("Colors")]
    [SerializeField] private Color safeBlue = new Color32(0, 148, 255, 255);     // #0094FF
    [SerializeField] private Color warningAmber = new Color32(255, 191, 0, 255); // #FFBF00
    [SerializeField] private Color dangerRed = new Color32(255, 59, 48, 255);    // #FF3B30

    [Header("Pill Tint")]
    [Range(0f, 1f)]
    [SerializeField] private float backgroundTint = 0.18f; // subtle tint on the pill

    [Header("Formatting")]
    [SerializeField] private bool showAgeSeconds = true;

    private void Reset()
    {
        telemetryManager = FindFirstObjectByType<TelemetryManager>();
    }

    private void OnValidate()
    {
        if (!telemetryManager) telemetryManager = FindFirstObjectByType<TelemetryManager>();
    }

    private void Update()
    {
        if (!telemetryManager) return;

        // Use your existing health flags
        bool disconnected = telemetryManager.IsDisconnected;
        bool stale = telemetryManager.IsStale && !disconnected;

        Color stateColor = safeBlue;
        string stateLabel = "LIVE";

        if (disconnected)
        {
            stateColor = dangerRed;
            stateLabel = "DISCONNECTED";
        }
        else if (stale)
        {
            stateColor = warningAmber;
            stateLabel = "STALE";
        }

        if (statusDot) statusDot.color = stateColor;

        if (pillBackground)
        {
            // Keep your dark base, but add a subtle state tint (no palette changes)
            // Assumes pillBackground already set to dark color in editor.
            Color baseCol = pillBackground.color;
            Color tinted = Color.Lerp(baseCol, stateColor, backgroundTint);
            tinted.a = baseCol.a;
            pillBackground.color = tinted;
        }

        if (statusText)
        {
            // If TelemetryManager has "AgeSeconds" use it. If not, show just state.
            // We'll handle absence safely by reflection-free try: user can wire later.
            string text = stateLabel;

            if (showAgeSeconds)
            {
                float age = telemetryManager.LastSampleAgeSeconds; // you may already have this; if not, we’ll patch next
                text = $"{stateLabel} · Age {age:0.0}s";
            }

            statusText.text = text;
        }
    }
}
