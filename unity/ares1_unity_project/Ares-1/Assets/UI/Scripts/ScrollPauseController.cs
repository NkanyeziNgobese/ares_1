using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ScrollPauseController : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Header("Refs")]
    [SerializeField] private TelemetryManager telemetryManager;

    [Header("Controls")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F7;
    [SerializeField] private bool autoPauseWhenDisconnected = true;

    [Header("Debug (optional)")]
    [SerializeField] private bool debugEnabled = false;
    [SerializeField] private TMP_Text debugText;
    [SerializeField] private KeyCode debugToggleKey = KeyCode.F8;

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
        // Toggle pause manually
        if (Input.GetKeyDown(toggleKey))
        {
            IsPaused = !IsPaused;
            Debug.Log($"[ScrollPauseController] IsPaused={IsPaused} (F7)");
        }

        // Toggle debug overlay
        if (Input.GetKeyDown(debugToggleKey))
        {
            debugEnabled = !debugEnabled;
            if (debugText) debugText.gameObject.SetActive(debugEnabled);
        }

        // Auto pause when disconnected (doesn't auto-unpause; you decide)
        if (autoPauseWhenDisconnected && telemetryManager != null && telemetryManager.IsDisconnected)
        {
            IsPaused = true;
        }

        if (debugEnabled && debugText)
        {
            string freshness =
                telemetryManager == null ? "NO TM" :
                telemetryManager.IsDisconnected ? "DISCONNECTED" :
                telemetryManager.IsStale ? "STALE" : "LIVE";

            debugText.text =
                $"[Scroll]\n" +
                $"Paused: {IsPaused}\n" +
                $"AutoPauseOnDisc: {autoPauseWhenDisconnected}\n" +
                $"Freshness: {freshness}\n" +
                $"F7 Toggle Pause\n" +
                $"F8 Toggle This HUD";
        }
    }
}
