using AIRGAP.Shared.Netcode;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AIRGAP.CI
{
    /// <summary>
    /// (Re)generates the network bootstrap scene, the player-session prefab, and the
    /// project player settings — all file-driven so no hand editor work is needed.
    /// Run via: Unity -batchmode -nographics -projectPath . -executeMethod AIRGAP.CI.SceneBootstrap.CreateBootstrapScene -logFile -
    /// </summary>
    public static class SceneBootstrap
    {
        private const string PrefabPath = "Assets/Shared/Netcode/PlayerSession.prefab";
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";

        public static void CreateBootstrapScene()
        {
            try
            {
                ConfigurePlayerSettings();
                GameObject playerPrefab = CreatePlayerPrefab();
                CreateScene(playerPrefab);

                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                AssetDatabase.SaveAssets();

                Debug.Log($"[AIRGAP.CI] SceneBootstrap OK — {ScenePath} + {PrefabPath} written, build list updated");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AIRGAP.CI] SceneBootstrap FAIL: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.productName = "AIRGAP";
            PlayerSettings.companyName = "Loamy";
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;

            // Active input handling -> Both (0 = legacy, 1 = Input System, 2 = Both):
            // action maps are the real bindings; legacy Input/IMGUI stays usable for greyboxing.
            var playerSettingsAsset = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            var serialized = new SerializedObject(playerSettingsAsset);
            SerializedProperty handler = serialized.FindProperty("activeInputHandler");
            if (handler != null)
            {
                handler.intValue = 2;
                serialized.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("[AIRGAP.CI] activeInputHandler property not found; leaving input handling as-is");
            }
        }

        private static GameObject CreatePlayerPrefab()
        {
            var temp = new GameObject("PlayerSession");
            try
            {
                temp.AddComponent<NetworkObject>();
                temp.AddComponent<PlayerSession>();
                temp.AddComponent<PingProbe>();
                PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new System.Exception($"player prefab failed to save at {PrefabPath}");
            return prefab;
        }

        private static void CreateScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraGo.transform.position = new Vector3(0, 0, -10);

            var managerGo = new GameObject("NetworkManager");
            var networkManager = managerGo.AddComponent<NetworkManager>();
            var transport = managerGo.AddComponent<UnityTransport>();
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;

            new GameObject("ConnectionBootstrap").AddComponent<ConnectionBootstrap>();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new System.Exception($"failed to save scene at {ScenePath}");
        }
    }
}
