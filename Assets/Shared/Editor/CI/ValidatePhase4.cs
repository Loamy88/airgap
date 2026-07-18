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
    /// Phase 4 validator: the order plausibility predicates (pure), the
    /// executability gate, source-blindness of EvaluateOrder, transient TTL
    /// revert, and scene-simulated guard behavior — patrol following, sound →
    /// memory → Investigate, chase de-escalation, and both capture rules.
    /// Run via: Unity -batchmode -nographics -projectPath . -executeMethod AIRGAP.CI.ValidatePhase4.Run -logFile -
    /// </summary>
    public static class ValidatePhase4
    {
        private const float Dt = 0.02f;
        private static readonly List<string> Errors = new List<string>();
        private static InfiltratorController _player;
        private static ScriptedMovementInput _input;

        public static void Run()
        {
            Errors.Clear();
            bool passed = false;
            try
            {
                EditorSceneManager.OpenScene(BlueprintScene.ScenePath, OpenSceneMode.Single);
                Physics2D.simulationMode = SimulationMode2D.Script;
                GameConfig.Invalidate();
                // NO SoundBus.Reset() here: the scene's guards subscribed on load
                // (ExecuteAlways OnEnable) and the hearing tests need them wired.
                CaptureSystem.Reset();
                OrderService.Reset();
                TraversalZone.Rebuild();
                AirgapLight.Rebuild();

                _player = Object.FindFirstObjectByType<InfiltratorController>();
                if (_player == null) throw new System.Exception("no InfiltratorController in Blueprint01 scene");
                _input = new ScriptedMovementInput();
                _player.SetInput(_input);
                _player.EnsureInitialized();

                foreach (GuardAgent agent in Object.FindObjectsByType<GuardAgent>(FindObjectsSortMode.None))
                {
                    agent.EnsureInitialized();
                    var hearing = agent.GetComponent<GuardHearing>();
                    if (hearing != null) hearing.SetRandom(new System.Random(100 + agent.GuardId.GetHashCode() % 50));
                }
                Check(GuardAgent.All.Count == 5, $"5 guard agents live in the scene (got {GuardAgent.All.Count})");

                PureOrderRules();
                GateAndSourceRules();
                PatrolFollowing();
                SoundMemoryInvestigate();
                CloseRangeClearCapture();
                EscalationCapture();
                AlarmedChaseCapture();

                passed = LogReport();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AIRGAP.CI] ValidatePhase4 exception: {e}");
                passed = false;
            }
            finally
            {
                Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
                SoundBus.Reset();
                CaptureSystem.Reset();
                OrderService.Reset();
            }
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        }

        // ---- Section A: pure order rules ------------------------------------

        private static OrderContext CleanContext() => new OrderContext
        {
            SightWithinTwoSeconds = false,
            PostRoomRole = "filler",
            ChannelOpen = true,
            SecondsSinceAcceptedOrder = 999f,
            Ladder = GuardAlertState.Unaware
        };

        private static void PureOrderRules()
        {
            const float debounce = 5f;

            OrderContext ctx = CleanContext();
            Check(GuardOrderRules.EvaluateOrder(ctx, Order.Of(OrderType.Investigate), debounce).Obey,
                "clean context: Investigate obeyed");
            Check(GuardOrderRules.EvaluateOrder(ctx, Order.GuardPoint("GP1"), debounce).Obey,
                "clean context: GuardPoint obeyed");

            ctx = CleanContext(); ctx.SightWithinTwoSeconds = true;
            Check(GuardOrderRules.EvaluateOrder(ctx, Order.Of(OrderType.Disregard), debounce).Reason == QueryReason.LiveSighting,
                "predicate 1: cannot Disregard what you are looking at");
            Check(GuardOrderRules.EvaluateOrder(ctx, Order.Of(OrderType.HoldPosition), debounce).Reason == QueryReason.LiveSighting,
                "predicate 1: cannot Hold what you are looking at");
            Check(GuardOrderRules.EvaluateOrder(ctx, Order.Of(OrderType.Investigate), debounce).Obey,
                "predicate 1 scopes only to Disregard/Hold (Investigate with live sight obeyed)");

            ctx = CleanContext(); ctx.PostRoomRole = "vault";
            Check(GuardOrderRules.EvaluateOrder(ctx, Order.GuardPoint("GP9"), debounce).Reason == QueryReason.HighSecurityPost,
                "predicate 2: vault post not abandoned (GuardPoint)");
            Check(GuardOrderRules.EvaluateOrder(ctx, Order.Patrol("P1"), debounce).Reason == QueryReason.HighSecurityPost,
                "predicate 2: vault post not abandoned (Patrol)");
            Check(GuardOrderRules.EvaluateOrder(ctx, Order.SetAlertness(GuardAlertness.Heightened), debounce).Obey,
                "predicate 2 scopes only to movement deployments (SetAlertness on vault post obeyed)");

            ctx = CleanContext(); ctx.ChannelOpen = false;
            Check(GuardOrderRules.EvaluateOrder(ctx, Order.Of(OrderType.Investigate), debounce).Reason == QueryReason.NoOpenChannel,
                "predicate 3: response order needs an open channel");
            Check(GuardOrderRules.EvaluateOrder(ctx, Order.GuardPoint("GP1"), debounce).Obey,
                "predicate 3 scopes only to response orders (deployment fine on closed channel)");

            ctx = CleanContext(); ctx.SecondsSinceAcceptedOrder = 1f;
            Check(GuardOrderRules.EvaluateOrder(ctx, Order.GuardPoint("GP1"), debounce).Reason == QueryReason.JustOrdered,
                "predicate 4: debounce queries a stacked order");

            ctx = CleanContext(); ctx.Ladder = GuardAlertState.Alarmed;
            Check(GuardOrderRules.EvaluateOrder(ctx, Order.SetAlertness(GuardAlertness.Relaxed), debounce).Reason == QueryReason.Alarmed,
                "predicate 5: alarmed guards take no orders");

            ctx = CleanContext(); ctx.SightWithinTwoSeconds = true; ctx.Ladder = GuardAlertState.Alarmed;
            Check(GuardOrderRules.EvaluateOrder(ctx, Order.Of(OrderType.Disregard), debounce).Reason == QueryReason.LiveSighting,
                "predicates evaluate in authored order (LiveSighting before Alarmed)");
        }

        private static void GateAndSourceRules()
        {
            GuardAgent g02 = GuardAgent.FindById("G-02");
            GuardAgent g04 = GuardAgent.FindById("G-04");
            Check(g02 != null && g04 != null, "G-02 and G-04 present for gate/source tests");
            if (g02 == null || g04 == null) return;

            List<OrderType> offerable = g02.OfferableOrders();
            Check(offerable.Contains(OrderType.GuardPoint) && offerable.Contains(OrderType.Patrol) &&
                  offerable.Contains(OrderType.SetAlertness) && offerable.Contains(OrderType.WhosNearYou),
                "gate: deployments + WhosNearYou always offerable");
            Check(!offerable.Contains(OrderType.Investigate),
                "gate: Investigate not offerable without a located memory event");
            Check(!offerable.Contains(OrderType.HoldPosition) && !offerable.Contains(OrderType.Disregard),
                "gate: Hold/Disregard not offerable below Suspicious");
            Check(!offerable.Contains(OrderType.SayAgain), "gate: SayAgain not offerable with the channel closed");

            OrderResult gateResult = OrderService.IssueOrder("G-02", Order.Of(OrderType.Investigate), OrderSource.Warden);
            Check(!gateResult.Offered, "gate: issuing an un-offerable order is rejected before plausibility");

            // Source blindness + durability: identical fresh guards, same order,
            // different source -> same decision; only the slot durability differs.
            OrderResult warden = OrderService.IssueOrder("G-02", Order.SetAlertness(GuardAlertness.Standard), OrderSource.Warden);
            OrderResult spoofed = OrderService.IssueOrder("G-04", Order.SetAlertness(GuardAlertness.Standard), OrderSource.Spoofed);
            Check(warden.Offered && spoofed.Offered && warden.Decision.Obey && spoofed.Decision.Obey,
                "source blindness: Warden and Spoofed get identical decisions on identical state");
            Check(g04.Orders.TransientActive, "spoofed deployment applied as TRANSIENT (never rewrites the board)");
            Check(!g02.Orders.TransientActive, "warden deployment does not occupy the transient slot");

            // Transient TTL revert.
            float ttl = GameConfig.Load().Guard.Orders.TransientTtlSeconds;
            Step(ttl + 0.5f);
            Check(!g04.Orders.TransientActive, "transient order expired after its TTL and reverted to standing");
        }

        // ---- Section B: scene simulation ------------------------------------

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

        private static void MovePlayer(Vector2 worldPos)
        {
            _player.transform.position = worldPos;
            _player.GetComponent<Rigidbody2D>().position = worldPos;
        }

        private static void PatrolFollowing()
        {
            GuardAgent g01 = GuardAgent.FindById("G-01");
            Check(g01 != null && g01.Duty == DutyMode.Patrol, "G-01 starts on patrol duty");
            if (g01 == null) return;

            Vector2 last = g01.Position;
            float travelled = 0f;
            for (int i = 0; i < 500; i++) // 10 seconds
            {
                Step(Dt);
                travelled += Vector2.Distance(g01.Position, last);
                last = g01.Position;
            }
            Check(travelled > 8f, $"G-01 walks its authored ring loop (travelled {travelled:F1} tiles in 10s)");
            Check(g01.Ladder.State == GuardAlertState.Unaware, "patrolling guard stays Unaware with no stimuli");
        }

        private static void SoundMemoryInvestigate()
        {
            GuardAgent g02 = GuardAgent.FindById("G-02");
            if (g02 == null) return;

            // Heightened baseline makes the point-blank notice deterministic
            // (p clamps to 1) — restored to Relaxed at the end of the test.
            g02.SetBaseline(GuardAlertness.Heightened);
            Vector2 noisePos = g02.Position + new Vector2(3f, 0f);
            SoundBus.Emit(new SoundEvent("test-noise", 1f, noisePos, "validator"));
            SoundBus.Emit(new SoundEvent("test-noise", 1f, noisePos, "validator"));
            Step(0.1f);

            Check(g02.Memory.TryLatestLocatedEvent(out MemoryEntry entry) && entry.Type == MemoryEventType.Sound,
                "noticed sound lands in the memory buffer with a position");
            Check(g02.Ladder.State >= GuardAlertState.Suspicious,
                $"two point-blank noises raise at least Suspicious (got {g02.Ladder.State}, s={g02.Ladder.Suspicion:F2})");
            Check(g02.OfferableOrders().Contains(OrderType.Investigate),
                "gate: Investigate becomes offerable once memory holds a located event");

            g02.ChannelOpen = true;
            OrderResult result = OrderService.IssueOrder("G-02", Order.Of(OrderType.Investigate), OrderSource.Warden);
            Check(result.Decision.Obey, "Investigate obeyed on an open channel");
            float before = Vector2.Distance(g02.Position, entry.Position);
            Step(2f);
            float after = Vector2.Distance(g02.Position, entry.Position);
            Check(after < before - 0.5f, $"guard moves toward the investigated noise ({before:F1} -> {after:F1})");
            g02.ChannelOpen = false;
            g02.SetBaseline(GuardAlertness.Relaxed);
        }

        private static void CloseRangeClearCapture()
        {
            GuardAgent g04 = GuardAgent.FindById("G-04");
            if (g04 == null) return;
            Check(g04.Ladder.State == GuardAlertState.Unaware, "G-04 Unaware before the close-range test");

            // Stand 0.9 tiles in front of the guard's face with the flashlight on:
            // clear sighting inside capture range must capture at ANY rung.
            Vector2 inFront = g04.Position + (Vector2)g04.transform.right * 0.9f;
            MovePlayer(inFront);
            if (!_player.FlashlightOn) { _input.QueueFlashlightToggle(); }
            Step(0.3f);
            Check(CaptureSystem.IsCaptured && CaptureSystem.Reason.Contains("close-range"),
                $"close-range clear sighting captures from Unaware (captured={CaptureSystem.IsCaptured})");
            CaptureSystem.Reset();
            MovePlayer(new Vector2(-6f, -24f)); // back to the yard, far from everyone
            if (_player.FlashlightOn) { _input.QueueFlashlightToggle(); Step(0.05f); }
            Step(8f); // let G-04's ladder decay back down
            Check(GuardAgent.FindById("G-04").Ladder.State <= GuardAlertState.Suspicious,
                "G-04's ladder decays after losing the target");
        }

        private static void EscalationCapture()
        {
            // Post guards have authored, stable facings — deterministic placement.
            // G-04 stands at its C-S1 post facing west; stand lit 2.5 tiles ahead:
            // the ladder must climb from Unaware and the guard must come get you.
            GuardAgent g04 = GuardAgent.FindById("G-04");
            if (g04 == null) return;

            Vector2 ahead = g04.Position + (Vector2)g04.transform.right * 2.5f;
            MovePlayer(ahead);
            if (!_player.FlashlightOn) { _input.QueueFlashlightToggle(); }

            bool sawSuspicious = false;
            for (int i = 0; i < 500 && !CaptureSystem.IsCaptured; i++)
            {
                Step(Dt);
                if (g04.Ladder.State >= GuardAlertState.Suspicious) sawSuspicious = true;
            }
            Check(sawSuspicious, "escalation passed through Suspicious");
            Check(CaptureSystem.IsCaptured, "escalating guard closes and captures a lit, close intruder");
            CaptureSystem.Reset();
            if (_player.FlashlightOn) { _input.QueueFlashlightToggle(); Step(0.05f); }
            MovePlayer(new Vector2(-6f, -24f));
        }

        private static void AlarmedChaseCapture()
        {
            // The Alarmed chase path, deterministically: force the ladder (the
            // escalation route to Alarmed races the close-range rule by design)
            // and verify chase movement + capture + order refusal.
            GuardAgent g02 = GuardAgent.FindById("G-02");
            if (g02 == null) return;

            Vector2 target = g02.Position + new Vector2(-4f, 0f); // west, inside R26's open floor
            MovePlayer(target);
            if (!_player.FlashlightOn) { _input.QueueFlashlightToggle(); Step(0.05f); } // stay visible: chase follows sight
            g02.Ladder.ForceState(GuardAlertState.Alarmed, 3.5f);

            OrderResult refused = OrderService.IssueOrder("G-02", Order.SetAlertness(GuardAlertness.Relaxed), OrderSource.Warden);
            Check(refused.Offered && !refused.Decision.Obey && refused.Decision.Reason == QueryReason.Alarmed,
                "alarmed guard refuses orders through the full IssueOrder path");

            float before = Vector2.Distance(g02.Position, _player.Position);
            bool sawChase = false;
            for (int i = 0; i < 250 && !CaptureSystem.IsCaptured; i++)
            {
                Step(Dt);
                if (g02.Duty == DutyMode.Chase) sawChase = true;
            }
            Check(sawChase, "alarmed guard enters Chase duty");
            Check(CaptureSystem.IsCaptured, $"alarmed chase closes to melee and captures (started {before:F1} tiles away)");
            CaptureSystem.Reset();
        }

        // ---- plumbing --------------------------------------------------------

        private static void Check(bool condition, string description)
        {
            if (condition) Debug.Log($"[AIRGAP.CI] ok: {description}");
            else Errors.Add(description);
        }

        private static bool LogReport()
        {
            if (Errors.Count == 0)
            {
                Debug.Log("[AIRGAP.CI] ValidatePhase4 PASS");
                return true;
            }
            foreach (string error in Errors) Debug.LogError($"[AIRGAP.CI] FAIL: {error}");
            Debug.LogError($"[AIRGAP.CI] ValidatePhase4 FAIL — {Errors.Count} error(s)");
            return false;
        }
    }
}
