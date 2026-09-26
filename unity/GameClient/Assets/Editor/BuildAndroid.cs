using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

public static class BuildAndroid
{
    // Builds the Android APK. Called via -executeMethod BuildAndroid.BuildAPK
    public static void BuildAPK()
    {
        string projectPath = Path.GetFullPath(".");
        string outputDir  = Path.Combine(projectPath, "Builds", "Android");
        string outputPath = Path.Combine(outputDir, "MyGame.apk");

        Directory.CreateDirectory(outputDir);

        string[] scenes = { "Assets/Scenes/Game.unity" };

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes           = scenes,
            locationPathName = outputPath,
            target           = BuildTarget.Android,
            options          = BuildOptions.None
        };

        Debug.Log($"[BuildAndroid] Building APK to: {outputPath}");

        BuildReport  report  = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BuildAndroid] Build SUCCEEDED. Size: {summary.totalSize / (1024 * 1024)} MB. Path: {outputPath}");
        }
        else
        {
            Debug.LogError($"[BuildAndroid] Build FAILED. Result: {summary.result}");
            EditorApplication.Exit(1);
        }
    }
}
