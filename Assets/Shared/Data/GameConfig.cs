using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace AIRGAP.Shared.Data
{
    public class InfiltratorConfig
    {
        public float CrouchSpeed, WalkSpeed, SprintSpeed, TraversalSpeed;
        public float CrouchLoudness, WalkLoudness, SprintLoudness, TraversalLoudness;
        public float CrouchStepInterval, WalkStepInterval, SprintStepInterval;
        public float NoiseRingScale;
        public float GravelLoudness;
    }

    public class LightingConfig
    {
        public float GlobalNightLevel;
        public float DimThreshold;
        public float LitThreshold;
    }

    public class GuardConfig
    {
        public float VisionRange;
        public float ConeAngleDegrees;
        public float ClearFraction, PartialFraction;
        public float ShadowRangeMultiplier, DimRangeMultiplier, LitRangeMultiplier;
        public float HearingReferenceDistance;
        public float HearingProbabilityFloor;
        public float RelaxedMultiplier, StandardMultiplier, HeightenedMultiplier;
        public float PatrolSpeed, InvestigateSpeed, ChaseSpeed;
        public float CloseRangeCaptureDistance;
        public LadderConfig Ladder;
        public DutyConfig Duty;
        public OrdersConfig Orders;
        public float MemoryBufferSeconds;
    }

    public class LadderConfig
    {
        public float NoneVision, SilhouetteVision, PartialVision, ClearVision; // suspicion/sec by category
        public float NoticedSoundBump;
        public float DecayPerSecond, DecayDelaySeconds;
        public float SuspiciousThreshold, SearchingThreshold, AlarmedThreshold;
        public bool AlarmedRequiresSight;
        public float RelaxedRate, StandardRate, HeightenedRate;
    }

    public class DutyConfig
    {
        public float WaypointTolerance;
        public float PatrolPauseSeconds;
        public float InvestigateSeconds;
        public float SearchGiveUpSeconds;
        public float ChaseLoseSightSeconds;
        public float MeleeReachTiles;
    }

    public class OrdersConfig
    {
        public float TransientTtlSeconds;
        public float DeploymentLatencySeconds;
        public float DebounceSeconds;
    }

    public class FlashlightConfig
    {
        public float BeamAngleDegrees;
        public float BeamRange;
        public float BeamIntensity;
        public float SelfGlowLevel;
        public float SelfGlowRadius;
    }

    /// <summary>
    /// Runtime view of the JSON design data in Assets/Shared/Resources/Data.
    /// Loaded once via Resources; numbers live in the JSON, never in code (see docs/CONVENTIONS.md).
    /// </summary>
    public class GameConfig
    {
        public InfiltratorConfig Infiltrator;
        public LightingConfig Lighting;
        public GuardConfig Guard;
        public FlashlightConfig Flashlight;

        private static GameConfig _cached;

        public static void Invalidate() => _cached = null;

        public static GameConfig Load()
        {
            if (_cached != null) return _cached;

            JObject facility = ParseResource("Data/facility");
            JObject guards = ParseResource("Data/guards");
            JObject gadgets = ParseResource("Data/gadgets");

            var config = new GameConfig
            {
                Infiltrator = new InfiltratorConfig
                {
                    CrouchSpeed = F(facility, "infiltrator.moveSpeeds.crouch"),
                    WalkSpeed = F(facility, "infiltrator.moveSpeeds.walk"),
                    SprintSpeed = F(facility, "infiltrator.moveSpeeds.sprint"),
                    TraversalSpeed = F(facility, "infiltrator.traversal.speed"),
                    CrouchLoudness = F(facility, "infiltrator.noiseLoudness.crouch"),
                    WalkLoudness = F(facility, "infiltrator.noiseLoudness.walk"),
                    SprintLoudness = F(facility, "infiltrator.noiseLoudness.sprint"),
                    TraversalLoudness = F(facility, "infiltrator.traversal.noiseLoudness"),
                    CrouchStepInterval = F(facility, "infiltrator.footstepIntervalSeconds.crouch"),
                    WalkStepInterval = F(facility, "infiltrator.footstepIntervalSeconds.walk"),
                    SprintStepInterval = F(facility, "infiltrator.footstepIntervalSeconds.sprint"),
                    NoiseRingScale = F(facility, "infiltrator.noiseRingScale"),
                    GravelLoudness = F(facility, "infiltrator.gravelLoudness")
                },
                Lighting = new LightingConfig
                {
                    GlobalNightLevel = F(facility, "lighting.globalNightLevel"),
                    DimThreshold = F(facility, "lighting.dimThreshold"),
                    LitThreshold = F(facility, "lighting.litThreshold")
                },
                Guard = new GuardConfig
                {
                    VisionRange = F(guards, "vision.range"),
                    ConeAngleDegrees = F(guards, "vision.coneAngleDegrees"),
                    ClearFraction = F(guards, "vision.categoryDistanceFractions.clear"),
                    PartialFraction = F(guards, "vision.categoryDistanceFractions.partial"),
                    ShadowRangeMultiplier = F(guards, "vision.lightLevelFalloff.shadow"),
                    DimRangeMultiplier = F(guards, "vision.lightLevelFalloff.dim"),
                    LitRangeMultiplier = F(guards, "vision.lightLevelFalloff.lit"),
                    HearingReferenceDistance = F(guards, "hearing.falloff.referenceDistance"),
                    HearingProbabilityFloor = F(guards, "hearing.falloff.minProbabilityFloor"),
                    RelaxedMultiplier = F(guards, "hearing.alertnessMultipliers.relaxed"),
                    StandardMultiplier = F(guards, "hearing.alertnessMultipliers.standard"),
                    HeightenedMultiplier = F(guards, "hearing.alertnessMultipliers.heightened"),
                    PatrolSpeed = F(guards, "movement.patrolSpeed"),
                    InvestigateSpeed = F(guards, "movement.investigateSpeed"),
                    ChaseSpeed = F(guards, "movement.chaseSpeed"),
                    CloseRangeCaptureDistance = F(guards, "vision.closeRangeCaptureDistance"),
                    MemoryBufferSeconds = F(guards, "memory.bufferSeconds"),
                    Ladder = new LadderConfig
                    {
                        NoneVision = F(guards, "alertnessLadder.suspicion.visionPerSecond.none"),
                        SilhouetteVision = F(guards, "alertnessLadder.suspicion.visionPerSecond.silhouette"),
                        PartialVision = F(guards, "alertnessLadder.suspicion.visionPerSecond.partial"),
                        ClearVision = F(guards, "alertnessLadder.suspicion.visionPerSecond.clear"),
                        NoticedSoundBump = F(guards, "alertnessLadder.suspicion.noticedSoundBump"),
                        DecayPerSecond = F(guards, "alertnessLadder.suspicion.decayPerSecond"),
                        DecayDelaySeconds = F(guards, "alertnessLadder.suspicion.decayDelaySeconds"),
                        SuspiciousThreshold = F(guards, "alertnessLadder.suspicion.suspiciousThreshold"),
                        SearchingThreshold = F(guards, "alertnessLadder.suspicion.searchingThreshold"),
                        AlarmedThreshold = F(guards, "alertnessLadder.suspicion.alarmedThreshold"),
                        AlarmedRequiresSight = guards.SelectToken("alertnessLadder.suspicion.alarmedRequiresSight")?.Value<bool>() ?? true,
                        RelaxedRate = F(guards, "alertnessLadder.suspicion.baselineRateMultipliers.relaxed"),
                        StandardRate = F(guards, "alertnessLadder.suspicion.baselineRateMultipliers.standard"),
                        HeightenedRate = F(guards, "alertnessLadder.suspicion.baselineRateMultipliers.heightened")
                    },
                    Duty = new DutyConfig
                    {
                        WaypointTolerance = F(guards, "duty.waypointToleranceTiles"),
                        PatrolPauseSeconds = F(guards, "duty.patrolPauseSeconds"),
                        InvestigateSeconds = F(guards, "duty.investigateSeconds"),
                        SearchGiveUpSeconds = F(guards, "duty.searchGiveUpSeconds"),
                        ChaseLoseSightSeconds = F(guards, "duty.chaseLoseSightSeconds"),
                        MeleeReachTiles = F(guards, "duty.meleeReachTiles")
                    },
                    Orders = new OrdersConfig
                    {
                        TransientTtlSeconds = F(guards, "orders.transientDefaultTtlSeconds"),
                        DeploymentLatencySeconds = F(guards, "orders.deploymentExecutionLatencySeconds"),
                        DebounceSeconds = F(guards, "orders.debounceSeconds")
                    }
                },
                Flashlight = LoadFlashlight(gadgets)
            };

            _cached = config;
            return config;
        }

        private static FlashlightConfig LoadFlashlight(JObject gadgets)
        {
            foreach (JToken item in (JArray)gadgets["standardKit"])
            {
                if ((string)item["id"] != "flashlight") continue;
                return new FlashlightConfig
                {
                    BeamAngleDegrees = (float)item["beamAngleDegrees"],
                    BeamRange = (float)item["beamRange"],
                    BeamIntensity = (float)item["beamIntensity"],
                    SelfGlowLevel = (float)item["selfGlowLevel"],
                    SelfGlowRadius = (float)item["selfGlowRadius"]
                };
            }
            throw new InvalidOperationException("gadgets.json has no flashlight entry in standardKit");
        }

        private static JObject ParseResource(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException($"config resource not found: {path}");
            return JObject.Parse(asset.text);
        }

        private static float F(JObject json, string path)
        {
            JToken token = json.SelectToken(path);
            if (token == null) throw new InvalidOperationException($"missing config field: {path}");
            return token.Value<float>();
        }
    }
}
