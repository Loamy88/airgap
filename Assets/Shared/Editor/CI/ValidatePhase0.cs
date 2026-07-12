using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AIRGAP.CI
{
    /// <summary>
    /// Phase 0 data-schema integrity validator.
    /// Run via: Unity -batchmode -nographics -projectPath . -executeMethod AIRGAP.CI.ValidatePhase0.Run -logFile -
    /// Prints [AIRGAP.CI] lines to the log and exits 1 on any failure.
    /// </summary>
    public static class ValidatePhase0
    {
        private static readonly List<string> Errors = new List<string>();

        public static void Run()
        {
            Errors.Clear();
            string dataDir = Path.Combine(Application.dataPath, "Shared", "Resources", "Data");

            JObject guards = LoadJson(Path.Combine(dataDir, "guards.json"));
            JObject technicians = LoadJson(Path.Combine(dataDir, "technicians.json"));
            JObject gadgets = LoadJson(Path.Combine(dataDir, "gadgets.json"));
            JObject facility = LoadJson(Path.Combine(dataDir, "facility.json"));

            if (guards != null) ValidateGuards(guards);
            if (technicians != null && facility != null) ValidateTechnicians(technicians, facility);
            if (gadgets != null) ValidateGadgets(gadgets);
            if (facility != null) ValidateFacility(facility);

            if (Errors.Count == 0)
            {
                Debug.Log("[AIRGAP.CI] ValidatePhase0 PASS — all data schemas valid");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                foreach (string error in Errors)
                {
                    Debug.LogError($"[AIRGAP.CI] FAIL: {error}");
                }
                Debug.LogError($"[AIRGAP.CI] ValidatePhase0 FAIL — {Errors.Count} error(s)");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static JObject LoadJson(string path)
        {
            if (!File.Exists(path))
            {
                Errors.Add($"missing data file: {path}");
                return null;
            }
            try
            {
                var json = JObject.Parse(File.ReadAllText(path));
                RequireHeader(json, Path.GetFileName(path));
                return json;
            }
            catch (Exception e)
            {
                Errors.Add($"unparseable JSON in {Path.GetFileName(path)}: {e.Message}");
                return null;
            }
        }

        private static void RequireHeader(JObject json, string file)
        {
            if (json["schemaVersion"]?.Type != JTokenType.Integer)
                Errors.Add($"{file}: missing integer schemaVersion");
            if (string.IsNullOrWhiteSpace((string)json["description"]))
                Errors.Add($"{file}: missing description");
        }

        private static void ValidateGuards(JObject g)
        {
            const string file = "guards.json";

            float patrol = Require<float>(g, "movement.patrolSpeed", file);
            float chase = Require<float>(g, "movement.chaseSpeed", file);
            if (patrol <= 0) Errors.Add($"{file}: movement.patrolSpeed must be > 0");
            if (chase <= patrol) Errors.Add($"{file}: chaseSpeed must exceed patrolSpeed");

            var categories = g.SelectToken("vision.visibilityCategories")?.Values<string>().ToArray();
            var expectedCats = new[] { "none", "silhouette", "partial", "clear" };
            if (categories == null || !categories.SequenceEqual(expectedCats))
                Errors.Add($"{file}: vision.visibilityCategories must be exactly [none, silhouette, partial, clear] in order");

            if (Require<float>(g, "vision.closeRangeCaptureDistance", file) <= 0)
                Errors.Add($"{file}: vision.closeRangeCaptureDistance must be > 0");

            if ((string)g.SelectToken("hearing.model") != "probabilistic")
                Errors.Add($"{file}: hearing.model must be 'probabilistic' — guards roll, machines don't (DEVELOPMENT.md Phase 2)");
            float relaxed = Require<float>(g, "hearing.alertnessMultipliers.relaxed", file);
            float standard = Require<float>(g, "hearing.alertnessMultipliers.standard", file);
            float heightened = Require<float>(g, "hearing.alertnessMultipliers.heightened", file);
            if (!(relaxed < standard && standard < heightened))
                Errors.Add($"{file}: hearing alertness multipliers must be strictly increasing (relaxed < standard < heightened)");
            float floor = Require<float>(g, "hearing.falloff.minProbabilityFloor", file);
            if (floor <= 0f || floor >= 1f)
                Errors.Add($"{file}: hearing.falloff.minProbabilityFloor must be in (0,1) — nowhere is zero");

            var ladder = g.SelectToken("alertnessLadder.states")?.Values<string>().ToArray();
            var expectedLadder = new[] { "unaware", "suspicious", "searching", "alarmed" };
            if (ladder == null || !ladder.SequenceEqual(expectedLadder))
                Errors.Add($"{file}: alertnessLadder.states must be exactly [unaware, suspicious, searching, alarmed] in order");

            if (Require<float>(g, "memory.bufferSeconds", file) <= 0)
                Errors.Add($"{file}: memory.bufferSeconds must be > 0");
            if (Require<float>(g, "consciousness.downDurationSeconds", file) <= 0)
                Errors.Add($"{file}: consciousness.downDurationSeconds must be > 0");
            if (g.SelectToken("consciousness.wakeUpReport")?.Value<bool>() != true)
                Errors.Add($"{file}: consciousness.wakeUpReport must be true — the wake-up report is load-bearing (Phase 5)");

            var personalities = g["personalities"] as JArray;
            if (personalities == null || personalities.Count == 0)
            {
                Errors.Add($"{file}: personalities must be a non-empty array");
            }
            else
            {
                CheckUniqueIds(personalities, $"{file}: personalities");
                foreach (var p in personalities)
                {
                    if (p.SelectToken("reportEagerness")?.Value<float>() is not > 0)
                        Errors.Add($"{file}: personality '{p["id"]}' needs reportEagerness > 0");
                }
            }

            var orderTypes = g.SelectToken("orders.types")?.Values<string>().ToHashSet();
            var standing = g.SelectToken("orders.standingOrderTypes")?.Values<string>().ToArray() ?? Array.Empty<string>();
            var transient = g.SelectToken("orders.transientOrderTypes")?.Values<string>().ToArray() ?? Array.Empty<string>();
            if (orderTypes == null)
            {
                Errors.Add($"{file}: orders.types missing");
            }
            else
            {
                foreach (string t in standing.Concat(transient).Where(t => !orderTypes.Contains(t)))
                    Errors.Add($"{file}: order '{t}' categorized but not declared in orders.types");
                if (standing.Intersect(transient).Any())
                    Errors.Add($"{file}: an order cannot be both standing and transient");
                if (standing.Length + transient.Length != orderTypes.Count)
                    Errors.Add($"{file}: every order type must be categorized as standing or transient");
            }
            if (Require<float>(g, "orders.transientDefaultTtlSeconds", file) <= 0)
                Errors.Add($"{file}: orders.transientDefaultTtlSeconds must be > 0");
        }

        private static void ValidateTechnicians(JObject t, JObject facility)
        {
            const string file = "technicians.json";

            float workSpeed = Require<float>(t, "movement.workSpeed", file);
            float fleeSpeed = Require<float>(t, "movement.fleeSpeed", file);
            if (workSpeed <= 0) Errors.Add($"{file}: movement.workSpeed must be > 0");

            float infiltratorSprint = Require<float>(facility, "infiltrator.moveSpeeds.sprint", "facility.json");
            if (fleeSpeed <= infiltratorSprint)
                Errors.Add($"{file}: fleeSpeed ({fleeSpeed}) must exceed Infiltrator sprint ({infiltratorSprint}) — a fleeing Technician can never be run down");

            if (Require<int>(t, "flee.abandonJobAfterFleeCount", file) < 1)
                Errors.Add($"{file}: flee.abandonJobAfterFleeCount must be >= 1");
            if (Require<float>(t, "repair.defaultRepairSeconds", file) <= 0)
                Errors.Add($"{file}: repair.defaultRepairSeconds must be > 0");

            if (t.SelectToken("takedown.permanent")?.Value<bool>() != true)
                Errors.Add($"{file}: takedown.permanent must be true — Technicians never wake up (Phase 13)");
            if (t.SelectToken("takedown.generatesReport")?.Value<bool>() != false)
                Errors.Add($"{file}: takedown.generatesReport must be false — a downed Technician is a silent drain");
            if (t.SelectToken("badge.wakeReportPath")?.Value<bool>() != false)
                Errors.Add($"{file}: badge.wakeReportPath must be false — no wake report means no wake-path Badge Flag");

            if (Require<int>(t, "pool.availablePerRound", file) < 1)
                Errors.Add($"{file}: pool.availablePerRound must be >= 1");
        }

        private static void ValidateGadgets(JObject g)
        {
            const string file = "gadgets.json";

            if (Require<int>(g, "slots.gadgetSlots", file) != 3)
                Errors.Add($"{file}: slots.gadgetSlots must be 3 (design constant)");
            if (Require<int>(g, "slots.idCardSlots", file) != 1)
                Errors.Add($"{file}: slots.idCardSlots must be 1 (design constant)");

            var standardKit = g["standardKit"] as JArray;
            var expectedKit = new[] { "flashlight", "pry-bar", "radio-scanner" };
            var kitIds = standardKit?.Select(item => (string)item["id"]).ToArray() ?? Array.Empty<string>();
            if (!expectedKit.OrderBy(x => x).SequenceEqual(kitIds.OrderBy(x => x)))
                Errors.Add($"{file}: standardKit must be exactly [flashlight, pry-bar, radio-scanner]");

            var pryBar = standardKit?.FirstOrDefault(item => (string)item["id"] == "pry-bar");
            if (pryBar != null)
            {
                if (pryBar.SelectToken("hands")?.Value<int>() != 2)
                    Errors.Add($"{file}: pry-bar must be two-handed — the drive carry-lock depends on it (Phase 11)");
                string worksOn = (string)pryBar.SelectToken("doorForce.worksOn") ?? "";
                if (!worksOn.Contains("never badge-gated"))
                    Errors.Add($"{file}: pry-bar doorForce must exclude badge-gated doors");
            }
            var scanner = standardKit?.FirstOrDefault(item => (string)item["id"] == "radio-scanner");
            if (scanner != null && scanner.SelectToken("receiveOnly")?.Value<bool>() != true)
                Errors.Add($"{file}: radio-scanner must be receiveOnly — transmitting is the spoofer's job");

            var loadout = g["loadoutGadgets"] as JArray;
            if (loadout == null || loadout.Count < 9)
                Errors.Add($"{file}: loadoutGadgets must contain at least the 9 designed gadgets (found {loadout?.Count ?? 0})");
            else
                CheckUniqueIds(loadout, $"{file}: loadoutGadgets");

            var foundItems = g["foundItems"] as JArray;
            if (foundItems == null || foundItems.Count == 0)
            {
                Errors.Add($"{file}: foundItems must be a non-empty array");
            }
            else
            {
                CheckUniqueIds(foundItems, $"{file}: foundItems");
                foreach (var item in foundItems)
                {
                    float? weight = item.SelectToken("spawnWeight")?.Value<float>();
                    if (weight is not > 0)
                        Errors.Add($"{file}: found item '{item["id"]}' needs spawnWeight > 0");
                }

                var spoofer = foundItems.FirstOrDefault(item => (string)item["id"] == "radio-spoofer");
                if (spoofer == null)
                {
                    Errors.Add($"{file}: foundItems must include radio-spoofer");
                }
                else
                {
                    float spooferWeight = spoofer.SelectToken("spawnWeight")?.Value<float>() ?? float.MaxValue;
                    float minOther = foundItems.Where(i => !ReferenceEquals(i, spoofer))
                        .Min(i => i.SelectToken("spawnWeight")?.Value<float>() ?? float.MaxValue);
                    if (spooferWeight >= minOther)
                        Errors.Add($"{file}: radio-spoofer must be the rarest item in the pool (weight {spooferWeight} >= {minOther})");
                    if (spoofer.SelectToken("constraints.consumedEvenIfQueried")?.Value<bool>() != true)
                        Errors.Add($"{file}: radio-spoofer must be consumed even when queried");
                    if (spoofer.SelectToken("constraints.alwaysTransient")?.Value<bool>() != true)
                        Errors.Add($"{file}: radio-spoofer orders must always be transient (Phase 10 A2)");
                }
            }

            int spawnMin = Require<int>(g, "spawnRules.spawnedPerRound.min", file);
            int spawnMax = Require<int>(g, "spawnRules.spawnedPerRound.max", file);
            if (spawnMin < 1 || spawnMax < spawnMin)
                Errors.Add($"{file}: spawnRules.spawnedPerRound must satisfy 1 <= min <= max");
        }

        private static void ValidateFacility(JObject f)
        {
            const string file = "facility.json";

            int bestOf = Require<int>(f, "match.bestOfRounds", file);
            if (bestOf < 1 || bestOf % 2 == 0)
                Errors.Add($"{file}: match.bestOfRounds must be a positive odd number");
            if (Require<float>(f, "match.setupPhaseSeconds", file) <= 0)
                Errors.Add($"{file}: match.setupPhaseSeconds must be > 0");
            float round = Require<float>(f, "match.roundSeconds", file);
            float roundMin = Require<float>(f, "match.roundSecondsRange.min", file);
            float roundMax = Require<float>(f, "match.roundSecondsRange.max", file);
            if (round < roundMin || round > roundMax)
                Errors.Add($"{file}: match.roundSeconds must sit inside roundSecondsRange");

            var scoringRows = f.SelectToken("scoring.table") as JArray;
            if (scoringRows == null || scoringRows.Count != 3)
                Errors.Add($"{file}: scoring.table must have exactly 3 outcome rows");

            float crouch = Require<float>(f, "infiltrator.moveSpeeds.crouch", file);
            float walk = Require<float>(f, "infiltrator.moveSpeeds.walk", file);
            float sprint = Require<float>(f, "infiltrator.moveSpeeds.sprint", file);
            if (!(0 < crouch && crouch < walk && walk < sprint))
                Errors.Add($"{file}: infiltrator.moveSpeeds must satisfy 0 < crouch < walk < sprint");
            float nCrouch = Require<float>(f, "infiltrator.noiseLoudness.crouch", file);
            float nWalk = Require<float>(f, "infiltrator.noiseLoudness.walk", file);
            float nSprint = Require<float>(f, "infiltrator.noiseLoudness.sprint", file);
            if (!(0 <= nCrouch && nCrouch < nWalk && nWalk < nSprint && nSprint <= 1f))
                Errors.Add($"{file}: infiltrator.noiseLoudness must be increasing within [0,1]");

            float traversalSpeed = Require<float>(f, "infiltrator.traversal.speed", file);
            if (traversalSpeed <= 0 || traversalSpeed > walk)
                Errors.Add($"{file}: infiltrator.traversal.speed must be in (0, walk]");
            float stepCrouch = Require<float>(f, "infiltrator.footstepIntervalSeconds.crouch", file);
            float stepWalk = Require<float>(f, "infiltrator.footstepIntervalSeconds.walk", file);
            float stepSprint = Require<float>(f, "infiltrator.footstepIntervalSeconds.sprint", file);
            if (!(stepCrouch > stepWalk && stepWalk > stepSprint && stepSprint > 0))
                Errors.Add($"{file}: footstep intervals must satisfy crouch > walk > sprint > 0");

            float nightLevel = Require<float>(f, "lighting.globalNightLevel", file);
            float dimThreshold = Require<float>(f, "lighting.dimThreshold", file);
            float litThreshold = Require<float>(f, "lighting.litThreshold", file);
            if (!(0 <= nightLevel && nightLevel < dimThreshold && dimThreshold < litThreshold && litThreshold <= 1f))
                Errors.Add($"{file}: lighting must satisfy 0 <= globalNightLevel < dimThreshold < litThreshold <= 1");
            if (nightLevel >= dimThreshold)
                Errors.Add($"{file}: the night baseline must read as shadow — globalNightLevel must sit below dimThreshold");

            int vaultEligible = Require<int>(f, "roomRoles.recipe.vault.eligibleSlots", file);
            int vaultChosen = Require<int>(f, "roomRoles.recipe.vault.chosen", file);
            if (vaultChosen != 3)
                Errors.Add($"{file}: vault.chosen must be 3 — all real, any one completes extraction");
            if (vaultChosen >= vaultEligible)
                Errors.Add($"{file}: vault.eligibleSlots must exceed vault.chosen (losing slots become filler)");

            if (Require<int>(f, "doors.defaultForceHitCount", file) < 1)
                Errors.Add($"{file}: doors.defaultForceHitCount must be >= 1");
            var doorTypes = f.SelectToken("doors.types")?.Values<string>().ToArray();
            var expectedDoors = new[] { "unlocked", "locked", "badge-gated" };
            if (doorTypes == null || !doorTypes.SequenceEqual(expectedDoors))
                Errors.Add($"{file}: doors.types must be exactly [unlocked, locked, badge-gated]");

            // The LUMEN containment rule: alerts carry meaning, never position (Phase 9).
            var alertShape = f.SelectToken("lumen.alertShape") as JObject;
            if (alertShape == null)
            {
                Errors.Add($"{file}: lumen.alertShape missing");
            }
            var catalogue = f.SelectToken("lumen.catalogue") as JArray;
            if (catalogue == null || catalogue.Count == 0)
            {
                Errors.Add($"{file}: lumen.catalogue must be a non-empty array");
            }
            else
            {
                foreach (var alert in catalogue)
                {
                    if (alert["position"] != null || alert["location"] != null || alert["coordinates"] != null)
                        Errors.Add($"{file}: LUMEN alert '{alert["eventType"]}' carries a position — forbidden by design, no LUMEN alert may acquire a map coordinate");
                    if (alert["eventType"] == null)
                        Errors.Add($"{file}: LUMEN catalogue entry missing eventType");
                }
                var intrusionClass = new[] { "unauthenticated-session", "ops-table-read", "extraction-in-progress", "looped-feed-detected" };
                foreach (var alert in catalogue.Where(a => intrusionClass.Contains((string)a["eventType"])))
                {
                    if (alert["nodeId"]?.Type != JTokenType.Null)
                        Errors.Add($"{file}: intrusion-class LUMEN alert '{alert["eventType"]}' must have null nodeId — an intruder presents no honest identity");
                }
            }

            if (Require<float>(f, "wardenActions.lockdown.cooldownSeconds", file) <= 0)
                Errors.Add($"{file}: lockdown.cooldownSeconds must be > 0");
            if (Require<float>(f, "objectives.drivePull.telemetry.pingIntervalSeconds", file) <= 0)
                Errors.Add($"{file}: drive telemetry pingIntervalSeconds must be > 0");
        }

        private static void CheckUniqueIds(JArray array, string context)
        {
            var ids = array.Select(item => (string)item["id"]).ToList();
            if (ids.Any(string.IsNullOrWhiteSpace))
                Errors.Add($"{context}: every entry needs a non-empty id");
            foreach (var dupe in ids.GroupBy(id => id).Where(g => g.Count() > 1))
                Errors.Add($"{context}: duplicate id '{dupe.Key}'");
        }

        private static T Require<T>(JObject json, string path, string file)
        {
            JToken token = json.SelectToken(path);
            if (token == null)
            {
                Errors.Add($"{file}: missing required field '{path}'");
                return default;
            }
            try
            {
                return token.Value<T>();
            }
            catch
            {
                Errors.Add($"{file}: field '{path}' is not a {typeof(T).Name}");
                return default;
            }
        }
    }
}
