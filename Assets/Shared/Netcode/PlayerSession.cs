using Unity.Netcode;
using UnityEngine;

namespace AIRGAP.Shared.Netcode
{
    /// <summary>
    /// One per connected player. The server assigns game roles at connect time:
    /// the host keeps its picked role, the joining client resolves to the complement.
    /// One universal build, role chosen at connect time (Phase 0 Part D).
    /// </summary>
    public class PlayerSession : NetworkBehaviour
    {
        public NetworkVariable<PlayerRole> AssignedRole = new NetworkVariable<PlayerRole>(PlayerRole.None);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                bool isHostPlayer = OwnerClientId == NetworkManager.ServerClientId;
                PlayerRole hostRole = SessionConfig.DesiredRole == PlayerRole.None
                    ? PlayerRole.Infiltrator
                    : SessionConfig.DesiredRole;
                AssignedRole.Value = isHostPlayer ? hostRole : SessionConfig.Complement(hostRole);
            }

            AssignedRole.OnValueChanged += OnRoleChanged;
            if (AssignedRole.Value != PlayerRole.None)
            {
                Announce(AssignedRole.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            AssignedRole.OnValueChanged -= OnRoleChanged;
        }

        private void OnRoleChanged(PlayerRole previous, PlayerRole current)
        {
            Announce(current);
        }

        private void Announce(PlayerRole role)
        {
            if (IsOwner)
            {
                Debug.Log($"[AIRGAP] ROLE assigned: {role} (clientId={OwnerClientId})");
            }
        }
    }
}
