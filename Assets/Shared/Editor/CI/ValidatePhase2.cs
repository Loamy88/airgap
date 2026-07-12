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
    /// Phase 2 validator: light sampling (levels, thresholds, wall occlusion),
    /// flashlight self-illumination, guard vision categories under light falloff,
    /// and the probabilistic hearing curve (monotonic in distance/loudness/alertness,
    /// floored, seeded-deterministic rolls). All headless — the light model is
    /// analytic, so no rendering is involved.
    /// Run via: Unity -batchmode -nographics -projectPath . -executeMethod AIRGAP.CI.ValidatePhase2.Run -logFile -
    /// </summary>
    public static class ValidatePhase2
    {
        private static readonly List<string> Errors = new List<string>();

        public static void Run()
        {
            Errors.Clear();
            try
            {
                EditorSceneManager.OpenScene(GreyboxScene.ScenePath, OpenSceneMode.Single);
                GameConfig.Invalidate();
                TraversalZone.Rebuild();

                var player = Object.FindFirstObjectByType<InfiltratorController>();
                var vision = Object.FindFirstObjectByType<GuardVision>();
                var hearing = Object.FindFirstObjectByType<GuardHearing>();
                if (player == null || vision == null || hearing == null)
                {
                    Errors.Add($"scene missing components (player={player}, vision={vision}, hearing={hearing})");
                    Report();
                    return;
                }

                var input = new ScriptedMovementInput();
                player.SetInput(input);
                player.EnsureInitialized(); // also parks the flashlight off
                AirgapLight.Rebuild();

                LightingConfig lighting = GameConfig.Load().Lighting;
                GuardConfig guardConfig = GameConfig.Load().Guard;

                // ---- light sampling ------------------------------------------------
                float lampLevel = VisibilitySampler.SampleAt(new Vector2(-5f, 2f));
                Check(VisibilitySampler.Categorize(lampLevel) == LightCategory.Lit,
                    $"lamp center reads Lit (level={lampLevel:F2})");

                float darkLevel = VisibilitySampler.SampleAt(new Vector2(2.5f, -5f));
                Check(Mathf.Abs(darkLevel - lighting.GlobalNightLevel) < 0.005f,
                    $"dark corner reads the night baseline (level={darkLevel:F2})");
                Check(VisibilitySampler.Categorize(darkLevel) == LightCategory.Shadow, "dark corner categorized Shadow");

                // Wall occlusion, with a controlled temp light against the divider.
                var tempGo = new GameObject("TempOcclusionLight");
                tempGo.transform.position = new Vector2(-1f, -4f);
                var tempLight = tempGo.AddComponent<AirgapLight>();
                tempLight.Configure(0.9f, 6f, 0f, 0f, 0f, true);
                float openSide = VisibilitySampler.SampleAt(new Vector2(-4.3f, -4f));
                float blockedSide = VisibilitySampler.SampleAt(new Vector2(2.3f, -4f));
                Check(openSide > 0.15f, $"open side of temp light illuminated (level={openSide:F2})");
                Check(Mathf.Abs(blockedSide - lighting.GlobalNightLevel) < 0.005f,
                    $"divider occludes the temp light (level={blockedSide:F2})");
                Object.DestroyImmediate(tempGo);
                AirgapLight.Rebuild();

                // ---- flashlight ----------------------------------------------------
                MovePlayer(player, new Vector2(2.5f, -5f));
                float beforeFlashlight = VisibilitySampler.SampleAt(player.Position);
                ToggleFlashlight(player, input);
                Check(player.FlashlightOn, "flashlight toggled on");
                float withFlashlight = VisibilitySampler.SampleAt(player.Position);
                Check(withFlashlight >= 0.4f && withFlashlight > beforeFlashlight + 0.3f,
                    $"flashlight raises own light level ({beforeFlashlight:F2} -> {withFlashlight:F2})");
                Check(VisibilitySampler.Categorize(withFlashlight) == LightCategory.Lit,
                    "flashlight carrier reads Lit");
                float beamAhead = VisibilitySampler.SampleAt(new Vector2(5.5f, -5f));
                Check(beamAhead > 0.15f, $"beam lights the wall it points at (level={beamAhead:F2})");
                ToggleFlashlight(player, input);
                Check(!player.FlashlightOn, "flashlight toggled back off");
                float afterOff = VisibilitySampler.SampleAt(player.Position);
                Check(Mathf.Abs(afterOff - beforeFlashlight) < 0.005f, "light level restored after flashlight off");

                // ---- guard vision --------------------------------------------------
                MovePlayer(player, new Vector2(4.5f, 0f));
                Check(vision.Tick() == VisibilityCategory.Partial,
                    $"close target in shadow reads Partial (got {vision.Current}, effRange={vision.EffectiveRange:F1})");

                MovePlayer(player, new Vector2(0f, 0f));
                Check(vision.Tick() == VisibilityCategory.None,
                    $"target behind divider reads None (got {vision.Current})");

                MovePlayer(player, new Vector2(4.5f, 0f));
                ToggleFlashlight(player, input);
                Check(vision.Tick() == VisibilityCategory.Clear,
                    $"flashlight upgrades same position to Clear (got {vision.Current})");
                ToggleFlashlight(player, input);

                MovePlayer(player, new Vector2(8f, 0f));
                Check(vision.Tick() == VisibilityCategory.None,
                    $"target behind the guard reads None (got {vision.Current})");

                MovePlayer(player, new Vector2(-7f, 0f));
                Check(vision.Tick() == VisibilityCategory.None,
                    $"target beyond max range reads None (got {vision.Current})");

                // ---- probabilistic hearing ----------------------------------------
                hearing.SetRandom(new System.Random(1234));
                hearing.SetAlertness(GuardAlertness.Standard);

                var near = hearing.EvaluateAndRecord(new SoundEvent("footstep", 0.35f, new Vector2(4f, 0f), "test"));
                var mid = hearing.EvaluateAndRecord(new SoundEvent("footstep", 0.35f, new Vector2(-2f, 0f), "test"));
                var far = hearing.EvaluateAndRecord(new SoundEvent("footstep", 0.35f, new Vector2(-9f, 0f), "test"));
                Check(Mathf.Abs(near.Probability - 0.35f) < 0.001f,
                    $"inside reference distance, p == loudness (p={near.Probability:F3})");
                Check(near.Probability > mid.Probability && mid.Probability > far.Probability,
                    $"p falls off with distance ({near.Probability:F3} > {mid.Probability:F3} > {far.Probability:F3})");
                Check(far.Probability >= guardConfig.HearingProbabilityFloor, "distant event still above the floor");

                var sprint = hearing.EvaluateAndRecord(new SoundEvent("sprint-footstep", 0.8f, new Vector2(-2f, 0f), "test"));
                Check(sprint.Probability > mid.Probability,
                    $"louder event, higher p at same distance ({sprint.Probability:F3} > {mid.Probability:F3})");

                hearing.SetAlertness(GuardAlertness.Heightened);
                var heightened = hearing.EvaluateAndRecord(new SoundEvent("footstep", 0.35f, new Vector2(-2f, 0f), "test"));
                Check(heightened.Probability > mid.Probability,
                    $"Heightened alertness raises p ({heightened.Probability:F3} > {mid.Probability:F3})");
                hearing.SetAlertness(GuardAlertness.Standard);

                var faint = hearing.EvaluateAndRecord(new SoundEvent("footstep", 0.1f, new Vector2(6f, 30f), "test"));
                Check(Mathf.Abs(faint.Probability - guardConfig.HearingProbabilityFloor) < 0.001f,
                    $"far faint event clamps to the floor — nowhere is zero (p={faint.Probability:F3})");

                var pointBlank = hearing.EvaluateAndRecord(new SoundEvent("test-noise", 1f, new Vector2(5.5f, 0f), "test"));
                Check(pointBlank.Noticed && pointBlank.Probability >= 1f, "point-blank max-loudness event always noticed");

                // Bus wiring: an emitted event reaches the guard's subscription.
                SoundBus.Emit(new SoundEvent("bus-wiring", 0.5f, new Vector2(4f, 0f), "test"));
                Check(hearing.LastResult.HasValue && hearing.LastResult.Value.Sound.Type == "bus-wiring",
                    "SoundBus emission reaches guard hearing");

                Report();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AIRGAP.CI] ValidatePhase2 exception: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
            finally
            {
                SoundBus.Reset();
            }
        }

        private static void MovePlayer(InfiltratorController player, Vector2 position)
        {
            player.transform.position = position;
            player.GetComponent<Rigidbody2D>().position = position;
        }

        private static void ToggleFlashlight(InfiltratorController player, ScriptedMovementInput input)
        {
            input.Move = Vector2.zero;
            input.QueueFlashlightToggle();
            player.Tick(0.02f);
            AirgapLight.Rebuild(); // flashlight GO active state changed
        }

        private static void Check(bool condition, string description)
        {
            if (condition) Debug.Log($"[AIRGAP.CI] ok: {description}");
            else Errors.Add(description);
        }

        private static void Report()
        {
            if (Errors.Count == 0)
            {
                Debug.Log("[AIRGAP.CI] ValidatePhase2 PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                foreach (string error in Errors) Debug.LogError($"[AIRGAP.CI] FAIL: {error}");
                Debug.LogError($"[AIRGAP.CI] ValidatePhase2 FAIL — {Errors.Count} error(s)");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
