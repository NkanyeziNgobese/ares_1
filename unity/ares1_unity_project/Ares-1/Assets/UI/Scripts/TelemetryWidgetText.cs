using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class TelemetryWidgetText : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text labelTMP;
    [SerializeField] private TMP_Text valueTMP;

    public void SetLabel(string label)
    {
        if (labelTMP) labelTMP.text = label;
    }

    public void SetValue(string value)
    {
        if (valueTMP) valueTMP.text = value;
    }

    private void Reset()
    {
        // Auto-grab TMPs if the prefab structure matches:
        // TelemetryWidget
        //   Content
        //     LabelTMP
        //     ValueTMP
        var tmps = GetComponentsInChildren<TMP_Text>(true);
        if (tmps.Length >= 2)
        {
            // Assumes Label first, Value second in hierarchy order
            labelTMP = tmps[0];
            valueTMP = tmps[1];
        }
    }

    private void OnValidate()
    {
        if (!labelTMP || !valueTMP)
        {
            // Try to recover references safely
            var tmps = GetComponentsInChildren<TMP_Text>(true);
            if (tmps.Length >= 2)
            {
                labelTMP = tmps[0];
                valueTMP = tmps[1];
            }
        }
    }
}
