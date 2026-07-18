using System;
using UnityEngine;

namespace AIRGAP.Facility.Guards
{
    /// <summary>
    /// Round-ending capture state. Guards raise it (close-range clear sighting at
    /// any ladder rung, or an Alarmed guard reaching melee); the Infiltrator
    /// controller freezes on it; the HUD renders it. Real round flow arrives with
    /// Phase 14 — for now a capture simply ends the greybox run.
    /// </summary>
    public static class CaptureSystem
    {
        public static bool IsCaptured { get; private set; }
        public static string CapturedByGuardId { get; private set; }
        public static string Reason { get; private set; }

        public static event Action<string, string> OnCapture; // (guardId, reason)

        public static void Capture(string guardId, string reason)
        {
            if (IsCaptured) return;
            IsCaptured = true;
            CapturedByGuardId = guardId;
            Reason = reason;
            Debug.Log($"[AIRGAP] CAPTURE by={guardId} reason={reason}");
            OnCapture?.Invoke(guardId, reason);
        }

        /// <summary>Test/round-reset hook.</summary>
        public static void Reset()
        {
            IsCaptured = false;
            CapturedByGuardId = null;
            Reason = null;
            OnCapture = null;
        }
    }
}
