using System;
using UnityEngine;

namespace AIRGAP.Facility
{
    /// <summary>
    /// Warden-side security event bus (Phase 5 backend). Two very different
    /// severities by design:
    /// - BadgeAlert: a FLAGGED badge hit a reader — immediate, precise,
    ///   unambiguous (door + badge named).
    /// - SuspicionPing: a badge used implausibly far from its owner's expected
    ///   location — passive, soft, fires whether or not the badge is flagged.
    /// Phase 6's Facility Deployment screen consumes both.
    /// </summary>
    public static class SecurityEvents
    {
        public static event Action<string, string> BadgeAlert;    // (doorId, badgeId)
        public static event Action<string, string> SuspicionPing; // (doorId, badgeId)

        public static void RaiseBadgeAlert(string doorId, string badgeId)
        {
            Debug.Log($"[AIRGAP] BADGE-ALERT door={doorId} badge={badgeId} (flagged badge use — precise)");
            BadgeAlert?.Invoke(doorId, badgeId);
        }

        public static void RaiseSuspicionPing(string doorId, string badgeId)
        {
            Debug.Log($"[AIRGAP] SUSPICION-PING door={doorId} badge={badgeId} (implausible badge location — soft)");
            SuspicionPing?.Invoke(doorId, badgeId);
        }

        public static void Reset()
        {
            BadgeAlert = null;
            SuspicionPing = null;
        }
    }
}
