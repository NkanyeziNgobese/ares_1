using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UiWiringGuard : MonoBehaviour
{
    [Header("Refs (assign on DevTools / DashboardRoot)")]
    [SerializeField] private TelemetryManager telemetryManager;
    [SerializeField] private ScrollPauseController scrollPauseController;
    [SerializeField] private TelemetryHealthIndicator telemetryHealthIndicator;
    [SerializeField] private TelemetryAlarmApplier alarmApplier;
    [SerializeField] private AlarmSoundController alarmSoundController;
    [SerializeField] private DepthScaleController depthScaleController;
    [SerializeField] private StratigraphyScroller stratigraphyScroller;
    [SerializeField] private MissionStateClassifier missionStateClassifier;

    private const string Prefix = "[UiWiringGuard]";
    private static readonly BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private void OnValidate()
    {
        AutoFind();
        Validate(false);
    }

    [ContextMenu("Validate Wiring Now")]
    private void ValidateNow()
    {
        AutoFind();
        Validate(true);
    }

    private void AutoFind()
    {
        if (!telemetryManager) telemetryManager = FindFirstObjectByType<TelemetryManager>();
        if (!scrollPauseController) scrollPauseController = FindFirstObjectByType<ScrollPauseController>();
        if (!alarmApplier) alarmApplier = FindFirstObjectByType<TelemetryAlarmApplier>();
    }

    private void Validate(bool verbose)
    {
        bool ok = true;

        if (!telemetryManager)
        {
            Warn("Missing TelemetryManager reference.");
            ok = false;
        }
        else
        {
            ok &= ValidateTelemetryManager();
        }

        if (!scrollPauseController)
        {
            Warn("Missing ScrollPauseController reference.");
            ok = false;
        }
        else
        {
            ok &= ValidateScrollPauseController();
        }

        if (telemetryHealthIndicator)
        {
            ok &= ValidateTelemetryHealthIndicator();
        }

        if (!alarmApplier)
        {
            Warn("Missing TelemetryAlarmApplier reference.");
            ok = false;
        }
        else
        {
            ok &= ValidateAlarmApplier();
        }

        if (alarmSoundController)
        {
            ok &= ValidateAlarmSoundController();
        }

        if (depthScaleController)
        {
            ok &= ValidateDepthScaleController();
        }

        if (stratigraphyScroller)
        {
            ok &= ValidateStratigraphyScroller();
        }

        if (missionStateClassifier)
        {
            ok &= ValidateMissionStateClassifier();
        }

        if (verbose && ok)
            Debug.Log($"{Prefix} Wiring looks OK.", this);
    }

    private bool ValidateTelemetryManager()
    {
        bool ok = true;
        var fields = telemetryManager.GetType().GetFields(FieldFlags);
        for (int i = 0; i < fields.Length; i++)
        {
            var f = fields[i];
            if (!IsSerializedField(f)) continue;
            if (!typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType)) continue;
            if (IsTelemetryManagerOptionalField(f.Name)) continue;

            var obj = f.GetValue(telemetryManager) as UnityEngine.Object;
            if (!obj)
            {
                Warn($"TelemetryManager missing ref: {f.Name}");
                ok = false;
            }
        }
        return ok;
    }

    private bool ValidateScrollPauseController()
    {
        bool ok = true;
        bool dimWhenPaused = GetBoolField(scrollPauseController, "dimWhenPaused");
        CanvasGroup[] dimTargets = GetFieldValue<CanvasGroup[]>(scrollPauseController, "dimTargets");
        TMP_Text pauseButtonLabel = GetFieldValue<TMP_Text>(scrollPauseController, "pauseButtonLabel");
        GameObject pausedBadge = GetFieldValue<GameObject>(scrollPauseController, "pausedBadge");

        if (dimWhenPaused && !HasAny(dimTargets))
        {
            Warn("ScrollPauseController dimWhenPaused is enabled but dimTargets is empty.");
            ok = false;
        }

        if (!pauseButtonLabel && GameObject.Find("BTN_PauseVisuals"))
        {
            Warn("ScrollPauseController pauseButtonLabel is not wired (BTN_PauseVisuals exists).");
            ok = false;
        }

        if (!pausedBadge && GameObject.Find("Pill_PausedBadge"))
        {
            Warn("ScrollPauseController pausedBadge is not wired (Pill_PausedBadge exists).");
            ok = false;
        }

        return ok;
    }

    private bool ValidateTelemetryHealthIndicator()
    {
        bool ok = true;
        var tm = GetFieldValue<TelemetryManager>(telemetryHealthIndicator, "telemetryManager");
        var pill = GetFieldValue<Image>(telemetryHealthIndicator, "pillBackground");
        var dot = GetFieldValue<Image>(telemetryHealthIndicator, "statusDot");
        var text = GetFieldValue<TMP_Text>(telemetryHealthIndicator, "statusText");

        if (!tm) { Warn("TelemetryHealthIndicator missing telemetryManager."); ok = false; }
        if (!pill) { Warn("TelemetryHealthIndicator missing pillBackground."); ok = false; }
        if (!dot) { Warn("TelemetryHealthIndicator missing statusDot."); ok = false; }
        if (!text) { Warn("TelemetryHealthIndicator missing statusText."); ok = false; }
        return ok;
    }

    private bool ValidateAlarmApplier()
    {
        bool ok = true;
        var tm = GetFieldValue<TelemetryManager>(alarmApplier, "telemetryManager");
        var targets = GetFieldValue<TelemetryAlarmApplier.MetricTarget[]>(alarmApplier, "targets");

        if (!tm) { Warn("TelemetryAlarmApplier missing telemetryManager."); ok = false; }

        if (targets == null || targets.Length == 0)
        {
            Warn("TelemetryAlarmApplier targets array is empty.");
            return false;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            var t = targets[i];
            if (t == null)
            {
                Warn($"TelemetryAlarmApplier targets[{i}] is null.");
                ok = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(t.key))
            {
                Warn($"TelemetryAlarmApplier targets[{i}] has empty key.");
                ok = false;
            }

            if (!t.valueText && !t.ring)
            {
                Warn($"TelemetryAlarmApplier targets[{i}] has no valueText or ring.");
                ok = false;
            }

            if (t.rule == null)
            {
                Warn($"TelemetryAlarmApplier targets[{i}] has no ThresholdRule.");
                ok = false;
            }
        }

        return ok;
    }

    private bool ValidateAlarmSoundController()
    {
        bool ok = true;
        var applier = GetFieldValue<TelemetryAlarmApplier>(alarmSoundController, "alarmApplier");
        var audio = GetFieldValue<AudioSource>(alarmSoundController, "audioSource");

        if (!applier) { Warn("AlarmSoundController missing alarmApplier."); ok = false; }
        if (!audio) { Warn("AlarmSoundController missing audioSource."); return false; }

        if (!audio.clip) { Warn("AlarmSoundController audioSource.clip is not assigned."); ok = false; }
        if (audio.spatialBlend > 0.01f) { Warn("AlarmSoundController audioSource.spatialBlend should be 0 for UI."); ok = false; }
        if (audio.playOnAwake) { Warn("AlarmSoundController audioSource.playOnAwake should be false."); ok = false; }
        return ok;
    }

    private bool ValidateDepthScaleController()
    {
        bool ok = true;
        var tick = GetFieldValue<DepthTickView>(depthScaleController, "tickPrefab");
        var scaleRoot = GetFieldValue<RectTransform>(depthScaleController, "scaleRoot");
        var viewport = GetFieldValue<RectTransform>(depthScaleController, "viewport");

        if (!tick) { Warn("DepthScaleController missing tickPrefab."); ok = false; }
        if (!scaleRoot) { Warn("DepthScaleController missing scaleRoot."); ok = false; }
        if (!viewport) { Warn("DepthScaleController missing viewport."); ok = false; }
        else if (viewport.rect.height <= 1f) { Warn("DepthScaleController viewport height is ~0; layout may not be built."); }

        return ok;
    }

    private bool ValidateStratigraphyScroller()
    {
        bool ok = true;
        var rawImage = GetFieldValue<RawImage>(stratigraphyScroller, "rawImage");
        if (!rawImage) { Warn("StratigraphyScroller missing RawImage reference."); return false; }
        if (!rawImage.texture) { Warn("StratigraphyScroller RawImage has no texture assigned."); ok = false; }
        return ok;
    }

    private bool ValidateMissionStateClassifier()
    {
        bool ok = true;
        var tm = GetFieldValue<TelemetryManager>(missionStateClassifier, "telemetryManager");
        var stateText = GetFieldValue<TMP_Text>(missionStateClassifier, "stateText");
        var stateDot = GetFieldValue<Image>(missionStateClassifier, "stateDot");

        if (!tm) { Warn("MissionStateClassifier missing telemetryManager."); ok = false; }
        if (!stateText) { Warn("MissionStateClassifier missing stateText."); ok = false; }
        if (!stateDot) { Warn("MissionStateClassifier missing stateDot."); ok = false; }
        return ok;
    }

    private static bool IsSerializedField(FieldInfo f)
    {
        return f.IsPublic || f.GetCustomAttribute<SerializeField>() != null;
    }

    private static bool IsTelemetryManagerOptionalField(string name)
    {
        return name.IndexOf("debug", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static T GetFieldValue<T>(object obj, string fieldName) where T : class
    {
        if (obj == null) return null;
        var f = obj.GetType().GetField(fieldName, FieldFlags);
        if (f == null) return null;
        return f.GetValue(obj) as T;
    }

    private static T GetFieldValueStruct<T>(object obj, string fieldName) where T : struct
    {
        if (obj == null) return default;
        var f = obj.GetType().GetField(fieldName, FieldFlags);
        if (f == null) return default;
        return (T)f.GetValue(obj);
    }

    private static bool GetBoolField(object obj, string fieldName) => GetFieldValueStruct<bool>(obj, fieldName);

    private static bool HasAny(UnityEngine.Object[] arr)
    {
        if (arr == null || arr.Length == 0) return false;
        for (int i = 0; i < arr.Length; i++)
            if (arr[i]) return true;
        return false;
    }

    private void Warn(string msg)
    {
        Debug.LogWarning($"{Prefix} {msg}", this);
    }
}
