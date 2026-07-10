using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AIRGAP.CI
{
    /// <summary>
    /// Headless player builds.
    /// Run via: Unity -batchmode -nographics -projectPath . -executeMethod AIRGAP.CI.Build.WindowsPlayer -logFile -
    /// </summary>
    public static class Build
    {
        private const string OutputPath = "Builds/Windows/Airgap.exe";

        public static void WindowsPlayer()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Bootstrap.unity" },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AIRGAP.CI] Build OK — {OutputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:F0}s)");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[AIRGAP.CI] Build FAIL — result={summary.result}, errors={summary.totalErrors}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
