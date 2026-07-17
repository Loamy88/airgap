using System.Collections.Generic;
using UnityEngine;

namespace AIRGAP.Facility
{
    /// <summary>
    /// Marks an exterior gravel patch. The Infiltrator controller polls Contains()
    /// each tick — footsteps on gravel are forced loud ("gravel-footstep"). Polling
    /// instead of trigger callbacks keeps play mode and batchmode validators
    /// identical, same as TraversalZone.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Collider2D))]
    public class GravelZone : MonoBehaviour
    {
        private static readonly List<GravelZone> Registry = new List<GravelZone>();

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
            foreach (GravelZone zone in Object.FindObjectsByType<GravelZone>(FindObjectsSortMode.None))
            {
                zone._collider = zone.GetComponent<Collider2D>();
                Registry.Add(zone);
            }
        }

        public static bool Contains(Vector2 worldPoint)
        {
            foreach (GravelZone zone in Registry)
            {
                if (zone != null && zone._collider != null && zone._collider.OverlapPoint(worldPoint))
                    return true;
            }
            return false;
        }
    }
}
