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

    [Header("Cables (optional)")]
    [SerializeField] private Transform[] steelCables;

    [Header("Discovery (optional)")]
    [SerializeField] private bool autoFindByName = true;
    [SerializeField] private bool verboseLogs = false;

    public bool IsReady => rigRoot && crownBlock && travelingBlock && topDrive;

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

        if (steelCables == null || steelCables.Length == 0)
        {
            List<Transform> cables = FindAllByNameContains(searchRoot, true, "Cable", "Steel_Cable");
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
    }

    private static Transform FindByNameContains(Transform root, bool includeRoot, params string[] needles)
    {
        if (!root) return null;

        Queue<Transform> queue = new Queue<Transform>(64);

        if (includeRoot)
        {
            queue.Enqueue(root);
        }
        else
        {
            for (int i = 0; i < root.childCount; i++)
            {
                queue.Enqueue(root.GetChild(i));
            }
        }

        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            if (MatchesAny(current.name, needles))
            {
                return current;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                queue.Enqueue(current.GetChild(i));
            }
        }

        return null;
    }

    private static List<Transform> FindAllByNameContains(Transform root, bool includeRoot, params string[] needles)
    {
        List<Transform> results = new List<Transform>(16);
        if (!root) return results;

        Queue<Transform> queue = new Queue<Transform>(64);

        if (includeRoot)
        {
            queue.Enqueue(root);
        }
        else
        {
            for (int i = 0; i < root.childCount; i++)
            {
                queue.Enqueue(root.GetChild(i));
            }
        }

        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            if (MatchesAny(current.name, needles))
            {
                results.Add(current);
            }

            for (int i = 0; i < current.childCount; i++)
            {
                queue.Enqueue(current.GetChild(i));
            }
        }

        return results;
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
        if (crownBlock)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.15f, 1f);
            Gizmos.DrawSphere(crownBlock.position, 0.2f);
        }

        if (travelingBlock)
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 1f);
            Gizmos.DrawSphere(travelingBlock.position, 0.2f);
        }
    }
}
