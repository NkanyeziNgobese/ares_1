using UnityEngine;

[DisallowMultipleComponent]
public class SteelCableSpan : MonoBehaviour
{
    [Header("Bindings")]
    [SerializeField] private RigBindings rigBindings;
    [SerializeField] private Transform crownCableAnchor;
    [SerializeField] private Transform travelCableAnchor;
    [SerializeField] private Transform steelCablesRoot; // parent containing Cable_01..Cable_08

    [Header("Cable Layout")]
    [SerializeField] private int cableCount = 8;
    [SerializeField] private float cableRadius = 0.02f; // sets X/Z scale
    [SerializeField] private float lateralSpacing = 0.25f; // offsets to spread the 8 cables
    [SerializeField] private Vector3 lateralAxisLocal = Vector3.right; // axis in anchor local space

    [Header("Runtime")]
    [SerializeField] private bool drawDebug = false;
    [SerializeField] private bool freeze = false;

    private Transform[] _cables = new Transform[0];
    private bool _cacheInitialized;
    private bool _warnedMissingCableSource;
    private bool _warnedInsufficientBindingsCables;
    private bool _warnedInsufficientRootCables;

    private const string LogPrefix = "[SteelCableSpan]";

    private void Awake()
    {
        EnsureAnchorsFromBindings();
        RebuildCableCache();
    }

    private void Reset()
    {
        if (!rigBindings) rigBindings = FindFirstObjectByType<RigBindings>();
    }

    private void OnValidate()
    {
        if (cableCount < 1) cableCount = 1;
        if (cableRadius <= 0f) cableRadius = 0.001f;

        EnsureAnchorsFromBindings();
        RebuildCableCache();
    }

    private void LateUpdate()
    {
        if (freeze) return;

        EnsureAnchorsFromBindings();
        if (!_cacheInitialized) RebuildCableCache();

        if (!crownCableAnchor || !travelCableAnchor || _cables == null || _cables.Length == 0)
            return;

        Vector3 axisWorld = crownCableAnchor.TransformDirection(lateralAxisLocal);
        if (axisWorld.sqrMagnitude < 0.000001f)
            axisWorld = crownCableAnchor.right;
        axisWorld.Normalize();

        float center = (cableCount - 1) * 0.5f;
        int usable = Mathf.Min(cableCount, _cables.Length);

        for (int i = 0; i < usable; i++)
        {
            Transform cable = _cables[i];
            if (!cable) continue;

            float lane = (i - center) * lateralSpacing;
            Vector3 offset = lane * axisWorld;

            Vector3 a = crownCableAnchor.position + offset;
            Vector3 b = travelCableAnchor.position + offset;

            ApplyCableSpan(cable, a, b);

            if (drawDebug)
                Debug.DrawLine(a, b, Color.cyan);
        }
    }

    [ContextMenu("Validate Now")]
    public void ValidateNow()
    {
        EnsureAnchorsFromBindings();
        RebuildCableCache();

        bool ok = true;
        if (!rigBindings)
        {
            Debug.LogWarning($"{LogPrefix} rigBindings is not assigned. Assign RigBindings for preferred cable sourcing.", this);
        }

        if (!crownCableAnchor)
        {
            Debug.LogWarning($"{LogPrefix} Missing crownCableAnchor. Assign explicitly or provide RigBindings.CrownBlock.", this);
            ok = false;
        }

        if (!travelCableAnchor)
        {
            Debug.LogWarning($"{LogPrefix} Missing travelCableAnchor. Assign explicitly or provide RigBindings.TravelingBlock.", this);
            ok = false;
        }

        if (_cables == null || _cables.Length < 1)
        {
            Debug.LogWarning($"{LogPrefix} No usable cable references resolved. Assign RigBindings.SteelCables or steelCablesRoot children.", this);
            ok = false;
        }
        else if (_cables.Length < cableCount)
        {
            Debug.LogWarning($"{LogPrefix} Using {_cables.Length} cable(s) but cableCount is {cableCount}.", this);
        }

        if (ok)
            Debug.Log($"{LogPrefix} ValidateNow complete. Ready for span updates.", this);
    }

    public void RebuildCache()
    {
        RebuildCableCache();
    }

    private void EnsureAnchorsFromBindings()
    {
        if (!rigBindings) return;

        if (!crownCableAnchor) crownCableAnchor = rigBindings.CrownBlock;
        if (!travelCableAnchor) travelCableAnchor = rigBindings.TravelingBlock;
    }

    // A Unity cylinder's default height is 2 along local Y, so scale.y = length / 2.
    private void ApplyCableSpan(Transform cable, Vector3 a, Vector3 b)
    {
        Vector3 delta = b - a;
        float length = delta.magnitude;
        if (length < 0.0001f) return;

        Vector3 dir = delta / length;
        cable.position = (a + b) * 0.5f;
        cable.rotation = Quaternion.FromToRotation(Vector3.up, dir);
        cable.localScale = new Vector3(cableRadius, length * 0.5f, cableRadius);
    }

    private void RebuildCableCache()
    {
        _cacheInitialized = true;

        // Priority A: use cables explicitly bound in RigBindings.
        var boundCables = rigBindings ? rigBindings.SteelCables : null;
        if (boundCables != null && boundCables.Count > 0)
        {
            int take = Mathf.Min(cableCount, boundCables.Count);
            Transform[] next = new Transform[take];

            for (int i = 0; i < take; i++)
                next[i] = boundCables[i];

            _cables = next;

            if (boundCables.Count < cableCount && !_warnedInsufficientBindingsCables)
            {
                Debug.LogWarning($"{LogPrefix} RigBindings provides {boundCables.Count} steel cable(s), expected {cableCount}. Using available cables.", this);
                _warnedInsufficientBindingsCables = true;
            }

            return;
        }

        // Priority B: fallback to direct children under steelCablesRoot.
        if (steelCablesRoot)
        {
            int childCount = steelCablesRoot.childCount;
            int take = Mathf.Min(cableCount, childCount);
            Transform[] next = new Transform[take];

            for (int i = 0; i < take; i++)
                next[i] = steelCablesRoot.GetChild(i);

            _cables = next;

            if (childCount < cableCount && !_warnedInsufficientRootCables)
            {
                Debug.LogWarning($"{LogPrefix} steelCablesRoot has {childCount} child cable(s), expected {cableCount}. Using available cables.", this);
                _warnedInsufficientRootCables = true;
            }

            return;
        }

        // Priority C: no source available.
        _cables = new Transform[0];
        if (!_warnedMissingCableSource)
        {
            Debug.LogWarning($"{LogPrefix} No cable source found. Assign RigBindings.SteelCables or steelCablesRoot.", this);
            _warnedMissingCableSource = true;
        }
    }
}
