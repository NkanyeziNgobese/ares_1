using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MissionStatusPill : MonoBehaviour
{
    // IMPORTANT:
    // This script assumes TelemetryFillGauge exposes a "current level" like:
    //   public enum Level { Safe, Warning, Danger }
    //   public Level CurrentLevel { get; }
    //
    // If your property/enum names differ, only update the GetLevel(...) method.

    public enum HmiLevel { Safe = 0, Warning = 1, Danger = 2 }

    [Header("Pill UI")]
    [SerializeField] private Image pillBackground;
    [SerializeField] private TMP_Text pillText;

    [Header("Gauges to watch")]
    [SerializeField] private List<TelemetryFillGauge> gauges = new List<TelemetryFillGauge>();

    [Header("Colors")]
    [SerializeField] private Color safeColor = new Color32(0x2B, 0xA3, 0xFF, 0xFF); // #2BA3FF
    [SerializeField] private Color warningColor = new Color32(0xFF, 0xB0, 0x00, 0xFF); // #FFB000
    [SerializeField] private Color dangerColor = new Color32(0xFF, 0x3B, 0x30, 0xFF); // #FF3B30

    private void Reset()
    {
        if (!pillBackground) pillBackground = GetComponent<Image>();
        if (!pillText) pillText = GetComponentInChildren<TMP_Text>(true);
    }

    private void OnValidate()
    {
        if (!pillBackground) pillBackground = GetComponent<Image>();
        if (!pillText) pillText = GetComponentInChildren<TMP_Text>(true);
    }

    private void Update()
    {
        var level = ComputeWorstLevel();

        if (pillBackground)
            pillBackground.color = level switch
            {
                HmiLevel.Danger => dangerColor,
                HmiLevel.Warning => warningColor,
                _ => safeColor
            };

        if (pillText)
            pillText.text = level switch
            {
                HmiLevel.Danger => "LIVE — DANGER",
                HmiLevel.Warning => "LIVE — WARNING",
                _ => "LIVE — SAFE"
            };
    }

    private HmiLevel ComputeWorstLevel()
    {
        HmiLevel worst = HmiLevel.Safe;

        for (int i = 0; i < gauges.Count; i++)
        {
            var g = gauges[i];
            if (!g) continue;

            var lvl = GetLevel(g);
            if (lvl > worst) worst = lvl;

            if (worst == HmiLevel.Danger) break; // early exit
        }

        return worst;
    }

    // 🔧 ADAPTER: If your TelemetryFillGauge uses different names, change ONLY this method.
    private static HmiLevel GetLevel(TelemetryFillGauge gauge)
    {
        // Expected: gauge.CurrentLevel returns Safe/Warning/Danger
        // If yours is gauge.CurrentSeverity or gauge.Level, change accordingly.

        // Example expected enum mapping:
        // return gauge.CurrentLevel switch { TelemetryFillGauge.Level.Danger => HmiLevel.Danger, ... };

        // --- Default implementation assumes:
        // TelemetryFillGauge has:
        //   public Level CurrentLevel { get; }
        // where Level has Safe/Warning/Danger
        return gauge.CurrentLevel switch
        {
            TelemetryFillGauge.Level.Danger => HmiLevel.Danger,
            TelemetryFillGauge.Level.Warning => HmiLevel.Warning,
            _ => HmiLevel.Safe
        };
    }
}
