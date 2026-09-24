using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Assigns the LogoGenerator-produced textures to every Android icon slot
// (Adaptive foreground+background layers, plus the legacy Round/Legacy
// single-layer fallbacks for pre-Android-8 launchers). One source texture
// per layer is enough - Unity bakes down to each DPI bucket at build time.
public static class IconAssigner
{
    [MenuItem("Tools/Branding/Assign Mahjong Sanctuary App Icon")]
    public static void Assign()
    {
        var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Branding/Icon_Foreground.png");
        var background = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Branding/Icon_Background.png");
        var flatMark = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Branding/Logo_Mark.png");

        if (foreground == null || background == null || flatMark == null)
        {
            Debug.LogError("IconAssigner: run LogoGenerator.Generate() first - one or more source textures missing.");
            return;
        }

        var target = NamedBuildTarget.Android;

        foreach (var kind in PlayerSettings.GetSupportedIconKinds(target))
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            bool isAdaptive = kind.ToString().StartsWith("Adaptive");

            foreach (var icon in icons)
            {
                if (isAdaptive)
                {
                    icon.SetTexture(foreground, 0);
                    icon.SetTexture(background, 1);
                }
                else
                {
                    icon.SetTexture(flatMark, 0);
                }
            }

            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("ICON_ASSIGNER_DONE");
    }
}
