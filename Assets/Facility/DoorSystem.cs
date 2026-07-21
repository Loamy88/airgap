using System.Collections.Generic;
using AIRGAP.Facility.Blueprints;
using AIRGAP.Facility.Guards;
using UnityEngine;

namespace AIRGAP.Facility
{
    public enum BadgeUseResult
    {
        NoSuchDoor,
        NotBadgeGated,
        UnknownBadge,
        FlaggedBadgeRejected,
        Opened
    }

    public class DoorState
    {
        public string DoorId;
        public string Type;              // unlocked | locked | badge-gated
        public bool Open;
        public string LastOpenerBadgeId; // attribution: exactly which badge opened it
        public Vector2 WorldPosition;
    }

    /// <summary>
    /// Phase 5 door backend: per-door open/closed state, the badge attribution
    /// log, flagged-badge rejection (immediate precise alert), and the
    /// plausibility check (badge used far from its owner's expected location →
    /// passive suspicion ping, independent of any flag). Phase 6 renders this on
    /// the Facility Deployment map; Phase 10/13 add the physical interactions.
    /// </summary>
    public static class DoorSystem
    {
        private static readonly Dictionary<string, DoorState> Doors = new Dictionary<string, DoorState>();
        private static bool _initialized;

        public static bool IsInitialized => _initialized;

        public static void Initialize(Blueprint bp, RoleAssignment assignment)
        {
            Doors.Clear();
            foreach (BlueprintDoor door in bp.Doors)
            {
                Vector2 dataCenter = door.Orientation == "h"
                    ? new Vector2(door.X + door.Length * 0.5f, door.Y)
                    : new Vector2(door.X, door.Y + door.Length * 0.5f);
                Doors[door.Id] = new DoorState
                {
                    DoorId = door.Id,
                    Type = assignment.DoorTypeOf(door.Id),
                    WorldPosition = Blueprint.ToWorld(dataCenter)
                };
            }
            _initialized = true;
            Debug.Log($"[AIRGAP] DoorSystem initialized — {Doors.Count} doors");
        }

        public static DoorState Of(string doorId) =>
            Doors.TryGetValue(doorId, out DoorState state) ? state : null;

        public static BadgeUseResult TryBadgeUse(string doorId, string badgeId)
        {
            DoorState door = Of(doorId);
            if (door == null) return BadgeUseResult.NoSuchDoor;
            if (door.Type != "badge-gated") return BadgeUseResult.NotBadgeGated;

            BadgeRecord badge = BadgeSystem.ById(badgeId);
            if (badge == null) return BadgeUseResult.UnknownBadge;

            if (badge.Flagged)
            {
                // A flagged badge fails on every door and pushes a precise,
                // unambiguous alert on the attempt itself.
                SecurityEvents.RaiseBadgeAlert(doorId, badgeId);
                return BadgeUseResult.FlaggedBadgeRejected;
            }

            door.Open = true;
            door.LastOpenerBadgeId = badgeId;
            Debug.Log($"[AIRGAP] DOOR {doorId} opened on badge {badgeId}");

            CheckPlausibility(door, badge);
            return BadgeUseResult.Opened;
        }

        public static void SetOpen(string doorId, bool open)
        {
            DoorState door = Of(doorId);
            if (door != null) door.Open = open;
        }

        /// <summary>
        /// The soft tell: compare the badge owner's expected location (post, or
        /// nearest patrol waypoint) with the door actually being opened. Fires
        /// whether or not the badge has been flagged yet.
        /// </summary>
        private static void CheckPlausibility(DoorState door, BadgeRecord badge)
        {
            GuardAgent owner = GuardAgent.FindById(badge.OwnerId);
            if (owner == null) return;

            float radius = AIRGAP.Shared.Data.GameConfig.Load().Guard.BadgePlausibilityRadius;
            float nearest = float.MaxValue;
            foreach (Vector2 point in owner.ExpectedDutyPoints())
                nearest = Mathf.Min(nearest, Vector2.Distance(door.WorldPosition, point));

            if (nearest > radius)
                SecurityEvents.RaiseSuspicionPing(door.DoorId, badge.BadgeId);
        }

        public static void Reset()
        {
            Doors.Clear();
            _initialized = false;
        }
    }
}
