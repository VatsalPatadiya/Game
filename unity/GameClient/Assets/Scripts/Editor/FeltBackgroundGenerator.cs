using System.IO;
using UnityEditor;
using UnityEngine;

// Warm felt-table backdrop: a radial gradient (lighter warm centre -> dark
// vignette) baked to a texture + a matte URP/Lit material. GameSceneBuilder3D
// puts a large quad behind the board with this, so the tiles sit on a table
// instead of floating in a flat colour void.
public static class FeltBackgroundGenerator
{
    // Two-stage falloff (bright core -> mid felt -> near-black edge) instead
    // of one flat lerp - a single-stage gradient read as a slightly-tinted
    // flat color with no real depth. The bright core simulates an overhead
    // spotlight pool on the table, matching the reference's richer backdrop.
    private static readonly Color FeltHighlight = new Color(0.34f, 0.53f, 0.42f); // #57875B bright warm core
    private static readonly Color FeltCentre = new Color(0.243f, 0.404f, 0.325f); // #3E6753 warm felt
    private static readonly Color FeltEdge   = new Color(0.018f, 0.040f, 0.031f); // #04140A deeper still than before

    [MenuItem("Tools/Mahjong/Generate Felt Background")]
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Textures");
        Directory.CreateDirectory("Assets/Materials");

        const int size = 1024;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
        {
            name = "Felt",
            wrapMode = TextureWrapMode.Clamp
        };
        var rng = new System.Random(20260827);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)(size - 1) * 2f - 1f;
            float v = y / (float)(size - 1) * 2f - 1f;
            // centre of the glow sits slightly above middle, like an overhead lamp
            float d = Mathf.Sqrt(u * u + (v - 0.18f) * (v - 0.18f));

            // Stage 1: bright core fading to base felt by d=0.6.
            float core = Mathf.Clamp01(d / 0.6f);
            core = core * core * (3f - 2f * core);
            var baseCol = Color.Lerp(FeltHighlight, FeltCentre, core);

            // Stage 2: base felt fading to near-black edge.
            float t = Mathf.Clamp01(d / 1.05f); // tighter radius than before (was 1.35) so the dark edge reaches in further, matching the reference's stronger falloff
            t = t * t * (3f - 2f * t); // smooth vignette
            var c = Color.Lerp(baseCol, FeltEdge, t);
            float n = ((float)rng.NextDouble() - 0.5f) * 0.018f; // faint felt grain
            c.r = Mathf.Clamp01(c.r + n);
            c.g = Mathf.Clamp01(c.g + n);
            c.b = Mathf.Clamp01(c.b + n);
            tex.SetPixel(x, y, c);
        }
        tex.Apply(updateMipmaps: true);
        File.WriteAllBytes("Assets/Textures/Felt.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset("Assets/Textures/Felt.png");
        var imp = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/Felt.png");
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        var feltTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Felt.png");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Felt.mat");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, "Assets/Materials/Felt.mat");
        }
        else
        {
            mat.shader = shader;
        }
        mat.SetTexture("_BaseMap", feltTex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.05f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Cull", 0f); // double-sided so the tilted-camera orientation never culls it
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("FELT_BACKGROUND_GENERATOR_DONE");
    }
}
