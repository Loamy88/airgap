// AIRGAP — Phase 3 blueprint authoring validator.
// Headless: Unity -batchmode -nographics -projectPath . -executeMethod AIRGAP.CI.ValidatePhase3.Run
// Loads blueprint01 (structure + anchors), the fixed role assignment, and the dressing
// library, rasterizes the blueprint, then runs the 15 authoring checks from the Phase 3
// spec. Everything is computed in DATA space (x-right, y-DOWN, tiles).
// Exit 0 = pass, 1 = fail. No persisted settings are touched.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AIRGAP.Facility.Blueprints;
using UnityEditor;
using UnityEngine;

namespace AIRGAP.CI
{
    public static class ValidatePhase3
    {

        public static void Run()
        {
            var errors = new List<string>();
            try
            {
                Blueprint bp = Blueprint.LoadFromResources(AssignmentData.DefaultBlueprintBaseName);
                RoleAssignment ra = AssignmentData.LoadDefaultAssignment();
                DressingLibrary lib = AssignmentData.LoadDefaultDressings();
                var raster = new BlueprintRaster(bp);
                Debug.Log($"[AIRGAP.CI] ok: loaded blueprint '{bp.Id}' rev {bp.Rev}, assignment '{ra.Label}', " +
                          $"dressings; raster {raster.WidthCells}x{raster.HeightCells} cells");

                List<BlueprintSpace> rooms = bp.Spaces.Where(s => !s.IsCorridor).ToList();
                List<BlueprintSpace> corridors = bp.Spaces.Where(s => s.IsCorridor).ToList();

                CheckUniqueIds(bp, errors);                       // 1
                CheckSchemaSanity(bp, ra, lib, errors);           // 1b
                CheckEligibilityCensus(rooms, corridors, errors); // 2
                CheckDoors(bp, errors);                           // 3
                CheckConnectivity(bp, rooms, raster, errors);     // 4
                CheckPatrols(bp, raster, errors);                 // 5
                CheckGuardPosts(bp, raster, errors);              // 6
                CheckAnchorContainment(bp, errors);               // 7
                CheckPeeks(bp, errors);                           // 8
                CheckVents(bp, raster, errors);                   // 9
                CheckAssignmentRoles(bp, ra, errors);             // 10
                CheckBadgeDoors(bp, ra, errors);                  // 11
                CheckEntranceAssignment(bp, ra, errors);          // 12
                CheckGuardDuties(bp, ra, errors);                 // 13
                CheckAnchorRichness(bp, rooms, corridors, errors);// 14
                CheckDressings(bp, rooms, ra, lib, errors);       // 15

                LogSummary(bp, rooms, corridors, raster);
            }
            catch (Exception e)
            {
                errors.Add($"unhandled exception: {e.Message}");
                Debug.LogError($"[AIRGAP.CI] FAIL: unhandled exception: {e}");
            }
            finally
            {
                // Nothing persisted, nothing to restore — kept for the mandatory
                // try/compute, finally/cleanup, Exit-after pattern.
            }

            bool pass = errors.Count == 0;
            Debug.Log($"[AIRGAP.CI] ValidatePhase3 {(pass ? "PASS" : $"FAIL ({errors.Count} error(s))")}");
            if (Application.isBatchMode) EditorApplication.Exit(pass ? 0 : 1);
        }

        // ------------------------------------------------------------------
        // Check helpers
        // ------------------------------------------------------------------

        /// Logs "ok: label" when sub is empty, otherwise logs/collects each failure.
        static void Report(string label, List<string> sub, List<string> errors)
        {
            if (sub.Count == 0)
            {
                Debug.Log($"[AIRGAP.CI] ok: {label}");
                return;
            }
            foreach (string msg in sub)
            {
                errors.Add($"{label}: {msg}");
                Debug.LogError($"[AIRGAP.CI] FAIL: {label}: {msg}");
            }
        }

        // Single source of truth for the filler/candidate split lives on the space
        // itself (BlueprintData computes IsRoleEligible) — do not re-encode the set here.
        static bool IsFillerOnly(BlueprintSpace s) => !s.IsRoleEligible;

        /// Axis-aligned rect overlap with strict inequalities; rects are (x0,y0,x1,y1).
        static bool RectsOverlap(float ax0, float ay0, float ax1, float ay1,
                                 float bx0, float by0, float bx1, float by1) =>
            ax0 < bx1 && bx0 < ax1 && ay0 < by1 && by0 < ay1;

        /// The rect a door/opening carves in the raster.
        static void DoorCarveRect(BlueprintDoor d, out float x0, out float y0, out float x1, out float y1)
        {
            if (d.Orientation == "h") { x0 = d.X; x1 = d.X + d.Length; y0 = d.Y - 0.5f; y1 = d.Y + 0.5f; }
            else { x0 = d.X - 0.5f; x1 = d.X + 0.5f; y0 = d.Y; y1 = d.Y + d.Length; }
        }

        /// True if any of the space's rects, each expanded by `expand`, overlaps the given rect.
        static bool SpaceTouchesRect(BlueprintSpace s, float expand,
                                     float x0, float y0, float x1, float y1)
        {
            foreach (float[] r in s.Rects)
                if (RectsOverlap(r[0] - expand, r[1] - expand,
                                 r[0] + r[2] + expand, r[1] + r[3] + expand,
                                 x0, y0, x1, y1))
                    return true;
            return false;
        }

        /// True if any rect of s strictly overlaps any rect of t.
        static bool SpacesOverlap(BlueprintSpace s, BlueprintSpace t)
        {
            foreach (float[] r in s.Rects)
            foreach (float[] q in t.Rects)
                if (RectsOverlap(r[0], r[1], r[0] + r[2], r[1] + r[3],
                                 q[0], q[1], q[0] + q[2], q[1] + q[3]))
                    return true;
            return false;
        }

        static bool PointNearSpace(BlueprintSpace s, Vector2 p, float expand)
        {
            foreach (float[] r in s.Rects)
                if (p.x >= r[0] - expand && p.x <= r[0] + r[2] + expand &&
                    p.y >= r[1] - expand && p.y <= r[1] + r[3] + expand)
                    return true;
            return false;
        }

        static Vector2 EntranceProbe(BlueprintEntrance e)
        {
            if (e.Kind == "hatch") return new Vector2(e.X, e.Y);
            switch (e.Facing)
            {
                case "w": return new Vector2(e.X - 1f, e.Y);
                case "e": return new Vector2(e.X + 1f, e.Y);
                case "n": return new Vector2(e.X, e.Y - 1f);
                default: return new Vector2(e.X, e.Y + 1f); // "s"
            }
        }

        // ------------------------------------------------------------------
        // 1. Unique ids across spaces, doors, vents, entrances, patrols, guard posts,
        //    all interior anchors, and exterior anchors.
        // ------------------------------------------------------------------
        static void CheckUniqueIds(Blueprint bp, List<string> errors)
        {
            var sub = new List<string>();
            var seen = new Dictionary<string, string>();
            void Add(string id, string kind)
            {
                if (string.IsNullOrEmpty(id)) { sub.Add($"{kind} has null/empty id"); return; }
                if (seen.TryGetValue(id, out string prev)) sub.Add($"duplicate id '{id}' ({prev} vs {kind})");
                else seen[id] = kind;
            }

            foreach (var s in bp.Spaces) Add(s.Id, "space");
            foreach (var d in bp.Doors) Add(d.Id, "door");
            foreach (var v in bp.Vents) Add(v.Id, "vent");
            foreach (var e in bp.Entrances) Add(e.Id, "entrance");
            foreach (var p in bp.Patrols) Add(p.Id, "patrol");
            foreach (var g in bp.GuardPosts) Add(g.Id, "guardPost");
            foreach (var kv in bp.Anchors)
            {
                SpaceAnchors sa = kv.Value;
                if (sa.ItemAnchors != null) foreach (var a in sa.ItemAnchors) Add(a.Id, $"itemAnchor in {kv.Key}");
                if (sa.LightSources != null) foreach (var a in sa.LightSources) Add(a.Id, $"lightSource in {kv.Key}");
                if (sa.SensorMounts != null) foreach (var a in sa.SensorMounts) Add(a.Id, $"sensorMount in {kv.Key}");
                if (sa.SabotageFixtureMounts != null) foreach (var a in sa.SabotageFixtureMounts) Add(a.Id, $"sabotageMount in {kv.Key}");
            }
            foreach (var a in bp.Grounds.ExteriorItemAnchors) Add(a.Id, "exteriorAnchor");
            foreach (var p in bp.InteriorPeeks) Add(p.Id, "interiorPeek");
            if (bp.Grounds.Security != null)
            {
                foreach (var c in bp.Grounds.Security.Cameras ?? new List<SecurityCone>()) Add(c.Id, "camera");
                foreach (var f in bp.Grounds.Security.Floodlights ?? new List<SecurityCone>()) Add(f.Id, "floodlight");
            }

            Report($"check 1 unique ids ({seen.Count} ids)", sub, errors);
        }

        // ------------------------------------------------------------------
        // 1b. Schema sanity: enum-like string fields are whitelisted (a typo'd
        //     orientation/facing silently changes carve geometry otherwise), the
        //     assignment file actually pairs with this blueprint, and override
        //     keys reference real rooms.
        // ------------------------------------------------------------------
        static readonly string[] LightKinds =
            { "highbay", "lamp", "desk", "monitor", "window-spill", "flood", "emergency" };

        static void CheckSchemaSanity(Blueprint bp, RoleAssignment ra, DressingLibrary lib,
                                      List<string> errors)
        {
            var sub = new List<string>();

            foreach (var d in bp.Doors)
                if (d.Orientation != "h" && d.Orientation != "v")
                    sub.Add($"door '{d.Id}' orientation '{d.Orientation}' is not h|v");
            foreach (var o in bp.Openings)
                if (o.Orientation != "h" && o.Orientation != "v")
                    sub.Add($"opening at ({o.X},{o.Y}) orientation '{o.Orientation}' is not h|v");

            foreach (var e in bp.Entrances)
            {
                if (e.Kind != "door" && e.Kind != "hatch")
                    sub.Add($"entrance '{e.Id}' kind '{e.Kind}' is not door|hatch");
                else if (e.Kind == "door" && e.Facing != "n" && e.Facing != "s" && e.Facing != "e" && e.Facing != "w")
                    sub.Add($"entrance '{e.Id}' facing '{e.Facing}' is not n|s|e|w");
                else if (e.Kind == "hatch" && e.Facing != "down")
                    sub.Add($"hatch entrance '{e.Id}' facing '{e.Facing}' is not 'down'");
            }

            foreach (var kv in bp.Anchors)
                if (kv.Value.LightSources != null)
                    foreach (var l in kv.Value.LightSources)
                        if (!LightKinds.Contains(l.Kind))
                            sub.Add($"light '{l.Id}' in {kv.Key} has unknown kind '{l.Kind}' (loader would fall back silently)");

            if (!string.IsNullOrEmpty(ra.Blueprint) && ra.Blueprint != bp.Id)
                sub.Add($"assignment pairs with blueprint '{ra.Blueprint}' but this blueprint is '{bp.Id}'");

            if (ra.DressingOverrides != null)
                foreach (var kv in ra.DressingOverrides)
                {
                    if (bp.FindSpace(kv.Key) == null)
                        sub.Add($"dressingOverrides key '{kv.Key}' is not a space in this blueprint");
                    if (lib.Dressings == null || !lib.Dressings.ContainsKey(kv.Value))
                        sub.Add($"dressingOverrides value '{kv.Value}' is not a dressing");
                }

            Report("check 1b schema sanity", sub, errors);
        }

        // ------------------------------------------------------------------
        // 2. Eligibility census: exactly 5 vault / 3 power / 3 ops rooms; corridors roleless.
        // ------------------------------------------------------------------
        static void CheckEligibilityCensus(List<BlueprintSpace> rooms, List<BlueprintSpace> corridors,
                                           List<string> errors)
        {
            var sub = new List<string>();
            int vault = rooms.Count(r => r.EligibleRoles.Contains("vault"));
            int power = rooms.Count(r => r.EligibleRoles.Contains("power"));
            int ops = rooms.Count(r => r.EligibleRoles.Contains("ops"));
            // DEVELOPMENT.md recipe: 5 vault-eligible, 2-3 power-eligible, 2-3 ops-eligible.
            if (vault != 5) sub.Add($"expected exactly 5 vault-eligible rooms, found {vault}");
            if (power < 2 || power > 3) sub.Add($"expected 2-3 power-eligible rooms, found {power}");
            if (ops < 2 || ops > 3) sub.Add($"expected 2-3 ops-eligible rooms, found {ops}");
            foreach (var c in corridors)
                if (c.EligibleRoles.Count != 0)
                    sub.Add($"corridor '{c.Id}' has eligibleRoles [{string.Join(",", c.EligibleRoles)}]");
            Report($"check 2 eligibility census (vault={vault} power={power} ops={ops})", sub, errors);
        }

        // ------------------------------------------------------------------
        // 3. Doors: between = exactly 2 existing spaces; carve rect touches both
        //    (space rects expanded 0.6) and no third space.
        // ------------------------------------------------------------------
        static void CheckDoors(Blueprint bp, List<string> errors)
        {
            var sub = new List<string>();
            foreach (BlueprintDoor d in bp.Doors)
            {
                if (d.Between == null || d.Between.Count != 2)
                {
                    sub.Add($"door '{d.Id}' between must list exactly 2 spaces (has {d.Between?.Count ?? 0})");
                    continue;
                }
                BlueprintSpace a = bp.FindSpace(d.Between[0]);
                BlueprintSpace b = bp.FindSpace(d.Between[1]);
                if (a == null) { sub.Add($"door '{d.Id}' between space '{d.Between[0]}' does not exist"); continue; }
                if (b == null) { sub.Add($"door '{d.Id}' between space '{d.Between[1]}' does not exist"); continue; }

                DoorCarveRect(d, out float x0, out float y0, out float x1, out float y1);
                if (!SpaceTouchesRect(a, 0.6f, x0, y0, x1, y1))
                    sub.Add($"door '{d.Id}' does not geometrically touch space '{a.Id}'");
                if (!SpaceTouchesRect(b, 0.6f, x0, y0, x1, y1))
                    sub.Add($"door '{d.Id}' does not geometrically touch space '{b.Id}'");
                foreach (BlueprintSpace other in bp.Spaces)
                {
                    if (other == a || other == b) continue;
                    if (!SpaceTouchesRect(other, 0f, x0, y0, x1, y1)) continue;
                    // Strict overlap (no expansion) for the exclusion: a space that merely
                    // ENDS at the same wall corner as the door is legitimate. A space whose
                    // interior the carve cuts into is a mis-wired door — UNLESS that space
                    // itself overlaps one of the door's two named spaces (junction corridors
                    // like C-E/C-S3 overlap by design, so their floor legitimately covers
                    // doors belonging to either).
                    if (SpacesOverlap(other, a) || SpacesOverlap(other, b)) continue;
                    sub.Add($"door '{d.Id}' also touches third space '{other.Id}'");
                }
            }
            Report($"check 3 door adjacency ({bp.Doors.Count} doors)", sub, errors);
        }

        // ------------------------------------------------------------------
        // 4. Raster connectivity: every entrance probe reaches every room center,
        //    and every pair of entrance probes is connected.
        // ------------------------------------------------------------------
        static void CheckConnectivity(Blueprint bp, List<BlueprintSpace> rooms,
                                      BlueprintRaster raster, List<string> errors)
        {
            var sub = new List<string>();
            var probes = new List<(string id, Vector2 p)>();
            foreach (BlueprintEntrance e in bp.Entrances)
                probes.Add((e.Id, EntranceProbe(e)));

            foreach ((string id, Vector2 p) in probes)
                foreach (BlueprintSpace room in rooms)
                    if (!raster.AreConnected(p, room.Center))
                        sub.Add($"entrance '{id}' probe ({p.x},{p.y}) not connected to room '{room.Id}' center");

            for (int i = 0; i < probes.Count; i++)
                for (int j = i + 1; j < probes.Count; j++)
                    if (!raster.AreConnected(probes[i].p, probes[j].p))
                        sub.Add($"entrances '{probes[i].id}' and '{probes[j].id}' not connected");

            Report($"check 4 connectivity ({probes.Count} entrances x {rooms.Count} rooms)", sub, errors);
        }

        // ------------------------------------------------------------------
        // 5. Patrols: >= 2 waypoints, every consecutive (and closing) segment
        //    walkable strictly indoors.
        // ------------------------------------------------------------------
        static void CheckPatrols(Blueprint bp, BlueprintRaster raster, List<string> errors)
        {
            var sub = new List<string>();
            foreach (BlueprintPatrol p in bp.Patrols)
            {
                if (p.Waypoints == null || p.Waypoints.Count < 2)
                {
                    sub.Add($"patrol '{p.Id}' has fewer than 2 waypoints");
                    continue;
                }
                var wps = p.Waypoints.Select(w => new Vector2(w[0], w[1])).ToList();
                for (int i = 1; i < wps.Count; i++)
                    if (!raster.SegmentWalkableIndoors(wps[i - 1], wps[i]))
                        sub.Add($"patrol '{p.Id}' segment {i - 1}->{i} not walkable indoors");
                if (p.Closed && !raster.SegmentWalkableIndoors(wps[wps.Count - 1], wps[0]))
                    sub.Add($"patrol '{p.Id}' closing segment {wps.Count - 1}->0 not walkable indoors");
            }
            Report($"check 5 patrols ({bp.Patrols.Count} patrols)", sub, errors);
        }

        // ------------------------------------------------------------------
        // 6. Guard posts stand on walkable indoor cells (Floor/DoorFloor).
        // ------------------------------------------------------------------
        static void CheckGuardPosts(Blueprint bp, BlueprintRaster raster, List<string> errors)
        {
            var sub = new List<string>();
            foreach (BlueprintGuardPost g in bp.GuardPosts)
            {
                RasterCell c = raster.CellAtData(g.X, g.Y);
                if (c != RasterCell.Floor && c != RasterCell.DoorFloor)
                    sub.Add($"guard post '{g.Id}' at ({g.X},{g.Y}) stands on {c}, not indoor floor");
            }
            Report($"check 6 guard posts ({bp.GuardPosts.Count} posts)", sub, errors);
        }

        // ------------------------------------------------------------------
        // 7. Every anchor of every space lies inside one of that space's rects.
        // ------------------------------------------------------------------
        static void CheckAnchorContainment(Blueprint bp, List<string> errors)
        {
            var sub = new List<string>();
            foreach (var kv in bp.Anchors)
            {
                BlueprintSpace space = bp.FindSpace(kv.Key);
                if (space == null)
                {
                    sub.Add($"anchors entry for unknown space '{kv.Key}'");
                    continue;
                }
                void Contain(string id, float x, float y, string kind)
                {
                    if (!space.Contains(new Vector2(x, y)))
                        sub.Add($"{kind} '{id}' at ({x},{y}) lies outside space '{space.Id}'");
                }
                SpaceAnchors sa = kv.Value;
                if (sa.ItemAnchors != null) foreach (var a in sa.ItemAnchors) Contain(a.Id, a.X, a.Y, "itemAnchor");
                if (sa.LightSources != null) foreach (var a in sa.LightSources) Contain(a.Id, a.X, a.Y, "lightSource");
                if (sa.SensorMounts != null) foreach (var a in sa.SensorMounts) Contain(a.Id, a.X, a.Y, "sensorMount");
                if (sa.SabotageFixtureMounts != null) foreach (var a in sa.SabotageFixtureMounts) Contain(a.Id, a.X, a.Y, "sabotageMount");
            }
            Report($"check 7 anchor containment ({bp.Anchors.Count} spaces with anchors)", sub, errors);
        }

        // ------------------------------------------------------------------
        // 8. Peeks: every entrance has one; interior peeks reference a real door,
        //    look into one of its between spaces, and never into a candidate room.
        // ------------------------------------------------------------------
        static void CheckPeeks(Blueprint bp, List<string> errors)
        {
            var sub = new List<string>();
            foreach (BlueprintEntrance e in bp.Entrances)
                if (e.Peek == null)
                    sub.Add($"entrance '{e.Id}' has no peek");

            foreach (BlueprintPeek peek in bp.InteriorPeeks)
            {
                BlueprintDoor door = bp.Doors.FirstOrDefault(d => d.Id == peek.DoorId);
                if (door == null)
                {
                    sub.Add($"interior peek '{peek.Id}' references missing door '{peek.DoorId}'");
                    continue;
                }
                if (door.Between == null || !door.Between.Contains(peek.Into))
                    sub.Add($"interior peek '{peek.Id}' into '{peek.Into}' is not one of door '{door.Id}' between pair");
                BlueprintSpace into = bp.FindSpace(peek.Into);
                if (into == null)
                    sub.Add($"interior peek '{peek.Id}' into space '{peek.Into}' does not exist");
                else if (!IsFillerOnly(into))
                    sub.Add($"interior peek '{peek.Id}' looks into candidate room '{into.Id}' " +
                            $"(roles [{string.Join(",", into.EligibleRoles)}])");
            }
            Report($"check 8 peeks ({bp.Entrances.Count} entrance + {bp.InteriorPeeks.Count} interior)", sub, errors);
        }

        // ------------------------------------------------------------------
        // 9. Vents: end spaces exist and are corridor/filler-only; endpoints lie in
        //    (or within 0.6 of) their end spaces; the duct actually connects.
        // ------------------------------------------------------------------
        static void CheckVents(Blueprint bp, BlueprintRaster raster, List<string> errors)
        {
            var sub = new List<string>();
            foreach (BlueprintVent v in bp.Vents)
            {
                if (v.Points == null || v.Points.Count < 2)
                {
                    sub.Add($"vent '{v.Id}' has fewer than 2 points");
                    continue;
                }
                if (v.Ends == null || v.Ends.Count != 2)
                {
                    sub.Add($"vent '{v.Id}' must list exactly 2 ends (has {v.Ends?.Count ?? 0})");
                    continue;
                }
                Vector2 first = new Vector2(v.Points[0][0], v.Points[0][1]);
                Vector2 last = new Vector2(v.Points[v.Points.Count - 1][0], v.Points[v.Points.Count - 1][1]);
                var endPoints = new[] { first, last };
                for (int i = 0; i < 2; i++)
                {
                    BlueprintSpace end = bp.FindSpace(v.Ends[i]);
                    if (end == null)
                    {
                        sub.Add($"vent '{v.Id}' end[{i}] space '{v.Ends[i]}' does not exist");
                        continue;
                    }
                    if (!end.IsCorridor && !IsFillerOnly(end))
                        sub.Add($"vent '{v.Id}' end[{i}] '{end.Id}' is a candidate room " +
                                $"(roles [{string.Join(",", end.EligibleRoles)}])");
                    if (!PointNearSpace(end, endPoints[i], 0.6f))
                        sub.Add($"vent '{v.Id}' endpoint ({endPoints[i].x},{endPoints[i].y}) " +
                                $"not within 0.6 of end space '{end.Id}'");
                }
                if (!raster.AreConnected(first, last))
                    sub.Add($"vent '{v.Id}' duct does not connect its endpoints in the raster");
            }
            Report($"check 9 vents ({bp.Vents.Count} vents)", sub, errors);
        }

        // ------------------------------------------------------------------
        // 10. Assignment roles: 3 distinct vault-eligible vaults, eligible power/ops,
        //     vault spread (no shared door, bbox span >= 24 on an axis).
        // ------------------------------------------------------------------
        static void CheckAssignmentRoles(Blueprint bp, RoleAssignment ra, List<string> errors)
        {
            var sub = new List<string>();

            if (ra.Vaults.Count != 3)
                sub.Add($"expected exactly 3 vaults, assignment has {ra.Vaults.Count}");
            if (ra.Vaults.Distinct().Count() != ra.Vaults.Count)
                sub.Add($"vaults are not distinct: [{string.Join(",", ra.Vaults)}]");

            var vaultSpaces = new List<BlueprintSpace>();
            foreach (string id in ra.Vaults)
            {
                BlueprintSpace s = bp.FindSpace(id);
                if (s == null) { sub.Add($"vault '{id}' does not exist"); continue; }
                if (s.IsCorridor || !s.EligibleRoles.Contains("vault"))
                    sub.Add($"vault '{id}' is not a vault-eligible room");
                vaultSpaces.Add(s);
            }

            BlueprintSpace power = bp.FindSpace(ra.Power);
            if (power == null) sub.Add($"power room '{ra.Power}' does not exist");
            else if (power.IsCorridor || !power.EligibleRoles.Contains("power"))
                sub.Add($"power room '{ra.Power}' is not power-eligible");

            BlueprintSpace ops = bp.FindSpace(ra.Ops);
            if (ops == null) sub.Add($"ops room '{ra.Ops}' does not exist");
            else if (ops.IsCorridor || !ops.EligibleRoles.Contains("ops"))
                sub.Add($"ops room '{ra.Ops}' is not ops-eligible");

            // Spread: no two winning vaults share a door.
            var vaultIds = new HashSet<string>(ra.Vaults);
            foreach (BlueprintDoor d in bp.Doors)
                if (d.Between != null && d.Between.Count(vaultIds.Contains) >= 2)
                    sub.Add($"vaults share door '{d.Id}' ({string.Join(",", d.Between)})");

            // Spread: vault-center bounding box spans >= 24 tiles on at least one axis.
            if (vaultSpaces.Count == 3)
            {
                float minX = vaultSpaces.Min(s => s.Center.x), maxX = vaultSpaces.Max(s => s.Center.x);
                float minY = vaultSpaces.Min(s => s.Center.y), maxY = vaultSpaces.Max(s => s.Center.y);
                float spanX = maxX - minX, spanY = maxY - minY;
                if (spanX < 24f && spanY < 24f)
                    sub.Add($"vault centers span only {spanX:0.#}x{spanY:0.#} tiles (need >= 24 on one axis)");
            }

            Report($"check 10 assignment roles (vaults=[{string.Join(",", ra.Vaults)}] power={ra.Power} ops={ra.Ops})",
                   sub, errors);
        }

        // ------------------------------------------------------------------
        // 11. Badge doors == exactly the doors touching winning rooms; locked and
        //     badge-gated disjoint; all referenced doors exist.
        // ------------------------------------------------------------------
        static void CheckBadgeDoors(Blueprint bp, RoleAssignment ra, List<string> errors)
        {
            var sub = new List<string>();
            var winners = new HashSet<string>(ra.Vaults) { ra.Power, ra.Ops };
            var required = new HashSet<string>(
                bp.Doors.Where(d => d.Between != null && d.Between.Any(winners.Contains))
                        .Select(d => d.Id));

            var badge = new HashSet<string>(ra.DoorTypes?.BadgeGated ?? new List<string>());
            var locked = new HashSet<string>(ra.DoorTypes?.Locked ?? new List<string>());
            var doorIds = new HashSet<string>(bp.Doors.Select(d => d.Id));

            foreach (string id in required.Where(id => !badge.Contains(id)))
                sub.Add($"door '{id}' touches a winning room but is not badgeGated");
            foreach (string id in badge.Where(id => !required.Contains(id)))
                sub.Add($"door '{id}' is badgeGated but touches no winning room");
            foreach (string id in badge.Where(locked.Contains))
                sub.Add($"door '{id}' is both badgeGated and locked");
            foreach (string id in badge.Concat(locked).Where(id => !doorIds.Contains(id)))
                sub.Add($"doorTypes references missing door '{id}'");

            Report($"check 11 badge doors ({required.Count} required, {badge.Count} badgeGated, {locked.Count} locked)",
                   sub, errors);
        }

        // ------------------------------------------------------------------
        // 12. openEntrances >= 2 and all exist; exfil subset of openEntrances, >= 2.
        // ------------------------------------------------------------------
        static void CheckEntranceAssignment(Blueprint bp, RoleAssignment ra, List<string> errors)
        {
            var sub = new List<string>();
            var entranceIds = new HashSet<string>(bp.Entrances.Select(e => e.Id));

            if (ra.OpenEntrances.Count < 2)
                sub.Add($"openEntrances must have >= 2 entries, has {ra.OpenEntrances.Count}");
            foreach (string id in ra.OpenEntrances.Where(id => !entranceIds.Contains(id)))
                sub.Add($"openEntrances references missing entrance '{id}'");

            if (ra.Exfil.Count < 2)
                sub.Add($"exfil must have >= 2 entries, has {ra.Exfil.Count}");
            foreach (string id in ra.Exfil.Where(id => !ra.OpenEntrances.Contains(id)))
                sub.Add($"exfil entrance '{id}' is not in openEntrances");

            Report($"check 12 entrance assignment (open=[{string.Join(",", ra.OpenEntrances)}] " +
                   $"exfil=[{string.Join(",", ra.Exfil)}])", sub, errors);
        }

        // ------------------------------------------------------------------
        // 13. Guards: unique G-xx ids; duties reference existing patrols/posts.
        // ------------------------------------------------------------------
        static void CheckGuardDuties(Blueprint bp, RoleAssignment ra, List<string> errors)
        {
            var sub = new List<string>();
            var idPattern = new Regex(@"^G-\d{2}$");
            var seen = new HashSet<string>();
            var patrolIds = new HashSet<string>(bp.Patrols.Select(p => p.Id));
            var postIds = new HashSet<string>(bp.GuardPosts.Select(g => g.Id));

            foreach (GuardDuty g in ra.Guards)
            {
                if (g.Id == null || !idPattern.IsMatch(g.Id))
                    sub.Add($"guard id '{g.Id}' does not match G-xx format");
                else if (!seen.Add(g.Id))
                    sub.Add($"duplicate guard id '{g.Id}'");

                switch (g.Duty)
                {
                    case "patrol":
                        if (g.Patrol == null || !patrolIds.Contains(g.Patrol))
                            sub.Add($"guard '{g.Id}' patrol '{g.Patrol}' does not exist");
                        break;
                    case "post":
                        if (g.Post == null || !postIds.Contains(g.Post))
                            sub.Add($"guard '{g.Id}' post '{g.Post}' does not exist");
                        break;
                    default:
                        sub.Add($"guard '{g.Id}' has unknown duty '{g.Duty}'");
                        break;
                }
            }
            Report($"check 13 guard duties ({ra.Guards.Count} guards)", sub, errors);
        }

        // ------------------------------------------------------------------
        // 14. Anchor richness: interior itemAnchors >= 45, sensorMounts >= 22,
        //     sabotage >= 5 (>= 3 sensor-relay, >= 2 zone-breaker), every corridor lit,
        //     <= 6 unlit rooms.
        // ------------------------------------------------------------------
        static void CheckAnchorRichness(Blueprint bp, List<BlueprintSpace> rooms,
                                        List<BlueprintSpace> corridors, List<string> errors)
        {
            var sub = new List<string>();
            int items = 0, sensors = 0, sabotage = 0, relays = 0, breakers = 0;
            foreach (SpaceAnchors sa in bp.Anchors.Values)
            {
                items += sa.ItemAnchors?.Count ?? 0;
                sensors += sa.SensorMounts?.Count ?? 0;
                if (sa.SabotageFixtureMounts != null)
                {
                    sabotage += sa.SabotageFixtureMounts.Count;
                    relays += sa.SabotageFixtureMounts.Count(m => m.Kind == "sensor-relay");
                    breakers += sa.SabotageFixtureMounts.Count(m => m.Kind == "zone-breaker");
                }
            }
            if (items < 45) sub.Add($"interior itemAnchors total {items}, need >= 45");
            if (sensors < 22) sub.Add($"sensorMounts total {sensors}, need >= 22");
            if (sabotage < 5) sub.Add($"sabotageFixtureMounts total {sabotage}, need >= 5");
            if (relays < 3) sub.Add($"sensor-relay mounts total {relays}, need >= 3");
            if (breakers < 2) sub.Add($"zone-breaker mounts total {breakers}, need >= 2");

            int LightCount(string spaceId) =>
                bp.Anchors.TryGetValue(spaceId, out SpaceAnchors sa) ? (sa.LightSources?.Count ?? 0) : 0;

            foreach (BlueprintSpace c in corridors)
                if (LightCount(c.Id) < 1)
                    sub.Add($"corridor '{c.Id}' has no lightSources");

            int unlitRooms = rooms.Count(r => LightCount(r.Id) == 0);
            if (unlitRooms > 6)
                sub.Add($"{unlitRooms} rooms have zero lightSources, allowed <= 6");

            Report($"check 14 anchor richness (items={items} sensors={sensors} sabotage={sabotage} " +
                   $"[relay={relays} breaker={breakers}] unlitRooms={unlitRooms})", sub, errors);
        }

        // ------------------------------------------------------------------
        // 15. Dressings: every room resolves via byRoomDefaults or an assignment
        //     override; roleDressings covers vault/power/ops.
        // ------------------------------------------------------------------
        static void CheckDressings(Blueprint bp, List<BlueprintSpace> rooms, RoleAssignment ra,
                                   DressingLibrary lib, List<string> errors)
        {
            var sub = new List<string>();
            foreach (BlueprintSpace room in rooms)
            {
                string key = null;
                if (ra.DressingOverrides.TryGetValue(room.Id, out string ovr)) key = ovr;
                else if (lib.ByRoomDefaults.TryGetValue(room.Id, out string def)) key = def;

                if (key == null)
                    sub.Add($"room '{room.Id}' has no dressing (no byRoomDefaults entry, no override)");
                else if (!lib.Dressings.ContainsKey(key))
                    sub.Add($"room '{room.Id}' dressing key '{key}' not in dressings library");
            }
            foreach (string role in new[] { "vault", "power", "ops" })
            {
                if (!lib.RoleDressings.TryGetValue(role, out string key))
                    sub.Add($"roleDressings missing role '{role}'");
                else if (!lib.Dressings.ContainsKey(key))
                    sub.Add($"roleDressings['{role}'] = '{key}' not in dressings library");
            }
            Report($"check 15 dressings ({lib.Dressings.Count} dressings, {lib.ByRoomDefaults.Count} room defaults)",
                   sub, errors);
        }

        // ------------------------------------------------------------------

        static void LogSummary(Blueprint bp, List<BlueprintSpace> rooms, List<BlueprintSpace> corridors,
                               BlueprintRaster raster)
        {
            int items = bp.Anchors.Values.Sum(sa => sa.ItemAnchors?.Count ?? 0);
            int lights = bp.Anchors.Values.Sum(sa => sa.LightSources?.Count ?? 0);
            int sensors = bp.Anchors.Values.Sum(sa => sa.SensorMounts?.Count ?? 0);
            int sabotage = bp.Anchors.Values.Sum(sa => sa.SabotageFixtureMounts?.Count ?? 0);
            Debug.Log($"[AIRGAP.CI] summary: spaces={bp.Spaces.Count} (rooms={rooms.Count} corridors={corridors.Count}) " +
                      $"doors={bp.Doors.Count} openings={bp.Openings.Count} vents={bp.Vents.Count} " +
                      $"entrances={bp.Entrances.Count} interiorPeeks={bp.InteriorPeeks.Count} " +
                      $"patrols={bp.Patrols.Count} guardPosts={bp.GuardPosts.Count} " +
                      $"itemAnchors={items} lightSources={lights} sensorMounts={sensors} sabotageMounts={sabotage} " +
                      $"exteriorAnchors={bp.Grounds.ExteriorItemAnchors.Count} " +
                      $"ventGrates={raster.VentGrates.Count} raster={raster.WidthCells}x{raster.HeightCells}");
        }
    }
}
