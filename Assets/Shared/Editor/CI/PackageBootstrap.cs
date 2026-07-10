using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace AIRGAP.CI
{
    /// <summary>
    /// One-shot CI helpers for provisioning the project from the command line.
    /// Run via: Unity -batchmode -nographics -projectPath . -executeMethod AIRGAP.CI.PackageBootstrap.InstallNetcode -logFile -
    /// </summary>
    public static class PackageBootstrap
    {
        public static void InstallNetcode()
        {
            AddRequest request = Client.Add("com.unity.netcode.gameobjects");
            while (!request.IsCompleted)
            {
                Thread.Sleep(100);
            }

            if (request.Status == StatusCode.Success)
            {
                Debug.Log($"[AIRGAP.CI] Installed {request.Result.name}@{request.Result.version}");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[AIRGAP.CI] Netcode install failed: {request.Error?.message}");
                EditorApplication.Exit(1);
            }
        }
    }
}
