using System.IO;
using AIRGAP.Facility;
using AIRGAP.Facility.Guards;
using AIRGAP.Facility.Lighting;
using AIRGAP.Infiltrator;
using AIRGAP.Shared.Data;
using AIRGAP.Shared.Greybox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace AIRGAP.CI
{
    /// <summary>
    /// Generates the Phase 1/2 grey-box test scene: one two-room facility slice with
    /// a doorway, a vent duct shortcut, crates, a guard placeholder, and (Phase 2)
    /// authored lights. Fully file-driven — regenerate any time with:
    /// Unity -batchmode -nographics -projectPath . -executeMethod AIRGAP.CI.GreyboxScene.Create -logFile -
    /// </summary>
    public static class GreyboxScene
    {
        public const string ScenePath = "Assets/Scenes/Greybox.unity";
        private const string WhiteSpritePath = "Assets/Shared/Greybox/white.png";

        public const int WallsLayer = 8;
        public const int ActorsLayer = 9;

        private static Sprite _white;
        private static Material _lit;
        private static Material _unlit;

        public static void Create()
        {
            bool ok = false;
            try
            {
                EnsureLayers();
                _white = EnsureWhiteSprite();
                _lit = FindMaterial("Sprite-Lit-Default");
                _unlit = FindMaterial("Sprite-Unlit-Default");

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                BuildRooms();
                GameObject player = BuildPlayer();
                BuildGuard();
                BuildCameraAndHud(player.transform);
                BuildLights(player);

                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new System.Exception($"failed to save {ScenePath}");
                AssetDatabase.SaveAssets();

                Debug.Log($"[AIRGAP.CI] GreyboxScene OK — {ScenePath} written");
                ok = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AIRGAP.CI] GreyboxScene FAIL: {e}");
            }
            // Exit only after try/catch — exiting from inside try skips cleanup and
            // persists half-applied editor state (see the ValidatePhase1 incident).
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        // ---- rooms ---------------------------------------------------------
        // Interior: x in [-11,11], y in [-6,6]. Divider at x=1.5 splits room A (left)
        // and room B (right), passable through a doorway (south) or the vent duct (north).

        private static void BuildRooms()
        {
            Block("Floor", new Vector2(0f, 0f), new Vector2(22.5f, 12.5f), new Color(0.13f, 0.14f, 0.17f), false, 0, -10, _lit);
            Block("VentFloor", new Vector2(1.5f, 4.75f), new Vector2(5.5f, 1.0f), new Color(0.10f, 0.11f, 0.13f), false, 0, -9, _lit);

            Color wall = new Color(0.42f, 0.43f, 0.47f);
            Block("Wall_Bottom", new Vector2(0f, -6.25f), new Vector2(23.5f, 0.5f), wall, true, WallsLayer, 0, _lit);
            Block("Wall_Top", new Vector2(0f, 6.25f), new Vector2(23.5f, 0.5f), wall, true, WallsLayer, 0, _lit);
            Block("Wall_Left", new Vector2(-11.25f, 0f), new Vector2(0.5f, 13f), wall, true, WallsLayer, 0, _lit);
            Block("Wall_Right", new Vector2(11.25f, 0f), new Vector2(0.5f, 13f), wall, true, WallsLayer, 0, _lit);

            // Divider with a doorway gap (y -2.25..-0.75) and a vent gap (y 4.25..5.25).
            Block("Divider_South", new Vector2(1.5f, -4.125f), new Vector2(0.5f, 4.25f), wall, true, WallsLayer, 0, _lit);
            Block("Divider_Mid", new Vector2(1.5f, 1.75f), new Vector2(0.5f, 5.0f), wall, true, WallsLayer, 0, _lit);
            Block("Divider_North", new Vector2(1.5f, 5.625f), new Vector2(0.5f, 0.75f), wall, true, WallsLayer, 0, _lit);

            // Vent duct: interior y 4.25..5.25, x -1.25..4.25; shafts drop into the rooms
            // at x -1.25..-0.5 (room A) and x 3.5..4.25 (room B).
            Color duct = new Color(0.30f, 0.32f, 0.36f);
            Block("Duct_Floor", new Vector2(1.5f, 4.0f), new Vector2(4.0f, 0.5f), duct, true, WallsLayer, 0, _lit);
            Block("Duct_Ceiling", new Vector2(1.5f, 5.5f), new Vector2(6.5f, 0.5f), duct, true, WallsLayer, 0, _lit);
            Block("Duct_CapLeft", new Vector2(-1.5f, 4.75f), new Vector2(0.5f, 1.0f), duct, true, WallsLayer, 0, _lit);
            Block("Duct_CapRight", new Vector2(4.5f, 4.75f), new Vector2(0.5f, 1.0f), duct, true, WallsLayer, 0, _lit);

            TraversalBox("Vent_Duct", new Vector2(1.5f, 4.75f), new Vector2(5.5f, 1.0f));
            TraversalBox("Vent_ShaftLeft", new Vector2(-0.875f, 4.0f), new Vector2(0.75f, 1.0f));
            TraversalBox("Vent_ShaftRight", new Vector2(3.875f, 4.0f), new Vector2(0.75f, 1.0f));

            Color crate = new Color(0.36f, 0.33f, 0.28f);
            Block("Crate_A", new Vector2(-4f, -1f), new Vector2(1.5f, 1.5f), crate, true, WallsLayer, 0, _lit);
            Block("Crate_B", new Vector2(7f, 2f), new Vector2(2f, 1f), crate, true, WallsLayer, 0, _lit);
            Block("Crate_C", new Vector2(5f, -4f), new Vector2(1f, 1f), crate, true, WallsLayer, 0, _lit);
        }

        // ---- actors --------------------------------------------------------

        private static GameObject BuildPlayer()
        {
            var player = new GameObject("Infiltrator") { layer = ActorsLayer };
            player.transform.position = new Vector3(-7f, -3f, 0f);

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

            // Flashlight: gameplay cone (AirgapLight) + visual twin (Light2D), config
            // from gadgets.json. Inactive by default — the controller toggles the GO.
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

        private static void BuildGuard()
        {
            var guard = new GameObject("Guard_G-01") { layer = ActorsLayer };
            guard.transform.position = new Vector3(6f, 0f, 0f);
            guard.transform.right = Vector3.left; // watching the doorway from room B
            var collider = guard.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.9f, 0.9f);
            guard.AddComponent<GuardMarker>().SetGuardId("G-01");
            Visual(guard.transform, "Body", new Vector2(0.9f, 0.9f), new Color(0.3f, 0.5f, 0.95f), 5);

            // Phase 2 perception: vision cone (with debug fan) + probabilistic hearing.
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
        }

        private static void BuildCameraAndHud(Transform player)
        {
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.07f);
            cameraGo.transform.position = new Vector3(player.position.x, player.position.y, -10f);
            cameraGo.AddComponent<CameraFollow>().SetTarget(player);

            new GameObject("Debug HUD").AddComponent<GreyboxHud>();
        }

        // ---- lights ---------------------------------------------------------
        // Night baseline + authored sources (lamp, window spill, floodlight,
        // monitor glow). Each is an AirgapLight (gameplay) + Light2D (visual).

        private static void BuildLights(GameObject player)
        {
            var globalGo = new GameObject("Light_GlobalNight");
            var global = globalGo.AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.intensity = 0.14f;
            global.color = new Color(0.55f, 0.6f, 0.9f);

            LightSource("Light_Lamp_RoomA", new Vector2(-5f, 2f), 0.9f, 5f, 0f, Vector2.right,
                new Color(1f, 0.88f, 0.65f), 1.0f);
            LightSource("Light_WindowSpill", new Vector2(-10.6f, -1f), 0.5f, 6f, 55f, Vector2.right,
                new Color(0.6f, 0.7f, 1f), 0.7f);
            LightSource("Light_Floodlight", new Vector2(7f, 5.9f), 0.85f, 5.5f, 60f, Vector2.down,
                new Color(0.95f, 0.95f, 1f), 1.1f);
            LightSource("Light_MonitorGlow", new Vector2(8f, -3f), 0.55f, 3f, 0f, Vector2.right,
                new Color(0.4f, 0.85f, 1f), 0.8f);
        }

        private static void LightSource(string name, Vector2 position, float gameIntensity,
            float range, float coneDegrees, Vector2 facing, Color color, float visualIntensity)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            if (coneDegrees > 0f) go.transform.right = facing;

            var light = go.AddComponent<AirgapLight>();
            light.Configure(gameIntensity, range, coneDegrees, 0f, 0f, true);
            AddLight2D(go.transform, range, coneDegrees, color, visualIntensity);
        }

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

        // ---- helpers -------------------------------------------------------

        private static GameObject Block(string name, Vector2 position, Vector2 size, Color color,
            bool solid, int layer, int sortingOrder, Material material)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.position = position;
            Visual(go.transform, "Sprite", size, color, sortingOrder, material);
            if (solid)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = size;
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

        private static void TraversalBox(string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = size;
            go.AddComponent<TraversalZone>();
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
            if (sprite == null) throw new System.Exception("white sprite failed to import");
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
            throw new System.Exception($"material not found: {name}");
        }
    }
}
