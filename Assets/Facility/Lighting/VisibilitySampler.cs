using AIRGAP.Shared.Data;
using UnityEngine;

namespace AIRGAP.Facility.Lighting
{
    public enum LightCategory
    {
        Shadow,
        Dim,
        Lit
    }

    /// <summary>
    /// "Is this tile in shadow or light right now" — the light-level meter and the
    /// input to guard vision falloff. Level = max over sources (light pools don't
    /// stack), floored by the global night baseline, thresholds from facility.json.
    /// </summary>
    public static class VisibilitySampler
    {
        private static int _wallsMask = -1;

        private static int WallsMask
        {
            get
            {
                if (_wallsMask < 0) _wallsMask = LayerMask.GetMask("Walls");
                return _wallsMask;
            }
        }

        public static float SampleAt(Vector2 point)
        {
            LightingConfig config = GameConfig.Load().Lighting;
            float level = config.GlobalNightLevel;
            foreach (AirgapLight light in AirgapLight.All)
            {
                if (light == null) continue;
                level = Mathf.Max(level, light.ContributionAt(point, WallsMask));
            }
            return Mathf.Clamp01(level);
        }

        public static LightCategory Categorize(float level)
        {
            LightingConfig config = GameConfig.Load().Lighting;
            if (level >= config.LitThreshold) return LightCategory.Lit;
            if (level >= config.DimThreshold) return LightCategory.Dim;
            return LightCategory.Shadow;
        }

        public static LightCategory CategoryAt(Vector2 point) => Categorize(SampleAt(point));
    }
}
