using System;
using System.Collections.Concurrent;
using System.Text;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

public class AresMqttSubscriber : MonoBehaviour
{
    [Serializable]
    private class TelemetryPayload
    {
        public float depth;
        public float rop;
        public float vibration;
        public string status;
    }

    [Header("Connection")]
    [SerializeField] private string brokerAddress = "127.0.0.1";
    [SerializeField] private int brokerPort = 1883;
    [SerializeField] private string topic = "ares1/telemetry/realtime";

    [Header("Depth Mapping")]
    [SerializeField] private bool depthPositiveDown = true;
    [SerializeField] private float depthLerpSpeed = 5f;

    [Header("Vibration Shake")]
    [SerializeField] private float vibrationThreshold = 30f;
    [SerializeField] private float shakeAmplitude = 0.05f;
    [SerializeField] private float shakeDampSpeed = 8f;

    private readonly ConcurrentQueue<string> _payloadQueue = new ConcurrentQueue<string>();
    private MqttClient _client;
    private float _targetDepth;
    private float _lastVibration;
    private Vector3 _shakeOffset;

    private void Start()
    {
        Connect();
    }

    private void Connect()
    {
        try
        {
            _client = new MqttClient(brokerAddress, brokerPort, false, null, null, MqttSslProtocols.None);
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

    private void OnMqttMessage(object sender, MqttMsgPublishEventArgs e)
    {
        if (e == null || e.Message == null || e.Message.Length == 0)
        {
            return;
        }

        string payload = Encoding.UTF8.GetString(e.Message);
        _payloadQueue.Enqueue(payload);
    }

    private void Update()
    {
        while (_payloadQueue.TryDequeue(out string payload))
        {
            TelemetryPayload data = null;
            try
            {
                data = JsonUtility.FromJson<TelemetryPayload>(payload);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"MQTT payload parse failed: {ex.Message}");
            }

            if (data != null)
            {
                _targetDepth = data.depth;
                _lastVibration = data.vibration;
            }
        }

        Vector3 basePos = transform.position - _shakeOffset;
        float targetY = depthPositiveDown ? -_targetDepth : _targetDepth;
        basePos.y = Mathf.Lerp(basePos.y, targetY, depthLerpSpeed * Time.deltaTime);

        if (_lastVibration > vibrationThreshold)
        {
            Vector3 targetShake = UnityEngine.Random.insideUnitSphere * shakeAmplitude;
            _shakeOffset = Vector3.Lerp(_shakeOffset, targetShake, 0.5f);
        }
        else
        {
            _shakeOffset = Vector3.Lerp(_shakeOffset, Vector3.zero, shakeDampSpeed * Time.deltaTime);
        }

        transform.position = basePos + _shakeOffset;
    }

    private void OnDestroy()
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
}
