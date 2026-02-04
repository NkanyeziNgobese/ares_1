using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DepthTickView : MonoBehaviour
{
    [SerializeField] private Image tickLine;
    [SerializeField] private Image majorLine;
    [SerializeField] private TMP_Text depthLabel;

    public void SetMajor(bool isMajor)
    {
        if (majorLine) majorLine.gameObject.SetActive(isMajor);
        if (tickLine) tickLine.gameObject.SetActive(!isMajor);
        if (depthLabel) depthLabel.gameObject.SetActive(isMajor);
    }

    public void SetLabel(float depthMeters)
    {
        if (!depthLabel) return;
        depthLabel.text = $"{depthMeters:0} m";
    }
}
