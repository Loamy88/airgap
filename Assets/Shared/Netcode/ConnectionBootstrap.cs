using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace AIRGAP.Shared.Netcode
{
    /// <summary>
    /// The universal build's entry point: a host-or-join screen with a role pick,
    /// or fully CLI-driven startup for headless smoke tests (see SessionConfig for args).
    /// LAN direct-connect only for now — Phase 16 upgrades this to Relay/Lobby.
    /// </summary>
    public class ConnectionBootstrap : MonoBehaviour
    {
        private const float ClientSmokeTimeoutSeconds = 60f;
        private const float HostSmokeTimeoutSeconds = 90f;
        private const float QuitDelaySeconds = 2f;

        private static bool _serverSawPing;
        private static bool _clientPingSucceeded;

        private string _addressField = "127.0.0.1";
        private string _portField = SessionConfig.DefaultPort.ToString();
        private bool _started;
        private float _startedAt;
        private float _quitAt = -1f;
        private int _quitCode;
        private string _status = "";

        public static void NotifyServerSawPing() => _serverSawPing = true;
        public static void NotifyClientPingSucceeded() => _clientPingSucceeded = true;

        private void Awake()
        {
            Application.runInBackground = true;
            _serverSawPing = false;
            _clientPingSucceeded = false;
            SessionConfig.ParseCommandLine(Environment.GetCommandLineArgs());
        }

        private void Start()
        {
            switch (SessionConfig.Mode)
            {
                case StartMode.Host:
                    StartHost(SessionConfig.Port);
                    break;
                case StartMode.Client:
                    StartClient(SessionConfig.ServerAddress, SessionConfig.Port);
                    break;
                case StartMode.Interactive:
                    break;
            }
        }

        private void StartHost(ushort port)
        {
            var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.StartHost();
            _started = true;
            _startedAt = Time.realtimeSinceStartup;
            _status = $"Hosting on UDP {port} as {SessionConfig.DesiredRole} — waiting for the other player…";
            Debug.Log($"[AIRGAP] HOST started (port={port}, role={SessionConfig.DesiredRole}, smoke={SessionConfig.SmokeMode})");
        }

        private void StartClient(string address, ushort port)
        {
            var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            transport.SetConnectionData(address, port);
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.StartClient();
            _started = true;
            _startedAt = Time.realtimeSinceStartup;
            _status = $"Joining {address}:{port}…";
            Debug.Log($"[AIRGAP] CLIENT connecting (server={address}:{port}, smoke={SessionConfig.SmokeMode})");
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[AIRGAP] connected: clientId={clientId}");
            _status = NetworkManager.Singleton.IsHost
                ? "Player connected."
                : "Connected — role assignment incoming.";
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[AIRGAP] disconnected: clientId={clientId}");
            // In host smoke mode the client leaving after a successful ping ends the test.
            if (SessionConfig.SmokeMode && NetworkManager.Singleton.IsHost && _serverSawPing)
            {
                ScheduleQuit(0, "smoke complete (client round-tripped and left)");
            }
        }

        private void Update()
        {
            if (_quitAt > 0f && Time.realtimeSinceStartup >= _quitAt)
            {
                Application.Quit(_quitCode);
                return;
            }

            if (!SessionConfig.SmokeMode || !_started || _quitAt > 0f) return;

            float elapsed = Time.realtimeSinceStartup - _startedAt;

            if (NetworkManager.Singleton.IsHost)
            {
                if (elapsed > HostSmokeTimeoutSeconds)
                {
                    Debug.LogError("[AIRGAP] SMOKE FAIL: host timeout without ping round-trip");
                    ScheduleQuit(1, "host smoke timeout");
                }
            }
            else
            {
                if (_clientPingSucceeded)
                {
                    Debug.Log("[AIRGAP] SMOKE OK: client shutting down");
                    NetworkManager.Singleton.Shutdown();
                    ScheduleQuit(0, "client smoke complete");
                }
                else if (elapsed > ClientSmokeTimeoutSeconds)
                {
                    Debug.LogError("[AIRGAP] SMOKE FAIL: client timeout without pong");
                    ScheduleQuit(1, "client smoke timeout");
                }
            }
        }

        private void ScheduleQuit(int code, string reason)
        {
            if (_quitAt > 0f) return;
            Debug.Log($"[AIRGAP] quitting in {QuitDelaySeconds}s: {reason} (exit={code})");
            _quitCode = code;
            _quitAt = Time.realtimeSinceStartup + QuitDelaySeconds;
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 420, 340), GUI.skin.box);
            GUILayout.Label("<b>AIRGAP — network bootstrap (Phase 0)</b>",
                new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16 });

            if (!_started)
            {
                GUILayout.Space(6);
                GUILayout.Label("Host role:");
                GUILayout.BeginHorizontal();
                if (GUILayout.Toggle(SessionConfig.DesiredRole == PlayerRole.Infiltrator, " Infiltrator"))
                    SessionConfig.DesiredRole = PlayerRole.Infiltrator;
                if (GUILayout.Toggle(SessionConfig.DesiredRole == PlayerRole.Warden, " Warden"))
                    SessionConfig.DesiredRole = PlayerRole.Warden;
                GUILayout.EndHorizontal();

                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Address", GUILayout.Width(60));
                _addressField = GUILayout.TextField(_addressField);
                GUILayout.Label("Port", GUILayout.Width(35));
                _portField = GUILayout.TextField(_portField, GUILayout.Width(60));
                GUILayout.EndHorizontal();

                ushort port = ushort.TryParse(_portField, out ushort parsed) ? parsed : SessionConfig.DefaultPort;

                GUILayout.Space(10);
                if (GUILayout.Button($"Host  (you play {SessionConfig.DesiredRole})", GUILayout.Height(32)))
                    StartHost(port);
                if (GUILayout.Button("Join  (role assigned by host)", GUILayout.Height(32)))
                    StartClient(_addressField, port);
            }
            else
            {
                GUILayout.Space(10);
                GUILayout.Label(_status);
                var local = NetworkManager.Singleton.LocalClient?.PlayerObject;
                var session = local != null ? local.GetComponent<PlayerSession>() : null;
                if (session != null && session.AssignedRole.Value != PlayerRole.None)
                    GUILayout.Label($"You are: {session.AssignedRole.Value}");

                GUILayout.Space(10);
                if (GUILayout.Button("Disconnect", GUILayout.Height(28)))
                {
                    NetworkManager.Singleton.Shutdown();
                    _started = false;
                    _status = "";
                }
            }

            GUILayout.EndArea();
        }
    }
}
