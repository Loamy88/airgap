using Unity.Netcode;
using UnityEngine;

namespace AIRGAP.Shared.Netcode
{
    /// <summary>
    /// Phase 0 smoke-test probe: the joining client sends a ping RPC on spawn,
    /// the server replies, the client measures the round trip. Every later phase
    /// builds on this connection existing.
    /// </summary>
    public class PingProbe : NetworkBehaviour
    {
        private float _pingSentAt;

        public override void OnNetworkSpawn()
        {
            if (IsOwner && !IsServer)
            {
                _pingSentAt = Time.realtimeSinceStartup;
                PingServerRpc(NetworkManager.LocalClientId);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void PingServerRpc(ulong senderClientId)
        {
            Debug.Log($"[AIRGAP] SMOKE ping received (from client {senderClientId})");
            ConnectionBootstrap.NotifyServerSawPing();
            PongClientRpc();
        }

        [ClientRpc]
        private void PongClientRpc()
        {
            if (!IsOwner) return;
            float rttMs = (Time.realtimeSinceStartup - _pingSentAt) * 1000f;
            Debug.Log($"[AIRGAP] SMOKE pong received rtt={rttMs:F1}ms");
            ConnectionBootstrap.NotifyClientPingSucceeded();
        }
    }
}
