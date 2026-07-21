using System;
using System.Collections.Generic;
using System.IO;
using AIRGAP.Facility;
using AIRGAP.Facility.Blueprints;
using AIRGAP.Facility.Guards;
using AIRGAP.Facility.Lighting;
using AIRGAP.Infiltrator;
using AIRGAP.Shared.Data;
using AIRGAP.Shared.Greybox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using LightAnchor = AIRGAP.Facility.Blueprints.LightAnchor;

namespace AIRGAP.CI
{
    /// <summary>
    /// Generates the Phase 3 Blueprint 01 scene from the blueprint data files:
    /// floors/walls from the raster, doors + sealed entrances per the role
    /// assignment, anchored lights, deterministic dressing props, exterior
    /// security (camera/floodlight markers, gravel zones), static guards, the
    /// player, and patrol-route visuals. Fully file-driven — regenerate with:
    /// Unity -batchmode -nographics -projectPath . -executeMethod AIRGAP.CI.BlueprintScene.Create -logFile -
    /// </summary>
    public static class BlueprintScene
    {
        public const string ScenePath = "Assets/Scenes/Blueprint01.unity";
        public const string BlueprintBaseName = AssignmentData.DefaultBlueprintBaseName;
        private const string WhiteSpritePath = "Assets/Shared/Greybox/white.png";
        private const string CircleSpritePath = "Assets/Shared/Greybox/circle.png";

        public const int WallsLayer = GreyboxScene.WallsLayer;
        public const int ActorsLayer = GreyboxScene.ActorsLayer;

        private static Sprite _white;
        private static Sprite _circle;
        private static Material _lit;
        private static Material _unlit;

        private static Blueprint _bp;
        private static BlueprintRaster _raster;
        private static RoleAssignment _assignment;
        private static DressingLibrary _dressings;

        // Palette.
        private static readonly Color CorridorTint = new Color(0.16f, 0.17f, 0.19f);
        private static readonly Color DefaultFloorTint = new Color(0.24f, 0.24f, 0.26f);
        private static readonly Color YardTint = new Color(0.09f, 0.08f, 0.06f);
        private static readonly Color DuctTint = new Color(0.10f, 0.11f, 0.13f);
        private static readonly Color WallGrey = new Color(0.42f, 0.43f, 0.47f);
        private static readonly Color GrateGreen = new Color(0.35f, 1f, 0.45f);
        private static readonly Color DoorUnlocked = new Color(0.48f, 0.62f, 0.48f);
        private static readonly Color DoorLocked = new Color(0.85f, 0.65f, 0.20f);
        private static readonly Color DoorBadgeGated = new Color(0.85f, 0.25f, 0.25f);
        private static readonly Color SealedYellow = new Color(0.45f, 0.40f, 0.10f);
        private static readonly Color EntranceAccent = new Color(0.25f, 0.95f, 0.85f);
        private static readonly Color GroundsPropTint = new Color(0.35f, 0.32f, 0.27f);
        private static readonly Color GravelTint = new Color(0.17f, 0.15f, 0.11f);

        public static void Create()
        {
            int exitCode = 0;
            try
            {
                BuildAll();
                Debug.Log($"[AIRGAP.CI] BlueprintScene OK — {ScenePath} written");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIRGAP.CI] BlueprintScene FAIL: {e}");
                exitCode = 1;
            }
            // Exit only after try/catch — never from inside try (settings persist on exit).
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }

        private static void BuildAll()
        {
            EnsureLayers();
            _white = EnsureWhiteSprite();
            _circle = EnsureCircleSprite();
            _lit = FindMaterial("Sprite-Lit-Default");
            _unlit = FindMaterial("Sprite-Unlit-Default");

            _bp = Blueprint.LoadFromResources(BlueprintBaseName);
            _assignment = AssignmentData.LoadDefaultAssignment();
            _dressings = AssignmentData.LoadDefaultDressings();
            _raster = new BlueprintRaster(_bp);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            int floors = BuildFloorsAndYard();
            int ventRuns = BuildVents();
            int walls = BuildWalls();
            int doors = BuildDoorsAndEntrances(out int sealedCount);
            int lights = BuildLights();
            int props = BuildProps();
            BuildSecurity(out int cameras, out int floodlights, out int gravel);
            int guards = BuildGuards();
            GameObject player = BuildPlayer();
            BuildCameraAndHud(player.transform);
            int paths = BuildPatrolPaths();

            var debugGo = new GameObject("BlueprintDebug");
            debugGo.AddComponent<BlueprintDebug>().SetBlueprint(BlueprintBaseName);

            var runtimeGo = new GameObject("FacilityRuntime");
            runtimeGo.AddComponent<FacilityRuntime>().SetBlueprint(BlueprintBaseName);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new Exception($"failed to save {ScenePath}");
            AssetDatabase.SaveAssets();

            Debug.Log("[AIRGAP.CI] BlueprintScene counts — " +
                $"floors:{floors} ventRuns:{ventRuns} walls:{walls} doors:{doors} sealed:{sealedCount} " +
                $"lights:{lights} props:{props} cameras:{cameras} floodlights:{floodlights} " +
                $"gravel:{gravel} guards:{guards} patrolPaths:{paths}");
        }

        // ---- coordinate helpers ---------------------------------------------
        // Blueprint data space is x-right, y-DOWN; Unity world flips y. A data
        // rect (x, y, w, h) becomes world center (x + w/2, -(y + h/2)); sizes
        // stay positive. Rotations negate: worldDeg = -dataDeg.

        private static Vector3 World(float dataX, float dataY) =>
            Blueprint.ToWorld(new Vector2(dataX, dataY));

        private static Vector3 DataRectCenterWorld(float x, float y, float w, float h) =>
            World(x + w * 0.5f, y + h * 0.5f);

        private static string RoleOf(string roomId)
        {
            if (_assignment.Vaults.Contains(roomId)) return "vault";
            if (_assignment.Power == roomId) return "power";
            if (_assignment.Ops == roomId) return "ops";
            return null;
        }

        // ---- floors ----------------------------------------------------------

        private static int BuildFloorsAndYard()
        {
            FenceInfo fence = _bp.Grounds.Fence;
            Block("Yard", DataRectCenterWorld(fence.X, fence.Y, fence.Width, fence.Height),
                new Vector2(fence.Width, fence.Height), YardTint, false, 0, -12, _lit);

            int count = 0;
            foreach (BlueprintSpace space in _bp.Spaces)
            {
                Color tint = space.IsCorridor ? CorridorTint : FloorTintOf(space);
                for (int i = 0; i < space.Rects.Count; i++)
                {
                    float[] r = space.Rects[i];
                    Block($"Floor_{space.Id}_{i}", DataRectCenterWorld(r[0], r[1], r[2], r[3]),
                        new Vector2(r[2], r[3]), tint, false, 0, -10, _lit);
                    count++;
                }
            }
            return count;
        }

        private static Color FloorTintOf(BlueprintSpace space)
        {
            Dressing dressing = _dressings.Resolve(space.Id, RoleOf(space.Id));
            if (dressing?.FloorTint == null || dressing.FloorTint.Length < 3) return DefaultFloorTint;
            return new Color(dressing.FloorTint[0], dressing.FloorTint[1], dressing.FloorTint[2]);
        }

        // ---- vents -----------------------------------------------------------

        private static int BuildVents()
        {
            List<RectInt> runs = _raster.MergedRuns(RasterCell.VentFloor);
            for (int i = 0; i < runs.Count; i++)
            {
                Rect d = _raster.CellRectToDataRect(runs[i]);
                var size = new Vector2(d.width, d.height);
                GameObject go = Block($"VentRun_{i:00}",
                    DataRectCenterWorld(d.x, d.y, d.width, d.height), size, DuctTint, false, 0, -8, _lit);
                var box = go.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = size;
                go.AddComponent<TraversalZone>();
            }

            for (int i = 0; i < _raster.VentGrates.Count; i++)
            {
                Vector2 p = _raster.VentGrates[i];
                var go = new GameObject($"VentGrate_{i:00}");
                go.transform.position = World(p.x, p.y);
                go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = _circle;
                renderer.color = GrateGreen;
                renderer.material = _unlit;
                renderer.sortingOrder = 2;
            }
            return runs.Count;
        }

        // ---- walls -----------------------------------------------------------
        // MergedWallRuns yields building walls, the fence (with gate gaps),
        // grounds props, and vent flanks in one pass.

        private static int BuildWalls()
        {
            List<RectInt> runs = _raster.MergedWallRuns();
            for (int i = 0; i < runs.Count; i++)
            {
                Rect d = _raster.CellRectToDataRect(runs[i]);
                Block($"Wall_{i:000}", DataRectCenterWorld(d.x, d.y, d.width, d.height),
                    new Vector2(d.width, d.height), WallGrey, true, WallsLayer, 0, _lit);
            }
            return runs.Count;
        }

        // ---- doors + entrances -----------------------------------------------

        private static int BuildDoorsAndEntrances(out int sealedCount)
        {
            foreach (BlueprintDoor door in _bp.Doors)
            {
                Vector3 center;
                Vector2 size;
                if (door.Orientation == "h")
                {
                    center = World(door.X + door.Length * 0.5f, door.Y);
                    size = new Vector2(door.Length, 0.25f);
                }
                else
                {
                    center = World(door.X, door.Y + door.Length * 0.5f);
                    size = new Vector2(0.25f, door.Length);
                }
                Color color;
                switch (_assignment.DoorTypeOf(door.Id))
                {
                    case "badge-gated": color = DoorBadgeGated; break;
                    case "locked": color = DoorLocked; break;
                    default: color = DoorUnlocked; break;
                }
                Block($"Door_{door.Id}", center, size, color, false, 0, 1, _unlit);
            }

            sealedCount = 0;
            foreach (BlueprintEntrance entrance in _bp.Entrances)
            {
                bool open = _assignment.OpenEntrances.Contains(entrance.Id);
                bool horizontalCarve = entrance.Facing == "n" || entrance.Facing == "s";
                Vector2 carveSize = horizontalCarve
                    ? new Vector2(entrance.Width, 1f)
                    : new Vector2(1f, entrance.Width);
                Vector3 center = World(entrance.X, entrance.Y);

                if (!open && entrance.Kind == "door")
                {
                    // The raster carved every entrance; sealing is our job. Wall-layer
                    // collider block + hazard dark-yellow cover over the carve.
                    Block($"Sealed_{entrance.Id}", center, carveSize, SealedYellow, true, WallsLayer, 1, _lit);
                    sealedCount++;
                }
                else if (open)
                {
                    Vector2 markerSize = entrance.Kind == "hatch"
                        ? new Vector2(entrance.Width * 0.6f, entrance.Width * 0.6f)
                        : (horizontalCarve ? new Vector2(entrance.Width, 0.3f) : new Vector2(0.3f, entrance.Width));
                    Block($"Entrance_{entrance.Id}", center, markerSize, EntranceAccent, false, 0, 1, _unlit);
                }
            }
            return _bp.Doors.Count;
        }

        // ---- lights ----------------------------------------------------------

        private static int BuildLights()
        {
            var globalGo = new GameObject("Light_GlobalNight");
            var global = globalGo.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 0.14f;
            global.color = new Color(0.55f, 0.6f, 0.9f);

            int count = 0;
            foreach (KeyValuePair<string, SpaceAnchors> kv in _bp.Anchors)
            {
                List<LightAnchor> sources = kv.Value?.LightSources;
                if (sources == null) continue;
                foreach (LightAnchor anchor in sources)
                {
                    var go = new GameObject($"Light_{kv.Key}_{anchor.Id}");
                    go.transform.position = World(anchor.X, anchor.Y);
                    // Data-space facing angles negate under the y flip.
                    if (anchor.ConeDeg > 0f)
                        go.transform.rotation = Quaternion.Euler(0f, 0f, -anchor.FacingDeg);

                    var light = go.AddComponent<AirgapLight>();
                    light.Configure(anchor.Intensity, anchor.Range, anchor.ConeDeg, 0f, 0f, true);

                    KindPreset(anchor.Kind, out Color color, out float visualIntensity);
                    AddLight2D(go.transform, anchor.Range, anchor.ConeDeg, color, visualIntensity);
                    count++;
                }
            }
            return count;
        }

        private static void KindPreset(string kind, out Color color, out float visualIntensity)
        {
            switch (kind)
            {
                case "highbay": color = new Color(1f, 0.96f, 0.88f); visualIntensity = 0.9f; break;
                case "lamp": color = new Color(1f, 0.88f, 0.65f); visualIntensity = 1.0f; break;
                case "desk": color = new Color(1f, 0.85f, 0.60f); visualIntensity = 0.7f; break;
                case "monitor": color = new Color(0.4f, 0.85f, 1f); visualIntensity = 0.8f; break;
                case "window-spill": color = new Color(0.6f, 0.7f, 1f); visualIntensity = 0.7f; break;
                case "flood": color = new Color(0.95f, 0.95f, 1f); visualIntensity = 1.1f; break;
                case "emergency": color = new Color(1f, 0.25f, 0.2f); visualIntensity = 0.5f; break;
                default: color = Color.white; visualIntensity = 0.8f; break;
            }
        }

        // ---- props -----------------------------------------------------------
        // Deterministic dressing blocks on a 2-tile grid, margin 1.2 from the
        // rect edges, skipping cells near the room's anchors or any door carve;
        // every Nth eligible cell survives (N from propDensity).

        private static int BuildProps()
        {
            var doorCarves = new List<Rect>();
            foreach (BlueprintDoor door in _bp.Doors)
            {
                doorCarves.Add(door.Orientation == "h"
                    ? new Rect(door.X, door.Y - 0.5f, door.Length, 1f)
                    : new Rect(door.X - 0.5f, door.Y, 1f, door.Length));
            }

            int total = 0;
            foreach (BlueprintSpace space in _bp.Spaces)
            {
                if (space.IsCorridor) continue;
                Dressing dressing = _dressings.Resolve(space.Id, RoleOf(space.Id));
                if (dressing == null || dressing.PropDensity <= 0f ||
                    string.IsNullOrEmpty(dressing.PropStyle) || dressing.PropStyle == "none")
                    continue;

                List<Vector2> anchorPoints = CollectAnchorPoints(space.Id);
                Color tint = PropStyleTint(dressing.PropStyle);
                int every = Mathf.Max(1, Mathf.RoundToInt(1f / dressing.PropDensity));
                int seed = StableHash(space.Id);
                int cellIndex = 0;
                int placed = 0;

                foreach (float[] r in space.Rects)
                {
                    const float margin = 1.2f;
                    for (float py = r[1] + margin; py <= r[1] + r[3] - margin + 1e-3f; py += 2f)
                    for (float px = r[0] + margin; px <= r[0] + r[2] - margin + 1e-3f; px += 2f)
                    {
                        var p = new Vector2(px, py);
                        if (NearAnyPoint(p, anchorPoints, 1.2f)) continue;
                        if (NearAnyRect(p, doorCarves, 1.2f)) continue;
                        cellIndex++;
                        if (cellIndex % every != 0) continue;

                        float t = (Mix(seed, placed) % 1000) / 999f;
                        float size = 0.8f + 0.8f * t;
                        Block($"Prop_{space.Id}_{placed}", World(px, py),
                            new Vector2(size, size), tint, true, WallsLayer, 1, _lit);
                        placed++;
                        total++;
                    }
                }
            }

            // Grounds props: the raster already walled them — sprites only, no colliders.
            for (int i = 0; i < _bp.Grounds.Props.Count; i++)
            {
                PropInfo prop = _bp.Grounds.Props[i];
                Block($"GroundsProp_{i:00}", DataRectCenterWorld(prop.X, prop.Y, prop.Width, prop.Height),
                    new Vector2(prop.Width, prop.Height), GroundsPropTint, false, 0, 1, _lit);
            }
            return total;
        }

        private static List<Vector2> CollectAnchorPoints(string spaceId)
        {
            var points = new List<Vector2>();
            if (_bp.Anchors == null || !_bp.Anchors.TryGetValue(spaceId, out SpaceAnchors anchors) ||
                anchors == null)
                return points;
            if (anchors.ItemAnchors != null)
                foreach (ItemAnchor a in anchors.ItemAnchors) points.Add(new Vector2(a.X, a.Y));
            if (anchors.LightSources != null)
                foreach (LightAnchor a in anchors.LightSources) points.Add(new Vector2(a.X, a.Y));
            if (anchors.SensorMounts != null)
                foreach (SensorMount a in anchors.SensorMounts) points.Add(new Vector2(a.X, a.Y));
            if (anchors.SabotageFixtureMounts != null)
                foreach (SabotageMount a in anchors.SabotageFixtureMounts) points.Add(new Vector2(a.X, a.Y));
            return points;
        }

        private static Color PropStyleTint(string style)
        {
            switch (style)
            {
                case "racks": return new Color(0.30f, 0.34f, 0.40f);
                case "machinery": return new Color(0.40f, 0.30f, 0.24f);
                case "desks": return new Color(0.42f, 0.38f, 0.30f);
                case "crates": return new Color(0.36f, 0.33f, 0.28f);
                default: return new Color(0.35f, 0.35f, 0.35f);
            }
        }

        private static bool NearAnyPoint(Vector2 p, List<Vector2> points, float radius)
        {
            foreach (Vector2 q in points)
                if (Vector2.Distance(p, q) <= radius) return true;
            return false;
        }

        private static bool NearAnyRect(Vector2 p, List<Rect> rects, float radius)
        {
            foreach (Rect r in rects)
            {
                float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
                float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
                if (dx * dx + dy * dy <= radius * radius) return true;
            }
            return false;
        }

        /// <summary>Deterministic string hash (string.GetHashCode is not contractual).</summary>
        private static int StableHash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in s) h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }

        private static int Mix(int seed, int index)
        {
            unchecked
            {
                int h = seed * 31 + index * 17;
                h ^= h >> 13;
                h *= unchecked((int)0x5bd1e995);
                h ^= h >> 15;
                return h & 0x7fffffff;
            }
        }

        // ---- security --------------------------------------------------------

        private static void BuildSecurity(out int cameras, out int floodlights, out int gravel)
        {
            cameras = 0;
            floodlights = 0;
            gravel = 0;
            SecurityInfo security = _bp.Grounds.Security;
            if (security == null) return;

            if (security.Cameras != null)
            {
                foreach (SecurityCone cam in security.Cameras)
                {
                    var go = new GameObject($"Camera_{cam.Id}");
                    go.transform.position = World(cam.X, cam.Y);
                    go.transform.rotation = Quaternion.Euler(0f, 0f, -cam.FacingDeg);
                    Visual(go.transform, "Body", new Vector2(0.5f, 0.5f),
                        new Color(0.9f, 0.2f, 0.2f), 3, _unlit);
                    AddStaticFan(go.transform, cam.FovDeg, cam.Range,
                        new Color(0.9f, 0.2f, 0.2f, 0.15f));
                    cameras++;
                }
            }

            if (security.Floodlights != null)
            {
                foreach (SecurityCone flood in security.Floodlights)
                {
                    var go = new GameObject($"Floodlight_{flood.Id}");
                    go.transform.position = World(flood.X, flood.Y);
                    go.transform.rotation = Quaternion.Euler(0f, 0f, -flood.FacingDeg);
                    var body = new GameObject("Body");
                    body.transform.SetParent(go.transform, false);
                    body.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
                    var renderer = body.AddComponent<SpriteRenderer>();
                    renderer.sprite = _circle;
                    renderer.color = new Color(0.95f, 0.75f, 0.25f);
                    renderer.material = _unlit;
                    renderer.sortingOrder = 3;
                    AddRayBurst(go.transform, flood.FovDeg, flood.Range,
                        new Color(0.95f, 0.75f, 0.25f, 0.3f));
                    floodlights++;
                }
            }

            if (security.Gravel != null)
            {
                for (int i = 0; i < security.Gravel.Count; i++)
                {
                    GravelInfo g = security.Gravel[i];
                    var size = new Vector2(g.Width, g.Height);
                    GameObject go = Block($"Gravel_{i}",
                        DataRectCenterWorld(g.X, g.Y, g.Width, g.Height), size,
                        GravelTint, false, 0, -9, _lit);
                    var box = go.AddComponent<BoxCollider2D>();
                    box.isTrigger = true;
                    box.size = size;
                    go.AddComponent<GravelZone>();
                    gravel++;
                }
            }
        }

        /// <summary>Static local-space FOV wedge (marker only), like GuardVision's cone fan.</summary>
        private static void AddStaticFan(Transform parent, float fovDegrees, float range, Color color)
        {
            var go = new GameObject("FovWedge");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.material = _unlit;
            line.useWorldSpace = false;
            line.loop = true;
            line.startWidth = line.endWidth = 0.03f;
            line.startColor = line.endColor = color;
            line.sortingOrder = 2;

            const int segments = 12;
            line.positionCount = segments + 2;
            line.SetPosition(0, Vector3.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Deg2Rad * (-fovDegrees * 0.5f + fovDegrees * i / segments);
                line.SetPosition(1 + i,
                    new Vector3(Mathf.Cos(angle) * range, Mathf.Sin(angle) * range, 0f));
            }
        }

        /// <summary>Radial ray marker: spokes across the FOV, each drawn out-and-back.</summary>
        private static void AddRayBurst(Transform parent, float fovDegrees, float range, Color color)
        {
            var go = new GameObject("Rays");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.material = _unlit;
            line.useWorldSpace = false;
            line.loop = false;
            line.startWidth = line.endWidth = 0.02f;
            line.startColor = line.endColor = color;
            line.sortingOrder = 2;

            const int rays = 5;
            line.positionCount = rays * 2;
            for (int i = 0; i < rays; i++)
            {
                float angle = Mathf.Deg2Rad * (-fovDegrees * 0.5f + fovDegrees * i / (rays - 1));
                line.SetPosition(i * 2, Vector3.zero);
                line.SetPosition(i * 2 + 1,
                    new Vector3(Mathf.Cos(angle) * range, Mathf.Sin(angle) * range, 0f));
            }
        }

        // ---- actors ----------------------------------------------------------

        private static int BuildGuards()
        {
            int count = 0;
            foreach (GuardDuty duty in _assignment.Guards)
            {
                Vector3 position;
                Vector2 facing;
                if (duty.Duty == "post")
                {
                    BlueprintGuardPost post = _bp.GuardPosts.Find(p => p.Id == duty.Post);
                    if (post == null)
                        throw new InvalidOperationException($"guard {duty.Id}: unknown post '{duty.Post}'");
                    position = World(post.X, post.Y);
                    facing = Quaternion.Euler(0f, 0f, -post.FacingDeg) * Vector2.right;
                    GameObject guardGo = BuildGuard(duty.Id, position, facing);
                    // Post room role feeds the high-security-post order predicate.
                    BlueprintSpace room = _bp.SpaceAt(new Vector2(post.X, post.Y));
                    guardGo.GetComponent<GuardAgent>().SetPost(position, facing, RoleOf(room?.Id));
                }
                else
                {
                    BlueprintPatrol patrol = _bp.Patrols.Find(p => p.Id == duty.Patrol);
                    if (patrol == null || patrol.Waypoints == null || patrol.Waypoints.Count < 2)
                        throw new InvalidOperationException($"guard {duty.Id}: unknown/short patrol '{duty.Patrol}'");
                    float[] w0 = patrol.Waypoints[0];
                    float[] w1 = patrol.Waypoints[1];
                    position = World(w0[0], w0[1]);
                    facing = ((Vector2)(World(w1[0], w1[1]) - position)).normalized;
                    GameObject guardGo = BuildGuard(duty.Id, position, facing);
                    var waypoints = new Vector2[patrol.Waypoints.Count];
                    for (int i = 0; i < waypoints.Length; i++)
                        waypoints[i] = World(patrol.Waypoints[i][0], patrol.Waypoints[i][1]);
                    guardGo.GetComponent<GuardAgent>().SetPatrol(waypoints, patrol.Closed);
                }
                count++;
            }
            return count;
        }

        private static GameObject BuildGuard(string guardId, Vector3 position, Vector2 facing)
        {
            var guard = new GameObject($"Guard_{guardId}") { layer = ActorsLayer };
            guard.transform.position = position;
            guard.transform.right = facing;
            var body = guard.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            var collider = guard.AddComponent<CircleCollider2D>();
            collider.radius = 0.4f;
            guard.AddComponent<GuardMarker>().SetGuardId(guardId);
            Visual(guard.transform, "Body", new Vector2(0.9f, 0.9f), new Color(0.3f, 0.5f, 0.95f), 5);

            var coneGo = new GameObject("VisionCone");
            coneGo.transform.SetParent(guard.transform, false);
            var coneLine = coneGo.AddComponent<LineRenderer>();
            coneLine.material = _unlit;
            coneLine.useWorldSpace = false;
            coneLine.loop = true;
            coneLine.startWidth = coneLine.endWidth = 0.04f;
            coneLine.sortingOrder = 9;
            guard.AddComponent<GuardVision>().SetConeRenderer(coneLine);
            guard.AddComponent<GuardHearing>();
            guard.AddComponent<GuardAgent>(); // duty wired by the caller; guards start Relaxed
            return guard;
        }

        private static GameObject BuildPlayer()
        {
            SpawnPoint spawn = _bp.Grounds.Spawn
                ?? throw new Exception("blueprint grounds has no spawn point");
            var player = new GameObject("Infiltrator") { layer = ActorsLayer };
            player.transform.position = World(spawn.X, spawn.Y);

            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            var collider = player.AddComponent<CircleCollider2D>();
            collider.radius = 0.35f;

            Visual(player.transform, "Body", new Vector2(0.8f, 0.8f), new Color(0.25f, 0.9f, 0.4f), 5);

            var ringGo = new GameObject("NoiseRing");
            ringGo.transform.SetParent(player.transform, false);
            var line = ringGo.AddComponent<LineRenderer>();
            line.material = _unlit;
            line.startColor = line.endColor = new Color(0.2f, 1f, 0.3f, 0.5f);
            line.startWidth = line.endWidth = 0.05f;
            line.sortingOrder = 10;
            var ring = ringGo.AddComponent<NoiseRing>();

            FlashlightConfig flashlightConfig = GameConfig.Load().Flashlight;
            var flashlightGo = new GameObject("Flashlight");
            flashlightGo.transform.SetParent(player.transform, false);
            var beam = flashlightGo.AddComponent<AirgapLight>();
            beam.Configure(flashlightConfig.BeamIntensity, flashlightConfig.BeamRange,
                flashlightConfig.BeamAngleDegrees, flashlightConfig.SelfGlowLevel,
                flashlightConfig.SelfGlowRadius, true);
            AddLight2D(flashlightGo.transform, flashlightConfig.BeamRange,
                flashlightConfig.BeamAngleDegrees, new Color(1f, 0.95f, 0.8f), 1.2f);

            var controller = player.AddComponent<InfiltratorController>();
            controller.SetNoiseRing(ring);
            controller.SetFlashlight(flashlightGo.transform);
            return player;
        }

        private static void BuildCameraAndHud(Transform player)
        {
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.07f);
            cameraGo.transform.position = new Vector3(player.position.x, player.position.y, -10f);
            cameraGo.AddComponent<CameraFollow>().SetTarget(player);

            new GameObject("Debug HUD").AddComponent<GreyboxHud>();
        }

        // ---- patrol routes (visual only; movement is Phase 4) ------------------

        private static int BuildPatrolPaths()
        {
            foreach (BlueprintPatrol patrol in _bp.Patrols)
            {
                if (patrol.Waypoints == null || patrol.Waypoints.Count < 2) continue;
                var go = new GameObject($"PatrolPath_{patrol.Id}");
                var line = go.AddComponent<LineRenderer>();
                line.material = _unlit;
                line.useWorldSpace = true;
                line.loop = patrol.Closed;
                line.startWidth = line.endWidth = 0.06f;
                line.startColor = line.endColor = new Color(0.45f, 0.55f, 0.9f, 0.28f);
                line.sortingOrder = -5;
                line.positionCount = patrol.Waypoints.Count;
                for (int i = 0; i < patrol.Waypoints.Count; i++)
                    line.SetPosition(i, World(patrol.Waypoints[i][0], patrol.Waypoints[i][1]));
            }
            return _bp.Patrols.Count;
        }

        // ---- scene-building helpers (mirror GreyboxScene) -----------------------

        /// <summary>
        /// Visual twin on a child rotated so the Light2D spot axis (+Y) lines up
        /// with the gameplay cone axis (+X of the parent).
        /// </summary>
        private static void AddLight2D(Transform parent, float range, float coneDegrees,
            Color color, float intensity)
        {
            var go = new GameObject("Light2D");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightInnerRadius = 0.1f;
            light.pointLightOuterRadius = range;
            light.color = color;
            light.intensity = intensity;
            light.shadowIntensity = 0.9f; // respect ShadowCaster2D walls — no beams through walls
            if (coneDegrees > 0f)
            {
                light.pointLightInnerAngle = coneDegrees * 0.7f;
                light.pointLightOuterAngle = coneDegrees;
            }
            else
            {
                light.pointLightInnerAngle = 360f;
                light.pointLightOuterAngle = 360f;
            }
        }

        private static GameObject Block(string name, Vector3 position, Vector2 size, Color color,
            bool solid, int layer, int sortingOrder, Material material)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.position = position;
            Visual(go.transform, "Sprite", size, color, sortingOrder, material);
            if (solid)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = size;
                // Solid greybox = visual occluder: block Light2D like the walls
                // already block AirgapLight's gameplay linecasts.
                Shadow2D.AddRectCaster(go, size.x, size.y);
            }
            return go;
        }

        private static void Visual(Transform parent, string name, Vector2 size, Color color,
            int sortingOrder, Material material = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _white;
            renderer.color = color;
            renderer.material = material != null ? material : _lit;
            renderer.sortingOrder = sortingOrder;
        }

        private static void EnsureLayers()
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(WallsLayer).stringValue = "Walls";
            layers.GetArrayElementAtIndex(ActorsLayer).stringValue = "Actors";
            tagManager.ApplyModifiedProperties();
        }

        private static Sprite EnsureWhiteSprite()
        {
            if (!File.Exists(WhiteSpritePath))
            {
                var texture = new Texture2D(8, 8);
                var pixels = new Color32[64];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(WhiteSpritePath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(WhiteSpritePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(WhiteSpritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 8;
                importer.filterMode = FilterMode.Point;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
            if (sprite == null) throw new Exception("white sprite failed to import");
            return sprite;
        }

        private static Sprite EnsureCircleSprite()
        {
            if (!File.Exists(CircleSpritePath))
            {
                const int size = 32;
                var texture = new Texture2D(size, size);
                var pixels = new Color32[size * size];
                float center = (size - 1) * 0.5f;
                float radius = size * 0.5f - 1f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center, dy = y - center;
                    bool inside = dx * dx + dy * dy <= radius * radius;
                    pixels[y * size + x] = inside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 0);
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(CircleSpritePath, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(CircleSpritePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(CircleSpritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = size;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
            if (sprite == null) throw new Exception("circle sprite failed to import");
            return sprite;
        }

        private static Material FindMaterial(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{name} t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                    return AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            throw new Exception($"material not found: {name}");
        }
    }
}
