using System.IO;
using UnityEditor;
using UnityEngine;

// Wood + bronze materials for the HUD chrome (score pill, tray, popup = wood;
// control-button discs = bronze), matching the reference's warm wooden UI.
public static class WoodUiGenerator
{
    private static readonly Color WoodBottom = new Color(0.29f, 0.18f, 0.10f); // #4A2E1A
    private static readonly Color WoodTop    = new Color(0.42f, 0.27f, 0.15f); // #6B4526
    private static readonly Color Bronze     = new Color(0.725f, 0.46f, 0.19f); // #B9752F

    [MenuItem("Tools/Mahjong/Generate Wood UI")]
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Textures");
        Directory.CreateDirectory("Assets/Materials");

        // --- wood grain texture ---
        const int w = 256, h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: true) { name = "Wood", wrapMode = TextureWrapMode.Clamp };
        var rng = new System.Random(4242);
        var streak = new float[w];
        for (int x = 0; x < w; x++) streak[x] = ((float)rng.NextDouble() - 0.5f);
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);
            float t = v * v * (3f - 2f * v);
            Color baseCol = Color.Lerp(WoodBottom, WoodTop, t);
            for (int x = 0; x < w; x++)
            {
                float grain = Mathf.Sin(x * 0.20f + streak[x] * 3f) * 0.025f + streak[x] * 0.02f;
                var c = new Color(
                    Mathf.Clamp01(baseCol.r + grain),
                    Mathf.Clamp01(baseCol.g + grain * 0.9f),
                    Mathf.Clamp01(baseCol.b + grain * 0.8f), 1f);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply(true);
        File.WriteAllBytes("Assets/Textures/Wood.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset("Assets/Textures/Wood.png");
        var imp = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/Wood.png");
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = true;
        imp.SaveAndReimport();
        var woodTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Wood.png");

        var shader = Shader.Find("Universal Render Pipeline/Lit");

        var wood = LoadOrCreate("Assets/Materials/Wood.mat", shader);
        wood.SetTexture("_BaseMap", woodTex);
        wood.SetColor("_BaseColor", Color.white);
        wood.SetFloat("_Smoothness", 0.22f);
        wood.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(wood);

        // --- bronze disc texture: light warm centre -> dark rim (beveled look).
        // A URP/Lit material with a null _BaseMap renders white here, so bronze
        // needs a real texture like the wood does.
        var bLight = new Color(0.85f, 0.60f, 0.32f); // #D89A52
        var bDark  = new Color(0.48f, 0.29f, 0.13f); // #7A4A22
        var btex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: true) { name = "Bronze", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float u = x / (float)(w - 1) * 2f - 1f;
            float vv = y / (float)(h - 1) * 2f - 1f;
            float d = Mathf.Clamp01(Mathf.Sqrt(u * u + vv * vv));
            float tt = d * d * (3f - 2f * d);
            btex.SetPixel(x, y, Color.Lerp(bLight, bDark, tt));
        }
        btex.Apply(true);
        File.WriteAllBytes("Assets/Textures/Bronze.png", btex.EncodeToPNG());
        Object.DestroyImmediate(btex);
        AssetDatabase.ImportAsset("Assets/Textures/Bronze.png");
        var bimp = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/Bronze.png");
        bimp.textureType = TextureImporterType.Default; bimp.sRGBTexture = true; bimp.SaveAndReimport();
        var bronzeTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Bronze.png");

        var bronze = LoadOrCreate("Assets/Materials/Bronze.mat", shader);
        bronze.SetTexture("_BaseMap", bronzeTex);
        bronze.SetColor("_BaseColor", Color.white);
        bronze.SetFloat("_Smoothness", 0.28f);
        bronze.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(bronze);

        AssetDatabase.SaveAssets();
        Debug.Log("WOOD_UI_GENERATOR_DONE");
    }

    private static Material LoadOrCreate(string path, Shader shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else m.shader = shader;
        return m;
    }
}
