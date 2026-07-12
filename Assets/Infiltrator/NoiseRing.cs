using UnityEngine;

namespace AIRGAP.Infiltrator
{
    /// <summary>
    /// Debug visualization of the current stance's loudness (green circle).
    /// Purely visual — hearing is a per-event roll (guards.json), not a radius.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class NoiseRing : MonoBehaviour
    {
        private const int Segments = 48;
        private LineRenderer _line;
        private float _radius = -1f;

        public void SetRadius(float radius)
        {
            if (_line == null)
            {
                _line = GetComponent<LineRenderer>();
                _line.loop = true;
                _line.useWorldSpace = false;
                _line.positionCount = Segments;
            }
            if (Mathf.Approximately(radius, _radius)) return;
            _radius = radius;

            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                _line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }
    }
}
