// AIRGAP — runtime blueprint schema (Phase 3).
// Data space is x-right, y-DOWN, units = tiles. Only the scene loader flips to Unity world
// (world = new Vector2(data.x, -data.y)); everything in this file works purely in data space.
// JSON is parsed with Newtonsoft; field names in the data files are camelCase.
// Consumed by BlueprintRaster.cs, ValidatePhase3.cs and BlueprintScene.cs — do not change shapes.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace AIRGAP.Facility.Blueprints
{
    public class Blueprint
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("rev")] public string Rev;
        [JsonProperty("building")] public BuildingInfo Building;
        [JsonProperty("spaces")] public List<BlueprintSpace> Spaces;              // rooms AND corridors
        [JsonProperty("doors")] public List<BlueprintDoor> Doors;
        [JsonProperty("openings")] public List<BlueprintOpening> Openings;
        [JsonProperty("vents")] public List<BlueprintVent> Vents;
        [JsonProperty("entrances")] public List<BlueprintEntrance> Entrances;
        [JsonProperty("interiorPeeks")] public List<BlueprintPeek> InteriorPeeks;
        [JsonProperty("patrols")] public List<BlueprintPatrol> Patrols;
        [JsonProperty("guardPosts")] public List<BlueprintGuardPost> GuardPosts;
        [JsonProperty("grounds")] public BlueprintGrounds Grounds;

        /// Keyed by space id; loaded from the separate anchors file. Empty dict entries allowed.
        [JsonIgnore] public Dictionary<string, SpaceAnchors> Anchors;

        public BlueprintSpace FindSpace(string id)
        {
            if (Spaces == null || id == null) return null;
            for (int i = 0; i < Spaces.Count; i++)
                if (Spaces[i].Id == id) return Spaces[i];
            return null;
        }

        /// Containment via rects, rooms first then corridors; null if no space contains the point.
        public BlueprintSpace SpaceAt(Vector2 dataPoint)
        {
            if (Spaces == null) return null;
            for (int i = 0; i < Spaces.Count; i++)
                if (!Spaces[i].IsCorridor && Spaces[i].Contains(dataPoint)) return Spaces[i];
            for (int i = 0; i < Spaces.Count; i++)
                if (Spaces[i].IsCorridor && Spaces[i].Contains(dataPoint)) return Spaces[i];
            return null;
        }

        /// Data space is y-down; Unity world is y-up. Only the scene loader should need this.
        public static Vector2 ToWorld(Vector2 dataPoint) => new Vector2(dataPoint.x, -dataPoint.y);

        /// Loads Resources "Data/Blueprints/{baseName}.structure" and "Data/Blueprints/{baseName}.anchors",
        /// parses both with Newtonsoft, merges anchors into .Anchors, computes derived fields.
        /// Throws InvalidOperationException on missing files, parse failure, or missing required sections.
        public static Blueprint LoadFromResources(string baseName)
        {
            string structurePath = $"Data/Blueprints/{baseName}.structure";
            string anchorsPath = $"Data/Blueprints/{baseName}.anchors";

            TextAsset structureAsset = Resources.Load<TextAsset>(structurePath);
            if (structureAsset == null)
                throw new InvalidOperationException(
                    $"Blueprint.LoadFromResources: missing TextAsset at Resources/{structurePath}");
            TextAsset anchorsAsset = Resources.Load<TextAsset>(anchorsPath);
            if (anchorsAsset == null)
                throw new InvalidOperationException(
                    $"Blueprint.LoadFromResources: missing TextAsset at Resources/{anchorsPath}");

            Blueprint bp;
            try
            {
                bp = JsonConvert.DeserializeObject<Blueprint>(structureAsset.text);
            }
            catch (JsonException e)
            {
                throw new InvalidOperationException(
                    $"Blueprint.LoadFromResources: failed to parse {structurePath}: {e.Message}", e);
            }
            if (bp == null)
                throw new InvalidOperationException(
                    $"Blueprint.LoadFromResources: {structurePath} parsed to null (empty file?)");

            // Required top-level sections.
            if (bp.Building == null)
                throw new InvalidOperationException($"Blueprint '{baseName}': missing required section 'building'");
            if (bp.Spaces == null || bp.Spaces.Count == 0)
                throw new InvalidOperationException($"Blueprint '{baseName}': missing required section 'spaces'");
            if (bp.Doors == null)
                throw new InvalidOperationException($"Blueprint '{baseName}': missing required section 'doors'");
            if (bp.Grounds == null)
                throw new InvalidOperationException($"Blueprint '{baseName}': missing required section 'grounds'");
            if (bp.Grounds.Fence == null)
                throw new InvalidOperationException($"Blueprint '{baseName}': missing required section 'grounds.fence'");

            // Optional sections normalize to empty lists so consumers never null-check.
            bp.Openings = bp.Openings ?? new List<BlueprintOpening>();
            bp.Vents = bp.Vents ?? new List<BlueprintVent>();
            bp.Entrances = bp.Entrances ?? new List<BlueprintEntrance>();
            bp.InteriorPeeks = bp.InteriorPeeks ?? new List<BlueprintPeek>();
            bp.Patrols = bp.Patrols ?? new List<BlueprintPatrol>();
            bp.GuardPosts = bp.GuardPosts ?? new List<BlueprintGuardPost>();
            bp.Grounds.Yards = bp.Grounds.Yards ?? new List<YardInfo>();
            bp.Grounds.Props = bp.Grounds.Props ?? new List<PropInfo>();
            bp.Grounds.ExteriorItemAnchors = bp.Grounds.ExteriorItemAnchors ?? new List<ExteriorAnchor>();

            // Derived per-space fields.
            foreach (BlueprintSpace space in bp.Spaces)
            {
                space.EligibleRoles = space.EligibleRoles ?? new List<string>();
                if (space.Rects == null || space.Rects.Count == 0)
                    throw new InvalidOperationException(
                        $"Blueprint '{baseName}': space '{space.Id}' has no rects");

                bool eligible = false;
                foreach (string role in space.EligibleRoles)
                    if (role != "filler" && role != "office") { eligible = true; break; }
                space.IsRoleEligible = eligible;

                float[] r0 = space.Rects[0];
                space.Center = new Vector2(r0[0] + r0[2] * 0.5f, r0[1] + r0[3] * 0.5f);
            }

            // Anchors file: { schemaVersion, description, blueprint, spaces: { "<spaceId>": {...} } }
            AnchorsFile anchorsFile;
            try
            {
                anchorsFile = JsonConvert.DeserializeObject<AnchorsFile>(anchorsAsset.text);
            }
            catch (JsonException e)
            {
                throw new InvalidOperationException(
                    $"Blueprint.LoadFromResources: failed to parse {anchorsPath}: {e.Message}", e);
            }
            if (anchorsFile == null || anchorsFile.Spaces == null)
                throw new InvalidOperationException(
                    $"Blueprint '{baseName}': anchors file missing required section 'spaces'");

            bp.Anchors = new Dictionary<string, SpaceAnchors>();
            foreach (KeyValuePair<string, SpaceAnchors> kv in anchorsFile.Spaces)
                bp.Anchors[kv.Key] = kv.Value ?? new SpaceAnchors();

            return bp;
        }

        class AnchorsFile
        {
            [JsonProperty("spaces")] public Dictionary<string, SpaceAnchors> Spaces;
        }
    }

    public class BuildingInfo
    {
        [JsonProperty("width")] public float Width;
        [JsonProperty("height")] public float Height;
        [JsonProperty("footprint")] public List<float[]> Footprint; // polygon [x,y] pairs
    }

    public class BlueprintSpace
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("kind")] public string Kind;                  // "room" | "corridor"
        [JsonProperty("name")] public string Name;
        [JsonProperty("intent")] public string Intent;
        [JsonProperty("eligibleRoles")] public List<string> EligibleRoles;
        [JsonProperty("rects")] public List<float[]> Rects;         // each [x,y,w,h] in data space

        [JsonIgnore] public bool IsCorridor => Kind == "corridor";
        [JsonIgnore] public bool IsRoleEligible;                    // computed on load: any role not in {"filler","office"}
        [JsonIgnore] public Vector2 Center;                         // computed on load: center of Rects[0]

        public bool Contains(Vector2 dataPoint)
        {
            if (Rects == null) return false;
            for (int i = 0; i < Rects.Count; i++)
            {
                float[] r = Rects[i];
                if (dataPoint.x >= r[0] && dataPoint.x <= r[0] + r[2] &&
                    dataPoint.y >= r[1] && dataPoint.y <= r[1] + r[3])
                    return true;
            }
            return false;
        }
    }

    // Orientation "h": gap in a horizontal wall at y, spanning X..X+Length.
    // Orientation "v": gap in a vertical wall at x, spanning Y..Y+Length.
    public class BlueprintDoor
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("length")] public float Length;
        [JsonProperty("orientation")] public string Orientation;
        [JsonProperty("between")] public List<string> Between;
    }

    public class BlueprintOpening
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("length")] public float Length;
        [JsonProperty("orientation")] public string Orientation;
        [JsonProperty("between")] public List<string> Between;
    }

    // Ends[0] = space of Points.first, Ends[1] = space of Points.last.
    public class BlueprintVent
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("points")] public List<float[]> Points;
        [JsonProperty("ends")] public List<string> Ends;
    }

    // Kind "door"|"hatch"; Facing "n","s","e","w" (door) or "down" (hatch); X,Y = center of the doorway/hatch.
    public class BlueprintEntrance
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("kind")] public string Kind;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("width")] public float Width;
        [JsonProperty("facing")] public string Facing;
        [JsonProperty("into")] public string Into;
        [JsonProperty("peek")] public BlueprintPeek Peek;
    }

    // Entrance peeks carry no id/doorId (null).
    public class BlueprintPeek
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("doorId")] public string DoorId;
        [JsonProperty("into")] public string Into;
        [JsonProperty("type")] public string Type;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("facingDeg")] public float FacingDeg;
    }

    public class BlueprintPatrol
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("closed")] public bool Closed;
        [JsonProperty("waypoints")] public List<float[]> Waypoints;
    }

    public class BlueprintGuardPost
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("facingDeg")] public float FacingDeg;
        [JsonProperty("note")] public string Note;
    }

    public class BlueprintGrounds
    {
        [JsonProperty("spawn")] public SpawnPoint Spawn;
        [JsonProperty("fence")] public FenceInfo Fence;
        [JsonProperty("yards")] public List<YardInfo> Yards;
        [JsonProperty("props")] public List<PropInfo> Props;
        [JsonProperty("exteriorItemAnchors")] public List<ExteriorAnchor> ExteriorItemAnchors;
        [JsonProperty("security")] public SecurityInfo Security;
    }

    /// <summary>The Infiltrator's authored round-start position (data space).</summary>
    public class SpawnPoint
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("note")] public string Note;
    }

    public class FenceInfo
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("width")] public float Width;
        [JsonProperty("height")] public float Height;
        [JsonProperty("gates")] public List<GateInfo> Gates;
    }

    // Side "w"|"n"|"e"|"s"; From/To along that side's axis.
    public class GateInfo
    {
        [JsonProperty("side")] public string Side;
        [JsonProperty("from")] public float From;
        [JsonProperty("to")] public float To;
    }

    public class YardInfo
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("width")] public float Width;
        [JsonProperty("height")] public float Height;
    }

    public class PropInfo
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("width")] public float Width;
        [JsonProperty("height")] public float Height;
    }

    public class ExteriorAnchor
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("note")] public string Note;
    }

    public class SecurityInfo
    {
        [JsonProperty("cameras")] public List<SecurityCone> Cameras;
        [JsonProperty("floodlights")] public List<SecurityCone> Floodlights;
        [JsonProperty("gravel")] public List<GravelInfo> Gravel;
    }

    public class SecurityCone
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("facingDeg")] public float FacingDeg;
        [JsonProperty("fovDeg")] public float FovDeg;
        [JsonProperty("range")] public float Range;
        [JsonProperty("note")] public string Note;
    }

    public class GravelInfo
    {
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("width")] public float Width;
        [JsonProperty("height")] public float Height;
        [JsonProperty("note")] public string Note;
    }

    // All lists may be null/empty — consumers must tolerate both.
    public class SpaceAnchors
    {
        [JsonProperty("itemAnchors")] public List<ItemAnchor> ItemAnchors;
        [JsonProperty("lightSources")] public List<LightAnchor> LightSources;
        [JsonProperty("sensorMounts")] public List<SensorMount> SensorMounts;
        [JsonProperty("sabotageFixtureMounts")] public List<SabotageMount> SabotageFixtureMounts;
    }

    public class ItemAnchor
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("note")] public string Note;
    }

    // Kind: highbay|lamp|desk|monitor|window-spill|flood|emergency; ConeDeg 0 = omni.
    public class LightAnchor
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("kind")] public string Kind;
        [JsonProperty("range")] public float Range;
        [JsonProperty("intensity")] public float Intensity;
        [JsonProperty("coneDeg")] public float ConeDeg;
        [JsonProperty("facingDeg")] public float FacingDeg;
    }

    public class SensorMount
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("note")] public string Note;
    }

    // Kind: sensor-relay | zone-breaker.
    public class SabotageMount
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("x")] public float X;
        [JsonProperty("y")] public float Y;
        [JsonProperty("kind")] public string Kind;
    }

    // ------------------------------------------------------------------
    // Assignment + dressing data (Resources "Data/Blueprints/*.assignment", "Data/Blueprints/dressings").
    // ------------------------------------------------------------------

    /// Default resource paths + convenience loaders for the assignment/dressing files.
    /// Phase 12 note: the assignment path derives from the blueprint base name, so
    /// pointing tools at blueprint02+ is one string, not a path hunt.
    public static class AssignmentData
    {
        public const string DefaultBlueprintBaseName = "blueprint01";
        public const string DefaultDressingsPath = "Data/Blueprints/dressings";

        public static string AssignmentPathFor(string blueprintBaseName) =>
            $"Data/Blueprints/{blueprintBaseName}.assignment";

        public static RoleAssignment LoadDefaultAssignment() =>
            RoleAssignment.LoadFromResources(AssignmentPathFor(DefaultBlueprintBaseName));

        public static DressingLibrary LoadDefaultDressings() =>
            DressingLibrary.LoadFromResources(DefaultDressingsPath);
    }

    public class RoleAssignment
    {
        [JsonProperty("blueprint")] public string Blueprint;
        [JsonProperty("label")] public string Label;
        [JsonProperty("vaults")] public List<string> Vaults;
        [JsonProperty("power")] public string Power;
        [JsonProperty("ops")] public string Ops;
        [JsonProperty("openEntrances")] public List<string> OpenEntrances;
        [JsonProperty("exfil")] public List<string> Exfil;
        [JsonProperty("doorTypes")] public DoorTypes DoorTypes;
        [JsonProperty("guards")] public List<GuardDuty> Guards;
        [JsonProperty("dressingOverrides")] public Dictionary<string, string> DressingOverrides;

        public static RoleAssignment LoadFromResources(string resourcePath)
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
                throw new InvalidOperationException(
                    $"RoleAssignment.LoadFromResources: missing TextAsset at Resources/{resourcePath}");
            RoleAssignment ra;
            try
            {
                ra = JsonConvert.DeserializeObject<RoleAssignment>(asset.text);
            }
            catch (JsonException e)
            {
                throw new InvalidOperationException(
                    $"RoleAssignment.LoadFromResources: failed to parse {resourcePath}: {e.Message}", e);
            }
            if (ra == null)
                throw new InvalidOperationException(
                    $"RoleAssignment.LoadFromResources: {resourcePath} parsed to null (empty file?)");

            ra.Vaults = ra.Vaults ?? new List<string>();
            ra.OpenEntrances = ra.OpenEntrances ?? new List<string>();
            ra.Exfil = ra.Exfil ?? new List<string>();
            ra.Guards = ra.Guards ?? new List<GuardDuty>();
            ra.DressingOverrides = ra.DressingOverrides ?? new Dictionary<string, string>();
            return ra;
        }

        /// "badge-gated" | "locked" | "unlocked".
        public string DoorTypeOf(string doorId)
        {
            if (DoorTypes != null)
            {
                if (DoorTypes.BadgeGated != null && DoorTypes.BadgeGated.Contains(doorId)) return "badge-gated";
                if (DoorTypes.Locked != null && DoorTypes.Locked.Contains(doorId)) return "locked";
            }
            return "unlocked";
        }
    }

    public class DoorTypes
    {
        [JsonProperty("badgeGated")] public List<string> BadgeGated;
        [JsonProperty("locked")] public List<string> Locked;
    }

    public class GuardDuty
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("duty")] public string Duty;      // "patrol" | "post"
        [JsonProperty("patrol")] public string Patrol;
        [JsonProperty("post")] public string Post;
    }

    public class DressingLibrary
    {
        [JsonProperty("dressings")] public Dictionary<string, Dressing> Dressings;
        [JsonProperty("roleDressings")] public Dictionary<string, string> RoleDressings;
        [JsonProperty("byRoomDefaults")] public Dictionary<string, string> ByRoomDefaults;

        public static DressingLibrary LoadFromResources(string resourcePath)
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
                throw new InvalidOperationException(
                    $"DressingLibrary.LoadFromResources: missing TextAsset at Resources/{resourcePath}");
            DressingLibrary lib;
            try
            {
                lib = JsonConvert.DeserializeObject<DressingLibrary>(asset.text);
            }
            catch (JsonException e)
            {
                throw new InvalidOperationException(
                    $"DressingLibrary.LoadFromResources: failed to parse {resourcePath}: {e.Message}", e);
            }
            if (lib == null || lib.Dressings == null)
                throw new InvalidOperationException(
                    $"DressingLibrary.LoadFromResources: {resourcePath} missing required section 'dressings'");

            lib.RoleDressings = lib.RoleDressings ?? new Dictionary<string, string>();
            lib.ByRoomDefaults = lib.ByRoomDefaults ?? new Dictionary<string, string>();
            return lib;
        }

        /// Role dressing (vault/power/ops) wins over the per-room filler default. Null if neither resolves.
        public Dressing Resolve(string roomId, string roleOrNull)
        {
            if (roleOrNull != null && RoleDressings != null &&
                RoleDressings.TryGetValue(roleOrNull, out string roleKey) &&
                Dressings.TryGetValue(roleKey, out Dressing roleDressing))
                return roleDressing;

            if (roomId != null && ByRoomDefaults != null &&
                ByRoomDefaults.TryGetValue(roomId, out string defaultKey) &&
                Dressings.TryGetValue(defaultKey, out Dressing defaultDressing))
                return defaultDressing;

            return null;
        }
    }

    public class Dressing
    {
        [JsonProperty("displayName")] public string DisplayName;
        [JsonProperty("floorTint")] public float[] FloorTint;    // [r,g,b] 0..1
        [JsonProperty("propStyle")] public string PropStyle;
        [JsonProperty("propDensity")] public float PropDensity;
        [JsonProperty("lightProfile")] public string LightProfile;
    }
}
