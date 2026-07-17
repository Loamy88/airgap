using UnityEngine;

namespace AIRGAP.Facility.Blueprints
{
    /// <summary>
    /// Editor-only gizmo overlay for a generated blueprint scene. Placed by the
    /// scene loader (AIRGAP.CI.BlueprintScene); holds the blueprint base name and,
    /// when selected, re-reads the data files (cached) and draws the authored
    /// structure — role-eligible rooms, anchors, guard posts, patrols, vents,
    /// peeks — converted from data space (y-down) to world space (y-up).
    /// Strictly read-only and exception-safe: never mutates the scene.
    /// </summary>
    public class BlueprintDebug : MonoBehaviour
    {
        [SerializeField] private string blueprintBaseName = "blueprint01";

        public void SetBlueprint(string baseName) => blueprintBaseName = baseName;

#if UNITY_EDITOR
        private static Blueprint _cached;
        private static string _cachedName;

        private void OnDrawGizmosSelected()
        {
            try
            {
                Blueprint bp = LoadCached();
                if (bp == null) return;
                DrawEligibleRooms(bp);
                DrawAnchors(bp);
                DrawGuardPosts(bp);
                DrawPatrols(bp);
                DrawVents(bp);
                DrawPeeks(bp);
            }
            catch
            {
                // Gizmos are best-effort — a bad/missing data file must never
                // spam the console or break scene view interaction.
            }
        }

        private Blueprint LoadCached()
        {
            if (string.IsNullOrEmpty(blueprintBaseName)) return null;
            if (_cachedName != blueprintBaseName)
            {
                // Record the attempt BEFORE loading: on failure the swallow-all catch
                // above would otherwise re-parse both JSON files on every scene-view
                // repaint, silently. One failed parse per name, not one per frame.
                _cachedName = blueprintBaseName;
                _cached = null;
                _cached = Blueprint.LoadFromResources(blueprintBaseName);
            }
            return _cached;
        }

        // ---- data -> world ---------------------------------------------------

        private static Vector3 World(float x, float y) =>
            Blueprint.ToWorld(new Vector2(x, y));

        private static Vector3 RectCenterWorld(float[] r) =>
            World(r[0] + r[2] * 0.5f, r[1] + r[3] * 0.5f);

        /// <summary>Data-space facing angles negate under the y flip.</summary>
        private static Vector3 FacingDir(float dataDeg)
        {
            float rad = -dataDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
        }

        // ---- layers ----------------------------------------------------------

        private static void DrawEligibleRooms(Blueprint bp)
        {
            if (bp.Spaces == null) return;
            foreach (BlueprintSpace space in bp.Spaces)
            {
                if (!space.IsRoleEligible || space.Rects == null) continue;
                Gizmos.color = RoleTint(space);
                foreach (float[] r in space.Rects)
                    Gizmos.DrawWireCube(RectCenterWorld(r), new Vector3(r[2], r[3], 0.1f));
            }
        }

        private static Color RoleTint(BlueprintSpace space)
        {
            if (space.EligibleRoles != null)
            {
                foreach (string role in space.EligibleRoles)
                {
                    switch (role)
                    {
                        case "vault": return new Color(1f, 0.8f, 0.2f);
                        case "power": return new Color(1f, 0.35f, 0.2f);
                        case "ops": return new Color(0.7f, 0.4f, 1f);
                    }
                }
            }
            return new Color(0.7f, 0.7f, 0.7f);
        }

        private static void DrawAnchors(Blueprint bp)
        {
            if (bp.Anchors == null) return;
            foreach (SpaceAnchors anchors in bp.Anchors.Values)
            {
                if (anchors == null) continue;
                if (anchors.ItemAnchors != null)
                {
                    Gizmos.color = Color.yellow;
                    foreach (ItemAnchor a in anchors.ItemAnchors)
                        Gizmos.DrawSphere(World(a.X, a.Y), 0.2f);
                }
                if (anchors.SensorMounts != null)
                {
                    Gizmos.color = Color.cyan;
                    foreach (SensorMount m in anchors.SensorMounts)
                        Gizmos.DrawCube(World(m.X, m.Y), new Vector3(0.35f, 0.35f, 0.35f));
                }
                if (anchors.SabotageFixtureMounts != null)
                {
                    Gizmos.color = Color.red;
                    foreach (SabotageMount m in anchors.SabotageFixtureMounts)
                        Gizmos.DrawCube(World(m.X, m.Y), new Vector3(0.35f, 0.35f, 0.35f));
                }
            }
        }

        private static void DrawGuardPosts(Blueprint bp)
        {
            if (bp.GuardPosts == null) return;
            Gizmos.color = new Color(0.3f, 0.5f, 1f);
            foreach (BlueprintGuardPost post in bp.GuardPosts)
            {
                Vector3 p = World(post.X, post.Y);
                Gizmos.DrawSphere(p, 0.3f);
                Gizmos.DrawLine(p, p + FacingDir(post.FacingDeg) * 1.5f);
            }
        }

        private static void DrawPatrols(Blueprint bp)
        {
            if (bp.Patrols == null) return;
            Gizmos.color = new Color(0.4f, 0.6f, 1f);
            foreach (BlueprintPatrol patrol in bp.Patrols)
            {
                if (patrol.Waypoints == null || patrol.Waypoints.Count < 2) continue;
                for (int i = 0; i < patrol.Waypoints.Count - 1; i++)
                    Gizmos.DrawLine(
                        World(patrol.Waypoints[i][0], patrol.Waypoints[i][1]),
                        World(patrol.Waypoints[i + 1][0], patrol.Waypoints[i + 1][1]));
                if (patrol.Closed)
                {
                    int last = patrol.Waypoints.Count - 1;
                    Gizmos.DrawLine(
                        World(patrol.Waypoints[last][0], patrol.Waypoints[last][1]),
                        World(patrol.Waypoints[0][0], patrol.Waypoints[0][1]));
                }
            }
        }

        private static void DrawVents(Blueprint bp)
        {
            if (bp.Vents == null) return;
            Gizmos.color = Color.green;
            foreach (BlueprintVent vent in bp.Vents)
            {
                if (vent.Points == null || vent.Points.Count < 2) continue;
                for (int i = 0; i < vent.Points.Count - 1; i++)
                    Gizmos.DrawLine(
                        World(vent.Points[i][0], vent.Points[i][1]),
                        World(vent.Points[i + 1][0], vent.Points[i + 1][1]));
            }
        }

        private static void DrawPeeks(Blueprint bp)
        {
            Gizmos.color = Color.white;
            if (bp.InteriorPeeks != null)
                foreach (BlueprintPeek peek in bp.InteriorPeeks)
                    DrawPeekRay(peek);
            if (bp.Entrances != null)
                foreach (BlueprintEntrance entrance in bp.Entrances)
                    if (entrance.Peek != null) DrawPeekRay(entrance.Peek);
        }

        private static void DrawPeekRay(BlueprintPeek peek)
        {
            Vector3 p = World(peek.X, peek.Y);
            Gizmos.DrawLine(p, p + FacingDir(peek.FacingDeg) * 1.2f);
        }
#endif
    }
}
