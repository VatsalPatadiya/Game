using UnityEditor;
using UnityEngine;

public static class AndroidBuilder
{
    public static void Build()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
        PlayerSettings.applicationIdentifier = "com.gameclient.mahjong";
        // Lets Android switch to a higher display mode (90/120Hz) when the
        // device panel supports one, matching the runtime's
        // Application.targetFrameRate = 120 (see GameController.Start).
        PlayerSettings.Android.optimizedFramePacing = true;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Game.unity" },
            locationPathName = "Builds/GameClient.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development | BuildOptions.AllowDebugging
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("ANDROID_BUILD_RESULT: " + report.summary.result);
        Debug.Log("ANDROID_BUILD_TOTAL_ERRORS: " + report.summary.totalErrors);
        Debug.Log("ANDROID_BUILD_OUTPUT_PATH: " + report.summary.outputPath);
    }
}
