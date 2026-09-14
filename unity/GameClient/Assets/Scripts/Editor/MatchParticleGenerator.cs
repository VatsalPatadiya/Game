using System.IO;
using UnityEditor;
using UnityEngine;

// Bakes the two particle textures MatchCelebrationController uses for its
// tray-match burst: a soft round glow (the bulk of the burst) and a 4-point
// sparkle/twinkle (a handful of brighter accent particles). Unity's particle
// system has no built-in shape - without an explicit alpha-cutout texture
// like these, the renderer falls back to its default opaque white texture
// and every particle reads as a plain square.
public static class MatchParticleGenerator
{
    private const int Size = 64;

    [MenuItem("Tools/Mahjong/Generate Match Particle Textures")]
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Textures");
        Directory.CreateDirectory("Assets/Materials");

        BakeGlow();
        BakeSparkle();
    }

    private static void BakeGlow()
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false)
        { name = "MatchParticleGlow", wrapMode = TextureWrapMode.Clamp };

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float u = (x + 0.5f) / Size * 2f - 1f;
            float v = (y + 0.5f) / Size * 2f - 1f;
            float d = Mathf.Sqrt(u * u + v * v);
            // Bright core fading softly to a fully transparent rim - a round
            // dot with no hard edge, unlike the shader's default white square.
            float a = 1f - SmoothStep01(0.35f, 1f, d);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        tex.Apply();
        SaveTextureAndMaterial(tex, "MatchParticleGlow");
    }

    private static void BakeSparkle()
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false)
        { name = "MatchParticleSparkle", wrapMode = TextureWrapMode.Clamp };

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float u = (x + 0.5f) / Size * 2f - 1f;
            float v = (y + 0.5f) / Size * 2f - 1f;
            float d = Mathf.Sqrt(u * u + v * v);

            // Rounded 4-point sparkle (fatter lobes)
            float angle = Mathf.Atan2(v, u);
            float shape = 0.15f + 0.65f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 2f)), 0.6f);
            float core = 1f - SmoothStep01(shape - 0.15f, shape + 0.05f, d);
            
            // Soft white circular background (lower opacity so the sparkle pops)
            float bg = (1f - SmoothStep01(0.7f, 0.95f, d)) * 0.35f; 
            
            float a = Mathf.Clamp01(core + bg);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        tex.Apply();
        SaveTextureAndMaterial(tex, "MatchParticleSparkle");
    }

    private static void SaveTextureAndMaterial(Texture2D tex, string name)
    {
        string texPath = "Assets/Textures/" + name + ".png";
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

        // Sprite type + alphaIsTransparency, not Default: Default-type textures
        // on this project's Android target compress to a format that drops the
        // alpha channel entirely, silently turning a soft/cutout shape into an
        // opaque square on-device (the exact bug this whole file exists to
        // avoid) - same fix already proven for the HUD icon/badge glyphs.
        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        var loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        var shader = Shader.Find("Sprites/Default");

        string matPath = "Assets/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else
        {
            mat.shader = shader;
        }
        mat.SetTexture("_MainTex", loadedTex);
        mat.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
    }

    // GLSL-style smoothstep: 0 below edge0, 1 above edge1, smooth in between
    // (matches the helper duplicated in TileFaceTexture/WoodUiGenerator).
    private static float SmoothStep01(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
