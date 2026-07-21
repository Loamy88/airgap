using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRGAP.Facility.Guards
{
    public enum BadgeHolder
    {
        Owner,
        Infiltrator
    }

    public class BadgeRecord
    {
        public string BadgeId;
        public string OwnerId;     // "G-04" (Technicians join in Phase 13)
        public BadgeHolder Holder = BadgeHolder.Owner;
        public bool Flagged;
    }

    /// <summary>
    /// Badge possession + the Badge Flag rule (Phase 5): a looted badge is a
    /// real, working credential right up until the owner's Unresponsive state is
    /// CONFIRMED — by a Warden status check or by the owner's own wake-up report
    /// — at which point, if the badge is missing, that specific badge ID is
    /// flagged facility-wide. Until that moment nothing happens: a takedown is
    /// not, by itself, information.
    /// </summary>
    public static class BadgeSystem
    {
        private static readonly Dictionary<string, BadgeRecord> ByOwner = new Dictionary<string, BadgeRecord>();

        public static event Action<string> BadgeFlagged; // badgeId

        public static void Register(string ownerId, string badgeIdPrefix)
        {
            if (ByOwner.ContainsKey(ownerId)) return;
            ByOwner[ownerId] = new BadgeRecord { BadgeId = badgeIdPrefix + ownerId, OwnerId = ownerId };
        }

        public static BadgeRecord OfOwner(string ownerId) =>
            ByOwner.TryGetValue(ownerId, out BadgeRecord record) ? record : null;

        public static BadgeRecord ById(string badgeId)
        {
            foreach (BadgeRecord record in ByOwner.Values)
                if (record.BadgeId == badgeId) return record;
            return null;
        }

        /// <summary>Loot only works on a Down owner still carrying their badge (Phase 10's on-body prompt).</summary>
        public static bool TryLoot(string ownerId)
        {
            BadgeRecord record = OfOwner(ownerId);
            GuardAgent agent = GuardAgent.FindById(ownerId);
            if (record == null || agent == null) return false;
            if (!agent.Consciousness.IsDown || record.Holder != BadgeHolder.Owner) return false;
            record.Holder = BadgeHolder.Infiltrator;
            Debug.Log($"[AIRGAP] BADGE looted: {record.BadgeId} from {ownerId}");
            return true;
        }

        /// <summary>
        /// The Badge Flag trigger — called the moment an owner's Unresponsive
        /// state is confirmed (status check or wake-up report). Flags only if
        /// the badge is actually missing.
        /// </summary>
        public static void ConfirmUnresponsive(string ownerId)
        {
            BadgeRecord record = OfOwner(ownerId);
            if (record == null || record.Flagged) return;
            if (record.Holder == BadgeHolder.Owner) return; // badge still on the body — nothing to flag
            record.Flagged = true;
            Debug.Log($"[AIRGAP] BADGE-FLAG {record.BadgeId} flagged facility-wide (owner {ownerId} confirmed unresponsive, badge missing)");
            BadgeFlagged?.Invoke(record.BadgeId);
        }

        /// <summary>Test/round-reset hook.</summary>
        public static void Reset()
        {
            ByOwner.Clear();
            BadgeFlagged = null;
        }
    }
}
