using UnityEngine;

namespace AIRGAP.Facility
{
    /// <summary>
    /// Grey-box guard placeholder: carries the persistent display ID (e.g. "G-01")
    /// that Phase 4's duty/alertness systems and Phase 5's identity system attach to.
    /// Perception components (Phase 2) live alongside this on the same GameObject.
    /// </summary>
    public class GuardMarker : MonoBehaviour
    {
        [SerializeField] private string guardId = "G-01";

        public string GuardId => guardId;

        public void SetGuardId(string id) => guardId = id;
    }
}
