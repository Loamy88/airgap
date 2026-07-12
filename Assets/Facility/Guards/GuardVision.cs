using AIRGAP.Facility.Lighting;
using AIRGAP.Infiltrator;
using AIRGAP.Shared.Data;
using UnityEngine;

namespace AIRGAP.Facility.Guards
{
    public enum VisibilityCategory
    {
        None,
        Silhouette,
        Partial,
        Clear
    }

    /// <summary>
    /// Guard vision: a cone along transform.right with distance and light-level
    /// falloff, producing a visibility category rather than a boolean. The target's
    /// sampled light level scales the effective range (a shadowed Infiltrator is
    /// near-invisible at distance); walls occlude via linecast. All thresholds from
    /// guards.json.
    /// </summary>
    [ExecuteAlways]
    public class GuardVision : MonoBehaviour
    {
        [SerializeField] private LineRenderer coneRenderer;

        private GuardConfig _config;
        private GuardMarker _marker;
        private InfiltratorController _target;
        private int _wallsMask = -1;
        private bool _initialized;

        public VisibilityCategory Current { get; private set; }
        public float EffectiveRange { get; private set; }
        public LightCategory TargetLightCategory { get; private set; }

        public string GuardId => _marker != null ? _marker.GuardId : name;

        public void SetConeRenderer(LineRenderer renderer) => coneRenderer = renderer;

        public void EnsureInitialized()
        {
            if (_initialized) return;
            _config = GameConfig.Load().Guard;
            _marker = GetComponent<GuardMarker>();
            _wallsMask = LayerMask.GetMask("Walls");
            EffectiveRange = _config.VisionRange;
            BuildConeGeometry();
            _initialized = true;
        }

        private void Update()
        {
            if (Application.isPlaying) Tick();
        }

        public VisibilityCategory Tick()
        {
            EnsureInitialized();
            if (_target == null)
            {
                _target = FindFirstObjectByType<InfiltratorController>();
                if (_target == null) return VisibilityCategory.None;
            }

            VisibilityCategory next = Compute(_target.Position);
            if (next != Current)
            {
                Debug.Log($"[AIRGAP] VISION guard={GuardId} {Current} -> {next} " +
                          $"(light={TargetLightCategory}, effRange={EffectiveRange:F1})");
                Current = next;
            }
            UpdateConeVisual();
            return Current;
        }

        private VisibilityCategory Compute(Vector2 targetPosition)
        {
            Vector2 origin = transform.position;
            Vector2 toTarget = targetPosition - origin;
            float distance = toTarget.magnitude;

            TargetLightCategory = VisibilitySampler.CategoryAt(targetPosition);
            EffectiveRange = _config.VisionRange * TargetLightCategory switch
            {
                LightCategory.Shadow => _config.ShadowRangeMultiplier,
                LightCategory.Dim => _config.DimRangeMultiplier,
                _ => _config.LitRangeMultiplier
            };

            if (distance > EffectiveRange) return VisibilityCategory.None;
            if (distance > 0.01f && Vector2.Angle(transform.right, toTarget) > _config.ConeAngleDegrees * 0.5f)
                return VisibilityCategory.None;
            if (Physics2D.Linecast(origin, targetPosition, _wallsMask).collider != null)
                return VisibilityCategory.None;

            float fraction = distance / EffectiveRange;
            if (fraction < _config.ClearFraction) return VisibilityCategory.Clear;
            if (fraction < _config.PartialFraction) return VisibilityCategory.Partial;
            return VisibilityCategory.Silhouette;
        }

        // ---- debug cone visual (scales with light-adjusted effective range) ----

        private void BuildConeGeometry()
        {
            if (coneRenderer == null) return;
            const int arcPoints = 14;
            float half = _config.ConeAngleDegrees * 0.5f * Mathf.Deg2Rad;
            coneRenderer.positionCount = arcPoints + 2;
            coneRenderer.SetPosition(0, Vector3.zero);
            for (int i = 0; i <= arcPoints; i++)
            {
                float angle = Mathf.Lerp(-half, half, i / (float)arcPoints);
                Vector3 point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * _config.VisionRange;
                coneRenderer.SetPosition(i + 1, point);
            }
        }

        private void UpdateConeVisual()
        {
            if (coneRenderer == null) return;
            float scale = _config.VisionRange > 0f ? EffectiveRange / _config.VisionRange : 1f;
            coneRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            Color color = Current switch
            {
                VisibilityCategory.Clear => new Color(1f, 0.2f, 0.15f, 0.6f),
                VisibilityCategory.Partial => new Color(1f, 0.55f, 0.1f, 0.45f),
                VisibilityCategory.Silhouette => new Color(1f, 0.9f, 0.2f, 0.3f),
                _ => new Color(0.7f, 0.7f, 0.8f, 0.12f)
            };
            coneRenderer.startColor = coneRenderer.endColor = color;
        }
    }
}
