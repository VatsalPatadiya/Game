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

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Game.unity" },
            locationPathName = "Builds/GameClient.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("ANDROID_BUILD_RESULT: " + report.summary.result);
        Debug.Log("ANDROID_BUILD_TOTAL_ERRORS: " + report.summary.totalErrors);
        Debug.Log("ANDROID_BUILD_OUTPUT_PATH: " + report.summary.outputPath);
    }
}
