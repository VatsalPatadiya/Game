using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

// Bakes a TextMeshPro SDF font asset from the bundled Cinzel OFL display font
// (premium re-theme Pass E, guidelines s7). Cinzel is used for headings/numbers
// (LEVEL, score, PLAY); LiberationSans stays the body font. The asset is created
// dynamic (source TTF is bundled) and pre-populated with the glyphs the HUD uses
// so they're baked into the atlas up front.
public static class PremiumFontGenerator
{
    private const string TtfPath = "Assets/Fonts/Cinzel.ttf";
    private const string AssetPath = "Assets/Fonts/Cinzel SDF.asset";

    [MenuItem("Tools/Mahjong/Generate Premium Font")]
    public static void Generate()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (font == null)
            throw new System.Exception("PREMIUM_FONT_MISSING: " + TtfPath + " not imported");

        var fontAsset = TMP_FontAsset.CreateFontAsset(font);
        if (fontAsset == null)
            throw new System.Exception("PREMIUM_FONT_CREATE_FAILED");
        fontAsset.name = "Cinzel SDF";

        // Pre-bake the glyphs the HUD renders so they land in the atlas now.
        const string charset =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 x:.,!-'";
        fontAsset.TryAddCharacters(charset);

        if (File.Exists(AssetPath)) AssetDatabase.DeleteAsset(AssetPath);
        AssetDatabase.CreateAsset(fontAsset, AssetPath);

        // Fold the atlas texture(s) + material in as sub-assets so the .asset is
        // self-contained (otherwise they're lost on reload).
        if (fontAsset.atlasTextures != null)
        {
            foreach (var tex in fontAsset.atlasTextures)
            {
                if (tex == null) continue;
                tex.name = "Cinzel Atlas";
                if (!AssetDatabase.Contains(tex))
                    AssetDatabase.AddObjectToAsset(tex, fontAsset);
            }
        }
        if (fontAsset.material != null)
        {
            fontAsset.material.name = "Cinzel SDF Material";
            if (!AssetDatabase.Contains(fontAsset.material))
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("PREMIUM_FONT_GENERATOR_DONE");
    }
}
