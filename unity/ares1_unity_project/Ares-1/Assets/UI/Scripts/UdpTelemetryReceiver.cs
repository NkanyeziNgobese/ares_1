using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[DisallowMultipleComponent]
public class UdpTelemetryReceiver : MonoBehaviour
{
    [Header("Networking")]
    [SerializeField] private int listenPort = 5055;
    [Tooltip("If enabled, only accept packets from this IP (leave blank to accept all).")]
    [SerializeField] private string allowedSenderIp = "";

    [Header("Target")]
    [SerializeField] private TelemetryManager telemetryManager;

    private UdpClient _client;
    private Thread _thread;
    private volatile bool _running;

    private void Reset()
    {
        telemetryManager = FindFirstObjectByType<TelemetryManager>();
    }

    private void OnValidate()
    {
        if (!telemetryManager)
            telemetryManager = FindFirstObjectByType<TelemetryManager>();
    }

    private void OnEnable()
    {
        StartReceiver();
    }

    private void OnDisable()
    {
        StopReceiver();
    }

    private void OnApplicationQuit()
    {
        StopReceiver();
    }

    private void StartReceiver()
    {
        if (_running) return;

        try
        {
            _client = new UdpClient(listenPort);
            _running = true;

            _thread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "UDP Telemetry Receiver"
            };
            _thread.Start();

            Debug.Log($"[UdpTelemetryReceiver] Listening on UDP port {listenPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UdpTelemetryReceiver] Failed to start: {e.Message}");
            StopReceiver();
        }
    }

    private void StopReceiver()
    {
        _running = false;

        try { _client?.Close(); } catch { /* ignore */ }
        _client = null;

        try { _thread?.Join(200); } catch { /* ignore */ }
        _thread = null;
    }

    private void ReceiveLoop()
    {
        IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

        while (_running)
        {
            try
            {
                byte[] data = _client.Receive(ref remote);

                if (!string.IsNullOrWhiteSpace(allowedSenderIp))
                {
                    if (remote.Address.ToString() != allowedSenderIp)
                        continue;
                }

                string json = Encoding.UTF8.GetString(data);

                // Hand off to TelemetryManager (thread-safe queue)
                if (telemetryManager)
                    telemetryManager.EnqueueJson(json);
            }
            catch (SocketException)
            {
                // Happens when closing the socket; safe to ignore on shutdown
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UdpTelemetryReceiver] Receive error: {e.Message}");
            }
        }
    }

    [ContextMenu("DEV: Print My IP")]
    private void DevPrintMyIp()
    {
        Debug.Log($"[UdpTelemetryReceiver] Local machine: {Dns.GetHostName()}");
    }
}
