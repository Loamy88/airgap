using System.Collections.Generic;
using UnityEngine;

namespace AIRGAP.Facility.Lighting
{
    /// <summary>
    /// The gameplay model of a light source. Visibility sampling is computed
    /// analytically from these (with wall occlusion), never read back from
    /// rendering — so it is deterministic, identical headless and in play mode,
    /// and a URP Light2D on the same object is purely the visual twin.
    ///
    /// Cone lights (coneAngleDegrees > 0) shine along transform.right. selfGlow
    /// models the pool of light around the fixture itself — for the flashlight
    /// it is what lifts the *carrier's* own light level (README: the beam is a
    /// hazard to the holder first).
    /// </summary>
    [ExecuteAlways]
    public class AirgapLight : MonoBehaviour
    {
        [SerializeField] private float intensity = 0.8f;
        [SerializeField] private float range = 5f;
        [SerializeField] private float coneAngleDegrees;
        [SerializeField] private float selfGlowLevel;
        [SerializeField] private float selfGlowRadius;
        [SerializeField] private bool occludedByWalls = true;

        private static readonly List<AirgapLight> Registry = new List<AirgapLight>();
        public static IReadOnlyList<AirgapLight> All => Registry;

        public void Configure(float lightIntensity, float lightRange, float coneDegrees,
            float glowLevel, float glowRadius, bool occluded)
        {
            intensity = lightIntensity;
            range = lightRange;
            coneAngleDegrees = coneDegrees;
            selfGlowLevel = glowLevel;
            selfGlowRadius = glowRadius;
            occludedByWalls = occluded;
        }

        private void OnEnable()
        {
            if (!Registry.Contains(this)) Registry.Add(this);
        }

        private void OnDisable()
        {
            Registry.Remove(this);
        }

        /// <summary>Edit-mode safety net for validators: rescan the open scene(s).</summary>
        public static void Rebuild()
        {
            Registry.Clear();
            foreach (AirgapLight light in Object.FindObjectsByType<AirgapLight>(FindObjectsSortMode.None))
            {
                if (light.isActiveAndEnabled) Registry.Add(light);
            }
        }

        public float ContributionAt(Vector2 point, int wallsMask)
        {
            Vector2 source = transform.position;
            float distance = Vector2.Distance(source, point);
            float value = 0f;

            if (distance <= range)
            {
                bool inBeam = true;
                if (coneAngleDegrees > 0.01f)
                {
                    Vector2 toPoint = point - source;
                    inBeam = toPoint.sqrMagnitude > 1e-6f &&
                             Vector2.Angle(transform.right, toPoint) <= coneAngleDegrees * 0.5f;
                }
                if (inBeam)
                {
                    float falloff = 1f - distance / range;
                    value = intensity * falloff * falloff;
                }
            }

            if (selfGlowLevel > 0f && distance <= selfGlowRadius)
            {
                float glow = selfGlowLevel * (1f - distance / selfGlowRadius);
                value = Mathf.Max(value, glow);
            }

            if (value <= 0f) return 0f;

            if (occludedByWalls && distance > 0.01f)
            {
                RaycastHit2D hit = Physics2D.Linecast(source, point, wallsMask);
                if (hit.collider != null) return 0f;
            }
            return value;
        }
    }
}
