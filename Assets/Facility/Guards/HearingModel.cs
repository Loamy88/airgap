using AIRGAP.Shared.Data;
using UnityEngine;

namespace AIRGAP.Facility.Guards
{
    /// <summary>
    /// The probabilistic hearing curve — the single most important tuning surface
    /// in the stealth model (DEVELOPMENT.md Phase 17). Pure function, no state:
    /// every sound event rolls independently, failed rolls are not remembered.
    /// Machines (Phase 9 sensors) never call this — they trigger deterministically.
    /// </summary>
    public static class HearingModel
    {
        public static float NoticeProbability(float loudness, float distance, float alertnessMultiplier, GuardConfig config)
        {
            if (loudness <= 0f) return 0f;

            float falloff = distance <= config.HearingReferenceDistance
                ? 1f
                : Mathf.Pow(config.HearingReferenceDistance / distance, 2f);

            float probability = loudness * falloff * alertnessMultiplier;

            // "Nowhere is zero" — no distance where a noise is safe, no edge to stand outside of.
            return Mathf.Clamp(probability, config.HearingProbabilityFloor, 1f);
        }
    }
}
