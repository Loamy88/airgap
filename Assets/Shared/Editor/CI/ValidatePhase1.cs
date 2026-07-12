using System.Collections.Generic;
using AIRGAP.Facility;
using AIRGAP.Infiltrator;
using AIRGAP.Shared.Data;
using AIRGAP.Shared.Events;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AIRGAP.CI
{
    /// <summary>
    /// Phase 1 validator: loads the grey-box scene headless, drives the Infiltrator
    /// through a scripted movement sequence (walk, sprint, crouch, vent enter/exit)
    /// by stepping Physics2D manually, and asserts stance transitions, config-driven
    /// speeds, footstep events, and the vent shortcut actually crossing the divider.
    /// Run via: Unity -batchmode -nographics -projectPath . -executeMethod AIRGAP.CI.ValidatePhase1.Run -logFile -
    /// </summary>
    public static class ValidatePhase1
    {
        private const float Dt = 0.02f;
        private static readonly List<string> Errors = new List<string>();

        public static void Run()
        {
            Errors.Clear();
            SimulationMode2D previousMode = Physics2D.simulationMode;
            try
            {
                EditorSceneManager.OpenScene(GreyboxScene.ScenePath, OpenSceneMode.Single);
                Physics2D.simulationMode = SimulationMode2D.Script;
                GameConfig.Invalidate();
                SoundBus.Reset();
                TraversalZone.Rebuild();

                var player = Object.FindFirstObjectByType<InfiltratorController>();
                if (player == null)
                {
                    Fail("no InfiltratorController in scene");
                    return;
                }

                var body = player.GetComponent<Rigidbody2D>();
                var input = new ScriptedMovementInput();
                player.SetInput(input);
                player.EnsureInitialized();
                InfiltratorConfig config = GameConfig.Load().Infiltrator;

                var heardEvents = new List<SoundEvent>();
                SoundBus.Emitted += e => heardEvents.Add(e);

                // --- walk right ---------------------------------------------------
                heardEvents.Clear();
                Vector2 start = body.position;
                input.Move = Vector2.right;
                Step(player, 1.2f);
                Check(player.Stance.Current == Stance.Walk, $"stance Walk while moving (got {player.Stance.Current})");
                CheckSpeed("walk", body.position.x - start.x, config.WalkSpeed * 1.2f);
                Check(heardEvents.Count >= 2, $"footsteps emitted while walking (got {heardEvents.Count})");
                Check(heardEvents.Count > 0 && Mathf.Approximately(heardEvents[0].Loudness, config.WalkLoudness),
                    "walk footstep loudness matches config");

                // --- sprint left --------------------------------------------------
                heardEvents.Clear();
                start = body.position;
                input.Move = Vector2.left;
                input.SprintHeld = true;
                Step(player, 1.0f);
                Check(player.Stance.Current == Stance.Sprint, $"stance Sprint while sprinting (got {player.Stance.Current})");
                CheckSpeed("sprint", start.x - body.position.x, config.SprintSpeed * 1.0f);
                Check(heardEvents.TrueForAll(e => e.Type == "sprint-footstep"), "sprint footsteps typed sprint-footstep");
                input.SprintHeld = false;

                // --- crouch up ----------------------------------------------------
                start = body.position;
                input.QueueCrouchToggle();
                input.Move = Vector2.up;
                Step(player, 1.0f);
                Check(player.Stance.Current == Stance.Crouch, $"stance Crouch after toggle (got {player.Stance.Current})");
                CheckSpeed("crouch", body.position.y - start.y, config.CrouchSpeed * 1.0f);

                // --- vent entry via left shaft ------------------------------------
                heardEvents.Clear();
                body.position = new Vector2(-0.875f, 3.2f);
                input.Move = Vector2.up;
                Step(player, 1.2f);
                Check(player.Stance.Current == Stance.Traversal, $"stance Traversal inside vent (got {player.Stance.Current})");
                Check(player.InTraversal, "InTraversal flag set inside vent");
                Check(body.position.y > 4.25f, $"climbed into duct (y={body.position.y:F2})");

                // --- crawl through the duct, across the divider -------------------
                input.Move = Vector2.right;
                Step(player, 2.5f);
                Check(player.Stance.Current == Stance.Traversal, "still Traversal mid-duct");
                Check(body.position.x > 1.75f, $"vent shortcut crossed the divider (x={body.position.x:F2})");
                Check(heardEvents.Exists(e => Mathf.Approximately(e.Loudness, config.TraversalLoudness)),
                    "traversal footsteps use traversal loudness");

                // --- exit via right shaft -----------------------------------------
                Step(player, 1.6f); // keep crawling to the right end of the duct
                input.Move = Vector2.down;
                Step(player, 1.6f);
                Check(!player.InTraversal, $"left the vent (pos={body.position})");
                Check(player.Stance.Current == Stance.Crouch,
                    $"crouch latch persists after vent exit (got {player.Stance.Current})");
                Check(body.position.y < 3.6f, $"descended into room B (y={body.position.y:F2})");

                Report("ValidatePhase1");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AIRGAP.CI] ValidatePhase1 exception: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
            finally
            {
                Physics2D.simulationMode = previousMode;
                SoundBus.Reset();
            }
        }

        private static void Step(InfiltratorController player, float seconds)
        {
            int ticks = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < ticks; i++)
            {
                player.Tick(Dt);
                Physics2D.Simulate(Dt);
            }
        }

        private static void CheckSpeed(string label, float displacement, float expected)
        {
            // Allow slack for acceleration-free motion quantized to ticks.
            bool ok = displacement > expected * 0.85f && displacement < expected * 1.1f;
            Check(ok, $"{label} displacement {displacement:F2} ≈ expected {expected:F2}");
        }

        private static void Check(bool condition, string description)
        {
            if (condition) Debug.Log($"[AIRGAP.CI] ok: {description}");
            else Errors.Add(description);
        }

        private static void Fail(string description) => Errors.Add(description);

        private static void Report(string name)
        {
            if (Errors.Count == 0)
            {
                Debug.Log($"[AIRGAP.CI] {name} PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                foreach (string error in Errors) Debug.LogError($"[AIRGAP.CI] FAIL: {error}");
                Debug.LogError($"[AIRGAP.CI] {name} FAIL — {Errors.Count} error(s)");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
