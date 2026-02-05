using System.Globalization;
using UnityEngine;

[DisallowMultipleComponent]
public class DevTelemetryDriver : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TelemetryManager telemetryManager;

    [Header("Toggle")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F5;
    [SerializeField] private bool enabledSim = false;

    [Header("Sim Settings")]
    [Tooltip("Meters per second. Positive means 'drilling down' (depth increases).")]
    [SerializeField] private float depthRateMps = 1.0f;

    [Tooltip("How often to inject a sample (seconds).")]
    [SerializeField] private float sendInterval = 0.2f;

    [Header("Signal Ranges (basic)")]
    [SerializeField] private float rop = 40f;
    [SerializeField] private float wob = 18f;
    [SerializeField] private float rpm = 160f;
    [SerializeField] private float torque = 35f;
    [SerializeField] private float flowIn = 900f;
    [SerializeField] private float flowOut = 870f;

    private float _depth;
    private float _timer;

    private void Reset()
    {
        telemetryManager = FindFirstObjectByType<TelemetryManager>();
    }

    private void OnValidate()
    {
        if (!telemetryManager) telemetryManager = FindFirstObjectByType<TelemetryManager>();
        if (sendInterval <= 0.01f) sendInterval = 0.2f;
    }

    private void Start()
    {
        // Start from current telemetry depth if available
        if (telemetryManager) _depth = telemetryManager.CurrentDepth;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            enabledSim = !enabledSim;
            _timer = 0f;

            if (telemetryManager)
                _depth = telemetryManager.CurrentDepth;

            Debug.Log($"[DevTelemetryDriver] Sim {(enabledSim ? "ENABLED" : "DISABLED")} (F5)");
        }

        if (!enabledSim || telemetryManager == null) return;

        _depth += depthRateMps * Time.deltaTime;

        _timer += Time.deltaTime;
        if (_timer < sendInterval) return;
        _timer = 0f;

        // Build JSON that matches your TelemetryManager payload keys
        // Use invariant culture (dot decimals)
        string json =
            "{" +
            $"\"depth\":{_depth.ToString("0.###", CultureInfo.InvariantCulture)}," +
            $"\"rop\":{rop.ToString("0.###", CultureInfo.InvariantCulture)}," +
            $"\"wob\":{wob.ToString("0.###", CultureInfo.InvariantCulture)}," +
            $"\"rpm\":{rpm.ToString("0.###", CultureInfo.InvariantCulture)}," +
            $"\"torque\":{torque.ToString("0.###", CultureInfo.InvariantCulture)}," +
            $"\"flowIn\":{flowIn.ToString("0.###", CultureInfo.InvariantCulture)}," +
            $"\"flowOut\":{flowOut.ToString("0.###", CultureInfo.InvariantCulture)}" +
            "}";

        telemetryManager.EnqueueJson(json);
    }
}
