using System;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class CtrlRigBridgeFollower : MonoBehaviour
{
    // This bridge exists because prefab children cannot be re-parented under scene-root CTRL objects.
    // We follow external CTRL transforms and apply motion onto rig prefab transforms each frame.

    [Header("Sources (CTRL chain in scene root)")]
    public Transform ctrlTravellingBlock;
    public Transform ctrlSwivel;
    public Transform ctrlTopDrive;

    [Header("Targets (Prefab rig transforms)")]
    public Transform rigTravellingBlock;
    public Transform rigSwivel;
    public Transform rigTopDrive;
    public Transform rigDollyCarriage;

    [Header("Follow Settings")]
    public bool followWorldYOnly = true;
    public bool followRotation = false;
    public bool followX = false;
    public bool followZ = false;
    public bool enableDollyCarriageFollow = false;

    [Header("Pause Settings")]
    public bool respectPause = true;
    public MonoBehaviour pauseProvider;
    public string pauseBoolMemberName = "IsPaused";

    [Header("Debug")]
    public bool enableDebug = true;
    public float debugInterval = 0.25f;

    private Vector3 _travelOffset;
    private Vector3 _swivelOffset;
    private Vector3 _topDriveOffset;
    private Vector3 _dollyOffset;
    private bool _offsetsBound;
    private float _nextDebugTime;
    private bool _warnedMissingRefs;

    private Component _cachedFallbackPauseProvider;
    private float _nextPauseProbeTime;

    private const string LogPrefix = "[CtrlRigBridgeFollower]";
    private const float PauseProbeIntervalSeconds = 1f;
    private static readonly BindingFlags MemberFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private void Awake()
    {
        TryBindOffsets(false, false);
    }

    private void Start()
    {
        if (!_offsetsBound)
            TryBindOffsets(false, false);
    }

    private void OnValidate()
    {
        if (debugInterval < 0.05f)
            debugInterval = 0.05f;

        if (string.IsNullOrWhiteSpace(pauseBoolMemberName))
            pauseBoolMemberName = "IsPaused";

        ValidateReferences(logWarnings: true, warnOnce: false);
    }

    private void LateUpdate()
    {
        // LateUpdate keeps this bridge after hoist/controller updates, reducing jitter.
        if (!ValidateReferences(logWarnings: false, warnOnce: true))
            return;

        if (!_offsetsBound && !TryBindOffsets(logOnFail: true, warnOnce: true))
            return;

        bool paused = respectPause && IsPaused();
        if (!paused)
        {
            ApplyFollow(ctrlTravellingBlock, rigTravellingBlock, _travelOffset);
            ApplyFollow(ctrlSwivel, rigSwivel, _swivelOffset);
            ApplyFollow(ctrlTopDrive, rigTopDrive, _topDriveOffset);

            if (enableDollyCarriageFollow && rigDollyCarriage && ctrlTopDrive)
                ApplyFollow(ctrlTopDrive, rigDollyCarriage, _dollyOffset);
        }

        if (enableDebug)
            TryLogDebugLine();
    }

    [ContextMenu("ValidateNow")]
    public void ValidateNow()
    {
        bool ok = ValidateReferences(logWarnings: true, warnOnce: false);
        Debug.Log(ok
            ? $"{LogPrefix} ValidateNow OK."
            : $"{LogPrefix} ValidateNow found missing required references.",
            this);
    }

    [ContextMenu("RebindOffsetsNow")]
    public void RebindOffsetsNow()
    {
        if (TryBindOffsets(logOnFail: true, warnOnce: false))
            Debug.Log($"{LogPrefix} Offsets rebound from current pose.", this);
    }

    [ContextMenu("PrintState")]
    public void PrintState()
    {
        float ctrlY = ctrlTravellingBlock ? ctrlTravellingBlock.position.y : float.NaN;
        float rigY = rigTravellingBlock ? rigTravellingBlock.position.y : float.NaN;

        Debug.Log(
            $"{LogPrefix} offsetsBound={_offsetsBound}, respectPause={respectPause}, " +
            $"followWorldYOnly={followWorldYOnly}, followRotation={followRotation}, " +
            $"ctrlY={ctrlY:0.000}, rigY={rigY:0.000}, travelOffsetY={_travelOffset.y:0.000}",
            this);
    }

    private bool TryBindOffsets(bool logOnFail, bool warnOnce)
    {
        if (!ValidateReferences(logWarnings: logOnFail, warnOnce: warnOnce))
        {
            _offsetsBound = false;
            return false;
        }

        // Capture offsets once so current scene pose is preserved and no snapping occurs.
        _travelOffset = rigTravellingBlock.position - ctrlTravellingBlock.position;
        _swivelOffset = rigSwivel.position - ctrlSwivel.position;
        _topDriveOffset = rigTopDrive.position - ctrlTopDrive.position;

        if (enableDollyCarriageFollow && rigDollyCarriage && ctrlTopDrive)
            _dollyOffset = rigDollyCarriage.position - ctrlTopDrive.position;
        else
            _dollyOffset = Vector3.zero;

        _offsetsBound = true;
        return true;
    }

    private void ApplyFollow(Transform source, Transform target, Vector3 offset)
    {
        if (!source || !target)
            return;

        if (followWorldYOnly)
        {
            target.position = new Vector3(
                target.position.x,
                source.position.y + offset.y,
                target.position.z);
        }
        else
        {
            float x = followX ? source.position.x + offset.x : target.position.x;
            float y = source.position.y + offset.y;
            float z = followZ ? source.position.z + offset.z : target.position.z;
            target.position = new Vector3(x, y, z);
        }

        if (followRotation)
            target.rotation = source.rotation;
    }

    private bool IsPaused()
    {
        if (pauseProvider)
        {
            if (TryReadPauseBool(pauseProvider, out bool pausedFromProvider))
                return pausedFromProvider;
        }
        else
        {
            // Fast path in this project.
            if (ScrollPauseController.IsPaused)
                return true;

            Component fallback = ResolveFallbackPauseProvider();
            if (fallback && TryReadPauseBool(fallback, out bool pausedFromFallback))
                return pausedFromFallback;
        }

        return false;
    }

    private bool TryReadPauseBool(object source, out bool value)
    {
        value = false;
        if (source == null) return false;

        if (TryReadBool(source, pauseBoolMemberName, out value)) return true;
        if (TryReadBool(source, "IsPaused", out value)) return true;
        if (TryReadBool(source, "Paused", out value)) return true;

        return false;
    }

    private static bool TryReadBool(object source, string memberName, out bool value)
    {
        value = false;
        if (source == null || string.IsNullOrEmpty(memberName))
            return false;

        Type type = source.GetType();
        try
        {
            PropertyInfo property = type.GetProperty(memberName, MemberFlags);
            if (property != null && property.PropertyType == typeof(bool))
            {
                value = (bool)property.GetValue(source);
                return true;
            }

            FieldInfo field = type.GetField(memberName, MemberFlags);
            if (field != null && field.FieldType == typeof(bool))
            {
                value = (bool)field.GetValue(source);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private Component ResolveFallbackPauseProvider()
    {
        if (_cachedFallbackPauseProvider)
            return _cachedFallbackPauseProvider;

        if (Time.unscaledTime < _nextPauseProbeTime)
            return null;

        _nextPauseProbeTime = Time.unscaledTime + PauseProbeIntervalSeconds;

        Component candidate = FindPauseProviderByTypeName("DevTools");
        if (!candidate)
            candidate = FindPauseProviderByTypeName("ScrollPauseController");

        _cachedFallbackPauseProvider = candidate;
        return _cachedFallbackPauseProvider;
    }

    private Component FindPauseProviderByTypeName(string typeName)
    {
        Type t = ResolveType(typeName);
        if (t == null) return null;

        UnityEngine.Object found = FindFirstObjectByType(t);
        return found as Component;
    }

    private static Type ResolveType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        Type resolved = Type.GetType(typeName);
        if (resolved != null) return resolved;

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            resolved = assemblies[i].GetType(typeName);
            if (resolved != null) return resolved;
        }

        for (int i = 0; i < assemblies.Length; i++)
        {
            Type[] types;
            try
            {
                types = assemblies[i].GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            if (types == null) continue;

            for (int j = 0; j < types.Length; j++)
            {
                Type t = types[j];
                if (t == null) continue;
                if (string.Equals(t.Name, typeName, StringComparison.Ordinal))
                    return t;
            }
        }

        return null;
    }

    private bool ValidateReferences(bool logWarnings, bool warnOnce)
    {
        bool valid = true;
        bool shouldLog = logWarnings || !warnOnce || !_warnedMissingRefs;

        if (!ctrlTravellingBlock)
        {
            valid = false;
            if (shouldLog) Debug.LogWarning($"{LogPrefix} Missing ctrlTravellingBlock.", this);
        }
        if (!ctrlSwivel)
        {
            valid = false;
            if (shouldLog) Debug.LogWarning($"{LogPrefix} Missing ctrlSwivel.", this);
        }
        if (!ctrlTopDrive)
        {
            valid = false;
            if (shouldLog) Debug.LogWarning($"{LogPrefix} Missing ctrlTopDrive.", this);
        }
        if (!rigTravellingBlock)
        {
            valid = false;
            if (shouldLog) Debug.LogWarning($"{LogPrefix} Missing rigTravellingBlock.", this);
        }
        if (!rigSwivel)
        {
            valid = false;
            if (shouldLog) Debug.LogWarning($"{LogPrefix} Missing rigSwivel.", this);
        }
        if (!rigTopDrive)
        {
            valid = false;
            if (shouldLog) Debug.LogWarning($"{LogPrefix} Missing rigTopDrive.", this);
        }

        if (enableDollyCarriageFollow && !rigDollyCarriage && shouldLog)
            Debug.LogWarning($"{LogPrefix} rigDollyCarriage is optional but missing while enableDollyCarriageFollow is true.", this);

        _warnedMissingRefs = !valid;
        if (!valid) _offsetsBound = false;
        return valid;
    }

    private void TryLogDebugLine()
    {
        if (Time.unscaledTime < _nextDebugTime)
            return;

        if (!ctrlTravellingBlock || !rigTravellingBlock)
            return;

        float ctrlY = ctrlTravellingBlock.position.y;
        float rigY = rigTravellingBlock.position.y;
        float dy = rigY - ctrlY;

        Debug.Log(string.Format(
            "{0} CTRL_Y={1:F3} RIG_Y={2:F3} DY={3:F3}",
            LogPrefix, ctrlY, rigY, dy), this);

        _nextDebugTime = Time.unscaledTime + debugInterval;
    }
}
