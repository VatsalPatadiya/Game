using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// One-time setup: TMPro.TMP_Settings.defaultFontAsset is null in this project
// because every TextMeshPro component was created via editor script
// (GameSceneBuilder3D) rather than through the Editor UI, which is what
// normally prompts the "Import TMP Essentials" dialog. Without it,
// TextMeshPro.Awake() throws a NullReferenceException on every TMP object
// at runtime. Run this once via -executeMethod (no -quit — it exits itself
// once the async import completes).
public static class TMPEssentialsImporter
{
    public static void Import()
    {
        var packageCacheDir = Path.GetFullPath("Library/PackageCache");
        var uguiDir = Directory.GetDirectories(packageCacheDir, "com.unity.ugui@*").FirstOrDefault();
        if (uguiDir == null)
        {
            Debug.LogError("TMP_ESSENTIALS_IMPORT_FAILED: com.unity.ugui package not found in PackageCache");
            EditorApplication.Exit(1);
            return;
        }

        var packagePath = Path.Combine(uguiDir, "Package Resources", "TMP Essential Resources.unitypackage");
        if (!File.Exists(packagePath))
        {
            Debug.LogError("TMP_ESSENTIALS_IMPORT_FAILED: unitypackage not found at " + packagePath);
            EditorApplication.Exit(1);
            return;
        }

        AssetDatabase.importPackageCompleted += _ =>
        {
            Debug.Log("TMP_ESSENTIALS_IMPORT_DONE");
            EditorApplication.Exit(0);
        };
        AssetDatabase.importPackageFailed += (name, error) =>
        {
            Debug.LogError("TMP_ESSENTIALS_IMPORT_FAILED: " + error);
            EditorApplication.Exit(1);
        };

        AssetDatabase.ImportPackage(packagePath, false);
    }
}
