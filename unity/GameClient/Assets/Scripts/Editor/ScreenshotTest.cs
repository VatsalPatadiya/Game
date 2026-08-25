using System.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public static class ScreenshotTest
{
    public static void Run()
    {
        EditorApplication.isPlaying = true;
        EditorApplication.update += OnUpdate;
    }

    private static float timer = 0f;
    private static bool captured = false;

    private static void OnUpdate()
    {
        if (!EditorApplication.isPlaying) return;

        timer += Time.deltaTime;
        if (timer > 2f && !captured)
        {
            captured = true;
            var path = System.IO.Path.GetFullPath("screenshot.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("SCREENSHOT_CAPTURED: " + path);
            EditorApplication.isPlaying = false;
        }
        else if (timer > 3f)
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.Exit(0);
        }
    }
}
