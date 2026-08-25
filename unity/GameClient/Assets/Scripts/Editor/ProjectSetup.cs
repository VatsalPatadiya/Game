using UnityEditor;
using UnityEngine;

public static class ProjectSetup
{
    public static void SwitchToAndroid()
    {
        bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        Debug.Log("PROJECT_SETUP_ANDROID_SWITCH_RESULT: " + switched);
        Debug.Log("PROJECT_SETUP_ACTIVE_TARGET: " + EditorUserBuildSettings.activeBuildTarget);
    }
}
