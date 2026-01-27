// ============================================================================
// HEADER: Setup, calibration, and troubleshooting notes
// ============================================================================
// Dependencies:
// - uPLibrary.Networking.M2Mqtt (M2Mqtt.Net.dll)
// - UnityEngine, System.Collections.Concurrent
//
// How to set up in scene:
// 1) Add this script to a rig controller GameObject.
// 2) Assign Traveling Block, Top Drive, and (optionally) Crown Block transforms.
// 3) Assign 8 cable cylinder transforms to Steel Cables.
// 4) Assign LineRenderer + Standpipe + Swivel anchors for the hose.
// 5) Assign Top Drive renderer/materials if hazard color changes are desired.
// 6) Set verticalAxis to Z (default) unless the scene uses Y-up for height.
// 7) Play, then run the context menu: "Calibrate Offset From Current Pose".
//
// Kelly offset calibration:
// - The incoming depth is the bit depth in meters (negative down by default).
// - travelingBlockAxis = DepthToAxis(depth) + kellyOffset
// - Calibrate when the bit visually touches ground at wellboreStartDepth (-2.9999).
//
// Troubleshooting:
// - MQTT not receiving: verify broker host/port and topic (ares1/telemetry/main).
// - API compatibility: ensure the project uses .NET Framework in Player Settings.
// - Cable scaling wrong: confirm cableMeshLengthUnits (default 2) and cableLocalAxis.
// - Hose not visible: ensure LineRenderer positions are updated and segments > 1.
// ============================================================================
//
// ============================================================================
// COMMENT-TO-CODE OUTLINE (Ares1_HoistingController)
// ============================================================================
// SECTION 0: File header
// - Purpose: Drive hoisting visuals from MQTT depth/status telemetry
// - Dependencies: UnityEngine, M2Mqtt, ConcurrentQueue
// - Scene setup steps + calibration notes + troubleshooting
//
// SECTION 1: Data model + enums
// - Telemetry payload (depth, status)
// - Vertical axis selector (Z default, Y optional)
// - Cable local axis selector (LocalY default, LocalZ optional)
//
// SECTION 2: Inspector fields
// - MQTT connection + topic
// - Rig references (traveling block, top drive, crown block)
// - Depth mapping controls (kelly offset, scale, axis, smoothing)
// - Steel cables (array of cylinders, mesh length, local axis)
// - Hose line (anchors, sag, segments)
// - Hazard effects (screen shake + top drive material/color)
//
// SECTION 3: Private runtime state
// - MQTT client + thread-safe queue
// - Latest depth/status + hazard state
// - Cached axis positions + smoothing
// - Cached cable scales + hose buffer
// - Camera shake cache + material cache
//
// SECTION 4: Unity lifecycle
// - Awake/Start: cache references, connect MQTT, init buffers
// - Update: drain queue, move traveling block, update cables/hoses, hazard effects
// - OnDestroy: clean MQTT shutdown
//
// SECTION 5: MQTT plumbing (thread safe)
// - Connect + subscribe
// - Message callback: enqueue payload
// - Drain queue: parse last message only
//
// SECTION 6: Mapping + helpers
// - DepthToAxis: convert depth meters to rig axis units
// - GetAxis/SetAxis: axis-agnostic helpers (Y or Z)
// - Axis unit vectors (up/down) for hose sag
//
// SECTION 7: Visual update helpers
// - UpdateTravelingBlock: smooth axis motion
// - UpdateCables: midpoint position + scale along local axis
// - UpdateHose: quadratic Bezier sampled into LineRenderer
// - ApplyHazard: screen shake and/or top drive color/material
//
// SECTION 8: Calibration utility
// - Context menu: Calibrate Offset From Current Pose
//   kellyOffset = currentAxis - DepthToAxis(currentDepth)
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Text;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

public class Ares1_HoistingController : MonoBehaviour
{
    // ============================================================================
    // SECTION 1: Data model + enums
    // ============================================================================

    [Serializable]
    private class Telemetry
    {
        public float depth;
        public string status;
    }

    public enum VerticalAxis
    {
        Z,
        Y
    }

    public enum CableAxis
    {
        LocalY,
        LocalZ
    }

    // ============================================================================
    // SECTION 2: Inspector fields
    // ============================================================================

    [Header("MQTT")]
    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private int port = 1883;
    [SerializeField] private string topic = "ares1/telemetry/main";

    [Header("Rig References")]
    [SerializeField] private Transform travelingBlock;
    [SerializeField] private Transform topDrive;
    [SerializeField] private Transform crownBlockTransform;
    [SerializeField] private float crownBlockWorldZ = 45f;
    [SerializeField] private Renderer topDriveRenderer;
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material hazardMaterial;
    [SerializeField] private Color hazardColor = Color.red;

    [Header("Depth Mapping")]
    [SerializeField] private float wellboreStartDepth = -2.9999f;
    [SerializeField] private float kellyOffset = 0f;
    [SerializeField] private float depthScale = 1f;
    [SerializeField] private float motionLerpSpeed = 2.5f;
    [SerializeField] private VerticalAxis verticalAxis = VerticalAxis.Z;
    [SerializeField] private bool depthIsNegativeDown = true;

    [Header("Steel Cables")]
    [SerializeField] private Transform[] steelCables = new Transform[8];
    [SerializeField] private CableAxis cableLocalAxis = CableAxis.LocalY;
    [SerializeField] private float cableMeshLengthUnits = 2f;
    [SerializeField] private Transform cableTopAnchor;
    [SerializeField] private Transform cableBottomAnchor;

    [Header("Fluid Hose (Bezier)")]
    [SerializeField] private LineRenderer hoseLine;
    [SerializeField] private Transform standpipeAnchor;
    [SerializeField] private Transform swivelAnchor;
    [SerializeField] private int hoseSegments = 24;
    [SerializeField] private float hoseSag = 1.5f;

    [Header("Hazard Effects")]
    [SerializeField] private bool enableScreenShake = true;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float shakeAmplitude = 0.25f;
    [SerializeField] private float shakeReturnSpeed = 8f;

    // ============================================================================
    // SECTION 3: Private runtime state
    // ============================================================================

    private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
    private MqttClient _client;

    private float _latestDepth;
    private string _latestStatus;
    private bool _hasTelemetry;
    private bool _isHazard;

    private float _currentAxis;
    private Vector3[] _hosePoints;
    private Vector3[] _cableBaseScales;

    private Vector3 _cameraBaseLocalPos;
    private Vector3 _shakeOffset;
    private bool _hazardMaterialApplied;
    private Material _runtimeTopDriveMaterial;
    private Color _originalTopDriveColor;

    // ============================================================================
    // SECTION 4: Unity lifecycle
    // ============================================================================

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            _cameraBaseLocalPos = targetCamera.transform.localPosition;
        }

        if (topDriveRenderer != null)
        {
            _runtimeTopDriveMaterial = topDriveRenderer.material;
            _originalTopDriveColor = _runtimeTopDriveMaterial.color;
        }

        CacheCableScales();
        EnsureHoseBuffer();
    }

    private void Start()
    {
        if (travelingBlock != null)
        {
            _currentAxis = GetAxisValue(travelingBlock.position);
        }

        ConnectMqtt();
    }

    private void Update()
    {
        DrainQueueAndUpdateTelemetry();

        if (travelingBlock != null)
        {
            UpdateTravelingBlock();
        }

        UpdateCables();
        UpdateHose();
        ApplyHazardEffects();
    }

    private void OnDestroy()
    {
        DisconnectMqtt();
    }

    // ============================================================================
    // SECTION 5: MQTT plumbing (thread safe)
    // ============================================================================

    private void ConnectMqtt()
    {
        try
        {
            _client = new MqttClient(host, port, false, null, null, MqttSslProtocols.None);
            _client.MqttMsgPublishReceived += OnMqttMessage;
            string clientId = Guid.NewGuid().ToString("N");
            byte result = _client.Connect(clientId);
            if (result == MqttMsgConnack.CONN_ACCEPTED)
            {
                _client.Subscribe(new[] { topic }, new[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
            }
            else
            {
                Debug.LogWarning($"MQTT connect returned code {result}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"MQTT connect failed: {ex.Message}");
        }
    }

    private void DisconnectMqtt()
    {
        if (_client == null)
        {
            return;
        }

        try
        {
            _client.MqttMsgPublishReceived -= OnMqttMessage;
            if (_client.IsConnected)
            {
                _client.Unsubscribe(new[] { topic });
                _client.Disconnect();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"MQTT disconnect failed: {ex.Message}");
        }
    }

    private void OnMqttMessage(object sender, MqttMsgPublishEventArgs e)
    {
        if (e == null || e.Message == null || e.Message.Length == 0)
        {
            return;
        }

        string payload = Encoding.UTF8.GetString(e.Message);
        _queue.Enqueue(payload);
    }

    private void DrainQueueAndUpdateTelemetry()
    {
        string latestPayload = null;
        while (_queue.TryDequeue(out string payload))
        {
            latestPayload = payload;
        }

        if (string.IsNullOrEmpty(latestPayload))
        {
            return;
        }

        Telemetry telemetry = null;
        try
        {
            telemetry = JsonUtility.FromJson<Telemetry>(latestPayload);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Telemetry parse failed: {ex.Message}");
        }

        if (telemetry == null)
        {
            return;
        }

        _latestDepth = telemetry.depth;
        _latestStatus = telemetry.status ?? string.Empty;
        _hasTelemetry = true;
        _isHazard = _latestStatus.IndexOf("HAZARD", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ============================================================================
    // SECTION 6: Mapping + helpers
    // ============================================================================

    private float DepthToAxis(float depthMeters)
    {
        float signedDepth = depthIsNegativeDown ? depthMeters : -depthMeters;
        return signedDepth * depthScale;
    }

    private float GetAxisValue(Vector3 position)
    {
        return verticalAxis == VerticalAxis.Z ? position.z : position.y;
    }

    private Vector3 SetAxisValue(Vector3 position, float axisValue)
    {
        if (verticalAxis == VerticalAxis.Z)
        {
            position.z = axisValue;
        }
        else
        {
            position.y = axisValue;
        }
        return position;
    }

    private Vector3 AxisUpVector()
    {
        return verticalAxis == VerticalAxis.Z ? Vector3.forward : Vector3.up;
    }

    private Vector3 AxisDownVector()
    {
        return verticalAxis == VerticalAxis.Z ? Vector3.back : Vector3.down;
    }

    private float GetCrownAxis()
    {
        if (crownBlockTransform != null)
        {
            return GetAxisValue(crownBlockTransform.position);
        }

        return crownBlockWorldZ;
    }

    // ============================================================================
    // SECTION 7: Visual update helpers
    // ============================================================================

    private void UpdateTravelingBlock()
    {
        float targetAxis = DepthToAxis(_latestDepth) + kellyOffset;
        _currentAxis = Mathf.Lerp(_currentAxis, targetAxis, motionLerpSpeed * Time.deltaTime);

        Vector3 pos = travelingBlock.position;
        pos = SetAxisValue(pos, _currentAxis);
        travelingBlock.position = pos;
    }

    private void CacheCableScales()
    {
        if (steelCables == null || steelCables.Length == 0)
        {
            return;
        }

        _cableBaseScales = new Vector3[steelCables.Length];
        for (int i = 0; i < steelCables.Length; i++)
        {
            _cableBaseScales[i] = steelCables[i] != null ? steelCables[i].localScale : Vector3.one;
        }
    }

    private void UpdateCables()
    {
        if (steelCables == null || steelCables.Length == 0)
        {
            return;
        }

        float topAxis = cableTopAnchor != null ? GetAxisValue(cableTopAnchor.position) : GetCrownAxis();
        float bottomAxis = cableBottomAnchor != null ? GetAxisValue(cableBottomAnchor.position) : _currentAxis;
        float requiredLength = Mathf.Abs(topAxis - bottomAxis);
        float scaleFactor = cableMeshLengthUnits > 0f ? requiredLength / cableMeshLengthUnits : 1f;
        float midpoint = (topAxis + bottomAxis) * 0.5f;

        for (int i = 0; i < steelCables.Length; i++)
        {
            Transform cable = steelCables[i];
            if (cable == null)
            {
                continue;
            }

            Vector3 scale = _cableBaseScales != null && i < _cableBaseScales.Length
                ? _cableBaseScales[i]
                : cable.localScale;

            if (cableLocalAxis == CableAxis.LocalY)
            {
                scale.y = scale.y * scaleFactor;
            }
            else
            {
                scale.z = scale.z * scaleFactor;
            }

            cable.localScale = scale;

            Vector3 pos = cable.position;
            pos = SetAxisValue(pos, midpoint);
            cable.position = pos;
        }
    }

    private void EnsureHoseBuffer()
    {
        int count = Mathf.Max(2, hoseSegments + 1);
        if (_hosePoints == null || _hosePoints.Length != count)
        {
            _hosePoints = new Vector3[count];
        }
    }

    private void UpdateHose()
    {
        if (hoseLine == null || standpipeAnchor == null || swivelAnchor == null)
        {
            return;
        }

        EnsureHoseBuffer();

        Vector3 p0 = standpipeAnchor.position;
        Vector3 p2 = swivelAnchor.position;
        Vector3 midpoint = (p0 + p2) * 0.5f;
        Vector3 p1 = midpoint + AxisDownVector() * hoseSag;

        int count = _hosePoints.Length;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0f : i / (float)(count - 1);
            float oneMinus = 1f - t;
            _hosePoints[i] = oneMinus * oneMinus * p0 + 2f * oneMinus * t * p1 + t * t * p2;
        }

        if (hoseLine.positionCount != count)
        {
            hoseLine.positionCount = count;
        }
        hoseLine.SetPositions(_hosePoints);
    }

    private void ApplyHazardEffects()
    {
        bool hazardActive = _isHazard;

        if (enableScreenShake && targetCamera != null)
        {
            if (hazardActive)
            {
                Vector3 targetShake = UnityEngine.Random.insideUnitSphere * shakeAmplitude;
                _shakeOffset = Vector3.Lerp(_shakeOffset, targetShake, 0.5f);
            }
            else
            {
                _shakeOffset = Vector3.Lerp(_shakeOffset, Vector3.zero, shakeReturnSpeed * Time.deltaTime);
            }

            targetCamera.transform.localPosition = _cameraBaseLocalPos + _shakeOffset;
        }

        if (topDriveRenderer == null)
        {
            return;
        }

        if (hazardActive)
        {
            if (!_hazardMaterialApplied)
            {
                if (hazardMaterial != null)
                {
                    topDriveRenderer.material = hazardMaterial;
                }
                else if (_runtimeTopDriveMaterial != null)
                {
                    _runtimeTopDriveMaterial.color = hazardColor;
                }

                _hazardMaterialApplied = true;
            }
        }
        else if (_hazardMaterialApplied)
        {
            if (normalMaterial != null)
            {
                topDriveRenderer.material = normalMaterial;
            }
            else if (_runtimeTopDriveMaterial != null)
            {
                _runtimeTopDriveMaterial.color = _originalTopDriveColor;
            }

            _hazardMaterialApplied = false;
        }
    }

    // ============================================================================
    // SECTION 8: Calibration utility
    // ============================================================================

    [ContextMenu("Calibrate Offset From Current Pose")]
    private void CalibrateOffsetFromCurrentPose()
    {
        if (travelingBlock == null)
        {
            Debug.LogWarning("Calibration failed: travelingBlock not assigned.");
            return;
        }

        float depthForCalibration = _hasTelemetry ? _latestDepth : wellboreStartDepth;
        float currentAxis = GetAxisValue(travelingBlock.position);
        kellyOffset = currentAxis - DepthToAxis(depthForCalibration);
        Debug.Log($"Kelly offset calibrated to {kellyOffset:F4} using depth {depthForCalibration:F4}.");
    }
}
