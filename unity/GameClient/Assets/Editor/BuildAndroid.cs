using UnityEditor;
using UnityEngine;

public static class BuildAndroid
{
    // Builds the Android APK using the first scene in the build settings (or specify explicitly).
    public static void BuildAPK()
    {
        // Define which scenes to include in the build. Adjust as needed.
        string[] scenes = { "Assets/Scenes/Game.unity" };

        // Ensure the output directory exists.
        string outputPath = "Build/Android/MyGame.apk";
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath));

        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = scenes;
        options.locationPathName = outputPath;
        options.target = BuildTarget.Android;
        options.options = BuildOptions.None;

        // Perform the build and log the result.
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"Android build succeeded: {summary.totalSize / (1024 * 1024)} MB");
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError("Android build failed.");
        }
    }
}
