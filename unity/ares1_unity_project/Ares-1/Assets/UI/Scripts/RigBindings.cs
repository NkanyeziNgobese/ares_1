using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RigBindings : MonoBehaviour
{
    [Header("Rig Root")]
    [SerializeField] private Transform rigRoot;

    [Header("Hoisting System")]
    [SerializeField] private Transform crownBlock;
    [SerializeField] private Transform travelingBlock;
    [SerializeField] private Transform topDrive;
    [SerializeField] private Transform drillString; // optional
    [SerializeField] private Transform wellboreStartMarker; // optional empty at ground level
    [SerializeField] private Transform bitTipMarker;

    [Header("Cables (optional)")]
    [SerializeField] private Transform[] steelCables;

    [Header("Discovery (optional)")]
    [SerializeField] private bool autoFindByName = true;
    [SerializeField] private bool verboseLogs = false;

    [Header("Debug Gizmos")]
    [SerializeField] private bool drawDebugGizmos = false;
    [SerializeField] private float gizmoRadius = 0.25f;

    public bool IsReady => crownBlock && travelingBlock && topDrive && wellboreStartMarker && bitTipMarker;
    public Transform BitTipMarker => bitTipMarker;
    public Transform DrillString => drillString;
    public Transform WellboreStartMarker => wellboreStartMarker;
    public Transform CrownBlock => crownBlock;
    public Transform TravelingBlock => travelingBlock;
    public Transform TopDrive => topDrive;
    public IReadOnlyList<Transform> SteelCables => steelCables;

    private const string LogPrefix = "[RigBindings]";

    private void OnValidate()
    {
        if (autoFindByName)
        {
            AutoFindMissing();
        }

        WarnIfMissingRequired();
    }

    [ContextMenu("Validate Now")]
    public void ValidateNow()
    {
        if (autoFindByName)
        {
            AutoFindMissing();
        }

        WarnIfMissingRequired();

        if (verboseLogs)
        {
            Debug.Log($"{LogPrefix} ValidateNow complete. IsReady={IsReady}", this);
        }
    }

    private void AutoFindMissing()
    {
        Transform searchRoot = rigRoot ? rigRoot : transform;

        if (!rigRoot)
        {
            rigRoot = FindByNameContains(transform, false, "Rig", "Derrick");
            if (!rigRoot && (NameContains(transform.name, "Rig") || NameContains(transform.name, "Derrick")))
            {
                rigRoot = transform;
            }

            if (rigRoot)
            {
                searchRoot = rigRoot;
                if (verboseLogs) Debug.Log($"{LogPrefix} Auto-wired rigRoot: {rigRoot.name}", this);
            }
        }

        if (!crownBlock)
        {
            crownBlock = FindByNameContains(searchRoot, true, "Crown");
            if (crownBlock && verboseLogs) Debug.Log($"{LogPrefix} Auto-wired crownBlock: {crownBlock.name}", this);
        }

        if (!travelingBlock)
        {
            travelingBlock = FindByNameContains(searchRoot, true, "Traveling");
            if (travelingBlock && verboseLogs) Debug.Log($"{LogPrefix} Auto-wired travelingBlock: {travelingBlock.name}", this);
        }

        if (!topDrive)
        {
            topDrive = FindByNameContains(searchRoot, true, "Top", "Drive");
            if (topDrive && verboseLogs) Debug.Log($"{LogPrefix} Auto-wired topDrive: {topDrive.name}", this);
        }

        if (!bitTipMarker)
        {
            bitTipMarker = FindByNameContains(searchRoot, true, "WEL_BitTipMarker", "BitTipMarker", "BitTip");
            if (bitTipMarker && verboseLogs) Debug.Log($"{LogPrefix} Auto-wired bitTipMarker: {bitTipMarker.name}", this);
        }

        if (steelCables == null || steelCables.Length == 0)
        {
            List<Transform> cables = FindAllByNameContains(searchRoot, true, "Steel_Cable", "SteelCable", "Cable", "Cables");
            if (cables.Count > 0)
            {
                steelCables = cables.ToArray();
                if (verboseLogs) Debug.Log($"{LogPrefix} Auto-wired steelCables: {steelCables.Length}", this);
            }
        }
    }

    private void WarnIfMissingRequired()
    {
        if (!rigRoot) Debug.LogWarning($"{LogPrefix} Missing rigRoot reference.", this);
        if (!crownBlock) Debug.LogWarning($"{LogPrefix} Missing crownBlock reference.", this);
        if (!travelingBlock) Debug.LogWarning($"{LogPrefix} Missing travelingBlock reference.", this);
        if (!topDrive) Debug.LogWarning($"{LogPrefix} Missing topDrive reference.", this);
        if (!wellboreStartMarker) Debug.LogWarning($"{LogPrefix} Missing wellboreStartMarker reference.", this);
        if (!bitTipMarker) Debug.LogWarning($"{LogPrefix} Missing bitTipMarker reference.", this);

        // Optional refs: warn for visibility, but they do not affect IsReady.
        if (!drillString) Debug.LogWarning($"{LogPrefix} drillString is optional but currently unassigned.", this);
        if (steelCables == null || steelCables.Length == 0) Debug.LogWarning($"{LogPrefix} steelCables is optional but currently empty.", this);
    }

    private static Transform FindByNameContains(Transform root, bool includeRoot, params string[] needles)
    {
        if (!root) return null;
        List<Transform> ordered = TraverseHierarchy(root, includeRoot);
        for (int i = 0; i < ordered.Count; i++)
        {
            Transform current = ordered[i];
            if (MatchesAny(current.name, needles)) return current;
        }
        return null;
    }

    private static List<Transform> FindAllByNameContains(Transform root, bool includeRoot, params string[] needles)
    {
        List<Transform> results = new List<Transform>(16);
        if (!root) return results;
        List<Transform> ordered = TraverseHierarchy(root, includeRoot);
        for (int i = 0; i < ordered.Count; i++)
        {
            Transform current = ordered[i];
            if (MatchesAny(current.name, needles)) results.Add(current);
        }
        return results;
    }

    // Stable hierarchy order traversal for deterministic auto-wiring.
    private static List<Transform> TraverseHierarchy(Transform root, bool includeRoot)
    {
        List<Transform> ordered = new List<Transform>(64);
        Stack<Transform> stack = new Stack<Transform>(64);

        if (includeRoot)
        {
            stack.Push(root);
        }
        else
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                stack.Push(root.GetChild(i));
            }
        }

        while (stack.Count > 0)
        {
            Transform current = stack.Pop();
            ordered.Add(current);

            for (int i = current.childCount - 1; i >= 0; i--)
            {
                stack.Push(current.GetChild(i));
            }
        }

        return ordered;
    }

    private static bool MatchesAny(string source, string[] needles)
    {
        if (string.IsNullOrEmpty(source) || needles == null || needles.Length == 0) return false;

        for (int i = 0; i < needles.Length; i++)
        {
            if (NameContains(source, needles[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool NameContains(string source, string needle)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(needle)) return false;
        return source.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos) return;
        if (gizmoRadius <= 0f) gizmoRadius = 0.25f;

        if (wellboreStartMarker)
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 1f);
            Gizmos.DrawSphere(wellboreStartMarker.position, gizmoRadius);
        }

        if (bitTipMarker)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.15f, 1f);
            Gizmos.DrawSphere(bitTipMarker.position, gizmoRadius);
        }

        if (wellboreStartMarker && bitTipMarker)
        {
            Gizmos.color = new Color(1f, 0.35f, 0.35f, 1f);
            Gizmos.DrawLine(bitTipMarker.position, wellboreStartMarker.position);
        }
    }
}
