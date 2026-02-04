using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class NetStatsDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private TelemetryManager telemetryManager;

    private void Reset()
    {
        if (!text) text = GetComponent<TMP_Text>();
        if (!telemetryManager) telemetryManager = FindFirstObjectByType<TelemetryManager>();
    }

    private void OnValidate()
    {
        if (!text) text = GetComponent<TMP_Text>();
        if (!telemetryManager) telemetryManager = FindFirstObjectByType<TelemetryManager>();
    }

    private void Update()
    {
        if (!text || !telemetryManager) return;

        float ageMs = telemetryManager.SecondsSinceLastPacket * 1000f;
        text.text = $"RX: {telemetryManager.RxHz:0.0} Hz   AGE: {ageMs:0} ms";
    }
}
