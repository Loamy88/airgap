using System.Collections.Generic;
using AIRGAP.Facility;
using AIRGAP.Facility.Guards;
using AIRGAP.Facility.Lighting;
using AIRGAP.Infiltrator;
using AIRGAP.Shared.Data;
using AIRGAP.Shared.Events;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AIRGAP.CI
{
    /// <summary>
    /// Phase 5 validator: consciousness machine, no-automatic-disclosure, status
    /// checks, both Badge Flag confirmation paths, flagged-badge rejection with
    /// its precise alert, the quiet-play / loud-mistake plausibility split, and
    /// the door attribution log.
    /// Run via: Unity -batchmode -nographics -projectPath . -executeMethod AIRGAP.CI.ValidatePhase5.Run -logFile -
    /// </summary>
    public static class ValidatePhase5
    {
        private const float Dt = 0.02f;
        private static readonly List<string> Errors = new List<string>();
        private static InfiltratorController _player;
        private static readonly List<ReportRequest> Reports = new List<ReportRequest>();
        private static readonly List<(string door, string badge)> BadgeAlerts = new List<(string, string)>();
        private static readonly List<(string door, string badge)> Pings = new List<(string, string)>();

        public static void Run()
        {
            Errors.Clear();
            Reports.Clear();
            BadgeAlerts.Clear();
            Pings.Clear();
            bool passed = false;
            try
            {
                EditorSceneManager.OpenScene(BlueprintScene.ScenePath, OpenSceneMode.Single);
                Physics2D.simulationMode = SimulationMode2D.Script;
                GameConfig.Invalidate();
                CaptureSystem.Reset();
                BadgeSystem.Reset();
                DoorSystem.Reset();
                ReportPipeline.Reset();
                SecurityEvents.Reset();
                TraversalZone.Rebuild();
                AirgapLight.Rebuild();

                ReportPipeline.ReportQueued += r => Reports.Add(r);
                SecurityEvents.BadgeAlert += (d, b) => BadgeAlerts.Add((d, b));
                SecurityEvents.SuspicionPing += (d, b) => Pings.Add((d, b));

                _player = Object.FindFirstObjectByType<InfiltratorController>();
                _player.SetInput(new ScriptedMovementInput());
                _player.EnsureInitialized();
                foreach (GuardAgent agent in Object.FindObjectsByType<GuardAgent>(FindObjectsSortMode.None))
                    agent.EnsureInitialized();

                var runtime = Object.FindFirstObjectByType<FacilityRuntime>();
                Check(runtime != null, "FacilityRuntime present in the scene");
                runtime.EnsureInitialized();
                Check(DoorSystem.IsInitialized, "door system initialized from blueprint + assignment");

                Registration();
                DownStateAndDisclosure();
                LootRules();
                StatusCheckFlagPath();
                FlaggedBadgeRejection();
                WakeReportFlagPath();
                QuietPlayAndLoudMistake();
                LadderIndependence();

                passed = LogReport();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AIRGAP.CI] ValidatePhase5 exception: {e}");
                passed = false;
            }
            finally
            {
                Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
                SoundBus.Reset();
                CaptureSystem.Reset();
                BadgeSystem.Reset();
                DoorSystem.Reset();
                ReportPipeline.Reset();
                SecurityEvents.Reset();
                OrderService.Reset();
            }
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void Step(float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < ticks; i++)
            {
                _player.Tick(Dt);
                foreach (GuardAgent agent in GuardAgent.All) agent.Tick(Dt);
                Physics2D.Simulate(Dt);
            }
        }

        private static void Registration()
        {
            foreach (string id in new[] { "G-01", "G-02", "G-03", "G-04", "G-05" })
            {
                BadgeRecord badge = BadgeSystem.OfOwner(id);
                Check(badge != null && badge.Holder == BadgeHolder.Owner && !badge.Flagged,
                    $"badge registered on {id} (id {badge?.BadgeId}, on body, unflagged)");
            }
        }

        private static void DownStateAndDisclosure()
        {
            GuardAgent g03 = GuardAgent.FindById("G-03");
            int reportsBefore = Reports.Count;

            g03.TakeDown(600f);
            Check(g03.Consciousness.IsDown, "TakeDown puts the guard Down");
            Check(Reports.Count == reportsBefore, "going Down pushes NO report (no automatic disclosure)");

            // Inert while down: loud noise at their feet — no memory, no ladder.
            int memoryBefore = g03.Memory.Entries.Count;
            float suspicionBefore = g03.Ladder.Suspicion;
            SoundBus.Emit(new SoundEvent("test-noise", 1f, g03.Position, "validator"));
            Step(0.1f);
            Check(g03.Memory.Entries.Count == memoryBefore, "down guard perceives no sound (memory unchanged)");
            Check(Mathf.Approximately(g03.Ladder.Suspicion, suspicionBefore), "down guard's ladder does not move");

            OrderResult order = OrderService.IssueOrder("G-03", Order.SetAlertness(GuardAlertness.Heightened), OrderSource.Warden);
            Check(!order.Offered, "down guard gives no response to orders");

            Vector2 position = g03.Position;
            Step(1f);
            Check(Vector2.Distance(position, g03.Position) < 0.05f, "down guard does not move");

            Check(GuardStatus.CheckGuardStatus("G-03") == StatusCheckResult.Unresponsive,
                "status check returns Unresponsive for a down guard");
            Check(!BadgeSystem.OfOwner("G-03").Flagged,
                "unresponsive WITH badge still on the body -> no flag (nothing is missing)");
            Check(GuardStatus.CheckGuardStatus("G-01") == StatusCheckResult.Responsive,
                "status check returns Responsive for an active guard");
        }

        private static void LootRules()
        {
            Check(!BadgeSystem.TryLoot("G-01"), "cannot loot an active guard");
            Check(BadgeSystem.TryLoot("G-03"), "looting a down guard succeeds");
            Check(!BadgeSystem.TryLoot("G-03"), "a badge can only be looted once");
            Check(BadgeSystem.OfOwner("G-03").Holder == BadgeHolder.Infiltrator, "looted badge is held by the infiltrator");
        }

        private static void StatusCheckFlagPath()
        {
            Check(!BadgeSystem.OfOwner("G-03").Flagged, "looted badge is NOT yet flagged (no confirmation since loot)");
            GuardStatus.CheckGuardStatus("G-03");
            Check(BadgeSystem.OfOwner("G-03").Flagged,
                "status check confirming Unresponsive + missing badge -> Badge Flag (path 1)");
        }

        private static void FlaggedBadgeRejection()
        {
            string badgeId = BadgeSystem.OfOwner("G-03").BadgeId;
            int alertsBefore = BadgeAlerts.Count;
            BadgeUseResult result = DoorSystem.TryBadgeUse("D37", badgeId); // R26|R10, badge-gated
            Check(result == BadgeUseResult.FlaggedBadgeRejected, "flagged badge fails on a badge-gated door");
            Check(BadgeAlerts.Count == alertsBefore + 1 && BadgeAlerts[BadgeAlerts.Count - 1].door == "D37",
                "flagged badge use pushes an immediate, precise alert");
            Check(DoorSystem.Of("D37").Open == false, "the door stays closed");
        }

        private static void WakeReportFlagPath()
        {
            GuardAgent g05 = GuardAgent.FindById("G-05");
            g05.TakeDown(2f);
            Check(BadgeSystem.TryLoot("G-05"), "looted G-05's badge while down");

            int reportsBefore = Reports.Count;
            Step(2.5f);
            Check(!g05.Consciousness.IsDown, "down timer expired -> guard wakes");
            Check(Reports.Count == reportsBefore + 1 && Reports[Reports.Count - 1].Tag == "recovering-from-attack",
                "wake-up unconditionally enqueues the incident report (no status check ever ran)");
            Check(BadgeSystem.OfOwner("G-05").Flagged,
                "wake-up report confirming the attack + missing badge -> Badge Flag (path 2)");
            Check(g05.Ladder.State == GuardAlertState.Searching, "woken guard comes up Searching");
        }

        private static void QuietPlayAndLoudMistake()
        {
            GuardAgent g02 = GuardAgent.FindById("G-02");
            g02.TakeDown(600f);
            Check(BadgeSystem.TryLoot("G-02"), "looted G-02's badge (post guard at the core anteroom)");
            string badgeId = BadgeSystem.OfOwner("G-02").BadgeId;

            // Quiet play: G-02's post is right next to D37 — matching location,
            // unconfirmed badge: opens clean, no ping, no alert.
            int pingsBefore = Pings.Count;
            int alertsBefore = BadgeAlerts.Count;
            BadgeUseResult near = DoorSystem.TryBadgeUse("D37", badgeId);
            Check(near == BadgeUseResult.Opened, "unflagged looted badge opens a door near its owner's post");
            Check(DoorSystem.Of("D37").Open && DoorSystem.Of("D37").LastOpenerBadgeId == badgeId,
                "attribution log records open state + exactly which badge opened it");
            Check(Pings.Count == pingsBefore && BadgeAlerts.Count == alertsBefore,
                "matching-location badge use raises nothing — the quiet play reads as normal");

            // Loud mistake: the same badge on the far south-west door.
            BadgeUseResult far = DoorSystem.TryBadgeUse("D20", badgeId); // C-S1|R06, ~26 tiles from GP4
            Check(far == BadgeUseResult.Opened, "far door still opens (badge is not flagged)");
            Check(Pings.Count == pingsBefore + 1 && Pings[Pings.Count - 1].door == "D20",
                "implausible location raises the passive suspicion ping (independent of any flag)");

            Check(DoorSystem.TryBadgeUse("D01", badgeId) == BadgeUseResult.NotBadgeGated,
                "badge use on a non-badge-gated door is NotBadgeGated");
        }

        private static void LadderIndependence()
        {
            GuardAgent g01 = GuardAgent.FindById("G-01");
            g01.Ladder.ForceState(GuardAlertState.Alarmed, 3.5f);
            g01.TakeDown(1f);
            Check(g01.Consciousness.IsDown, "a guard can go Down at ANY ladder rung (Alarmed included)");
            Step(1.3f);
            Check(!g01.Consciousness.IsDown && g01.Ladder.State == GuardAlertState.Searching,
                "wake resolves to Searching regardless of pre-takedown rung");
        }

        private static void Check(bool condition, string description)
        {
            if (condition) Debug.Log($"[AIRGAP.CI] ok: {description}");
            else Errors.Add(description);
        }

        private static bool LogReport()
        {
            if (Errors.Count == 0)
            {
                Debug.Log("[AIRGAP.CI] ValidatePhase5 PASS");
                return true;
            }
            foreach (string error in Errors) Debug.LogError($"[AIRGAP.CI] FAIL: {error}");
            Debug.LogError($"[AIRGAP.CI] ValidatePhase5 FAIL — {Errors.Count} error(s)");
            return false;
        }
    }
}
