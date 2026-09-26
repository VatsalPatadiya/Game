using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Assigns the LogoGenerator-produced textures to every Android icon slot
// (Adaptive foreground+background layers, plus the legacy Round/Legacy
// single-layer fallbacks for pre-Android-8 launchers). One source texture
// per layer is enough - Unity bakes down to each DPI bucket at build time.
public static class IconAssigner
{
    [MenuItem("Tools/Branding/Assign Celestial Tiles Mahjong App Icon")]
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

        var androidTarget = NamedBuildTarget.Android;

        foreach (var kind in PlayerSettings.GetSupportedIconKinds(androidTarget))
        {
            var icons = PlayerSettings.GetPlatformIcons(androidTarget, kind);
            bool isAdaptive = kind.ToString().StartsWith("Adaptive");

            foreach (var icon in icons)
            {
                if (isAdaptive)
                {
                    // Layer index 0 -> ic_launcher_background, index 1 ->
                    // ic_launcher_foreground in Unity's actual Android
                    // export (verified by unzipping a built APK's res/
                    // mipmap-xxxhdpi-v4/ - opposite of what the layer names
                    // "foreground"/"background" would suggest).
                    icon.SetTexture(background, 0);
                    icon.SetTexture(foreground, 1);
                }
                else
                {
                    icon.SetTexture(flatMark, 0);
                }
            }

            PlayerSettings.SetPlatformIcons(androidTarget, kind, icons);
        }

        // iOS has no adaptive-layer concept (that's PlatformIconKind, Android-only) -
        // it uses the legacy IconKind.Application slot set (App Store, Spotlight,
        // Settings, etc. sizes), all filled from the same opaque flat mark.
        var iosTarget = NamedBuildTarget.iOS;
        var iosSizeCount = PlayerSettings.GetIconSizes(iosTarget, IconKind.Application).Length;
        var iosIcons = new Texture2D[iosSizeCount];
        for (int i = 0; i < iosSizeCount; i++)
        {
            iosIcons[i] = flatMark;
        }
        PlayerSettings.SetIcons(iosTarget, iosIcons, IconKind.Application);

        AssetDatabase.SaveAssets();
        Debug.Log("ICON_ASSIGNER_DONE");
    }
}
