using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ScrollPauseController : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [Header("Refs")]
    [SerializeField] private TelemetryManager telemetryManager;
    [SerializeField] private TMP_Text pauseButtonLabel;
    [SerializeField] private GameObject pausedBadge;

    [Header("Controls")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F7;
    [SerializeField] private bool autoPauseWhenDisconnected = true;

    [Header("Visual Feedback (optional)")]
    [SerializeField] private bool dimWhenPaused = true;
    [SerializeField, Range(0.05f, 1f)] private float pausedAlpha = 0.6f;

    [Tooltip("CanvasGroups to dim when visuals are paused (e.g., DepthScaleViewport, StratigraphyViewport).")]
    [SerializeField] private CanvasGroup[] dimTargets;

    [Header("Debug (optional)")]
    [SerializeField] private bool debugEnabled = false;
    [SerializeField] private TMP_Text debugText;
    [SerializeField] private KeyCode debugToggleKey = KeyCode.F8;

    private bool _prevPaused;

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
            ApplyUIState();
            Debug.Log($"[ScrollPauseController] IsPaused={IsPaused} (F7)");
        }

        // Toggle debug overlay
        if (Input.GetKeyDown(debugToggleKey))
        {
            debugEnabled = !debugEnabled;
            if (debugText) debugText.gameObject.SetActive(debugEnabled);
        }

        // Auto pause when disconnected (doesn't auto-unpause; you decide)
        if (autoPauseWhenDisconnected && telemetryManager != null && telemetryManager.IsDisconnected && !IsPaused)
        {
            IsPaused = true;
            ApplyUIState();
        }

        if (debugEnabled && debugText)
        {
            string freshness =
                telemetryManager == null ? "NO TM" :
                telemetryManager.IsDisconnected ? "DISCONNECTED" :
                telemetryManager.IsStale ? "STALE" : "LIVE";

            string stateLine = IsPaused ? "STATE: VISUALS PAUSED" : "STATE: LIVE VISUALS";

            debugText.text =
                $"[Scroll]\n" +
                $"{stateLine}\n" +
                $"Paused: {IsPaused}\n" +
                $"AutoPauseOnDisc: {autoPauseWhenDisconnected}\n" +
                $"Freshness: {freshness}\n" +
                $"F7 Toggle Pause\n" +
                $"F8 Toggle This HUD";
        }
    }

    private void LateUpdate()
    {
        if (_prevPaused != IsPaused) ApplyUIState();
    }

    private void ApplyDimming()
    {
        if (dimTargets == null) return;

        float a = (IsPaused && dimWhenPaused) ? pausedAlpha : 1f;

        for (int i = 0; i < dimTargets.Length; i++)
        {
            var cg = dimTargets[i];
            if (!cg) continue;
            cg.alpha = a;
        }
    }

    public void TogglePauseVisuals()
    {
        IsPaused = !IsPaused;
        ApplyUIState();
        Debug.Log($"[ScrollPauseController] IsPaused={IsPaused} (UI Button)");
    }

    public void SetPaused(bool paused)
    {
        IsPaused = paused;
        ApplyUIState();
    }

    private void ApplyUIState()
    {
        if (pauseButtonLabel)
            pauseButtonLabel.text = IsPaused ? "RESUME VISUALS" : "PAUSE VISUALS";

        if (pausedBadge)
            pausedBadge.SetActive(IsPaused);

        ApplyDimming();
        _prevPaused = IsPaused;
    }
}
