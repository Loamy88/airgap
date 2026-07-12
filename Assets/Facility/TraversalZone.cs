using System.Collections.Generic;
using UnityEngine;

namespace AIRGAP.Facility
{
    /// <summary>
    /// Marks a vent/crawlspace region ("layerType": "traversal"). The Infiltrator
    /// controller polls Contains() each tick — entering is automatic detection,
    /// no trigger callbacks, so it behaves identically in play mode and in
    /// batchmode validators.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Collider2D))]
    public class TraversalZone : MonoBehaviour
    {
        private static readonly List<TraversalZone> Registry = new List<TraversalZone>();

        private Collider2D _collider;

        private void OnEnable()
        {
            _collider = GetComponent<Collider2D>();
            if (!Registry.Contains(this)) Registry.Add(this);
        }

        private void OnDisable()
        {
            Registry.Remove(this);
        }

        /// <summary>Edit-mode safety net: scan the open scene(s) for zones.</summary>
        public static void Rebuild()
        {
            Registry.Clear();
            foreach (TraversalZone zone in Object.FindObjectsByType<TraversalZone>(FindObjectsSortMode.None))
            {
                zone._collider = zone.GetComponent<Collider2D>();
                Registry.Add(zone);
            }
        }

        public static bool Contains(Vector2 worldPoint)
        {
            foreach (TraversalZone zone in Registry)
            {
                if (zone != null && zone._collider != null && zone._collider.OverlapPoint(worldPoint))
                    return true;
            }
            return false;
        }
    }
}
