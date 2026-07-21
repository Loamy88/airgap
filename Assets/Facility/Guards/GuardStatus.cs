using UnityEngine;

namespace AIRGAP.Facility.Guards
{
    public enum StatusCheckResult
    {
        NoSuchGuard,
        Responsive,
        Unresponsive
    }

    /// <summary>
    /// The status-check backend (Phase 5): the Warden's ONLY proactive way to
    /// learn a guard is down. Phase 6's click interaction calls this. A check
    /// that comes back Unresponsive is one of the two confirmation paths that
    /// can trip the Badge Flag.
    /// </summary>
    public static class GuardStatus
    {
        public static StatusCheckResult CheckGuardStatus(string guardId)
        {
            GuardAgent agent = GuardAgent.FindById(guardId);
            if (agent == null) return StatusCheckResult.NoSuchGuard;
            agent.EnsureInitialized();

            StatusCheckResult result = agent.Consciousness.IsDown
                ? StatusCheckResult.Unresponsive
                : StatusCheckResult.Responsive;
            Debug.Log($"[AIRGAP] STATUS-CHECK guard={guardId} -> {result}");

            if (result == StatusCheckResult.Unresponsive)
                BadgeSystem.ConfirmUnresponsive(guardId);
            return result;
        }
    }
}
