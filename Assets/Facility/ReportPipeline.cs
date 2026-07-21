using System;
using UnityEngine;

namespace AIRGAP.Facility
{
    public struct ReportRequest
    {
        public string GuardId;
        public string Tag;        // "live-sighting" | "recovering-from-attack" | ...
        public Vector2 Position;  // where the reporting guard is
    }

    /// <summary>
    /// The report-generation intake (Phase 5 stub). Guards enqueue report
    /// requests here; Phase 6 renders the queue on Guard Comms and Phase 7/8
    /// turn requests into streamed text. Until then it logs — the WAKE-UP
    /// report contract matters now: it fires unconditionally, whether or not
    /// the Warden ever ran a status check.
    /// </summary>
    public static class ReportPipeline
    {
        public static event Action<ReportRequest> ReportQueued;

        public static void Enqueue(ReportRequest request)
        {
            Debug.Log($"[AIRGAP] REPORT guard={request.GuardId} tag={request.Tag} " +
                      $"pos=({request.Position.x:F1},{request.Position.y:F1})");
            ReportQueued?.Invoke(request);
        }

        public static void Reset() => ReportQueued = null;
    }
}
