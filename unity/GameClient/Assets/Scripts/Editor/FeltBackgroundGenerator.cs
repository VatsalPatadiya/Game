using System.IO;
using UnityEditor;
using UnityEngine;

// Deep-jade backdrop (premium re-theme Pass A, see
// docs/superpowers/specs/2026-09-08-premium-jade-visual-retheme-design.md):
// a deep-jade radial gradient with an intentional overhead bloom in the upper
// third, deliberately dark corners, and a very faint diagonal lattice motif
// ("felt, not seen"). Baked to a texture + material; GameSceneBuilder3D puts a
// screen-filling quad behind the board with this so the ivory tiles sit on a
// jade table instead of floating in a flat colour void. Palette/curve values
// were approved as candidate "A1 + faint lattice" (prototyped in
// scratchpad/jade_bg.py, which this bake mirrors).
public static class FeltBackgroundGenerator
{
    // Two-stage falloff (bloom core -> deep jade -> near-black edge) instead of
    // one flat lerp - a single-stage gradient reads as a tinted flat colour with
    // no depth. The bloom simulates an overhead spotlight pool on the table.
    private static readonly Color FeltHighlight = new Color(0.18f, 0.38f, 0.22f); // warmer green bloom core
    private static readonly Color FeltCentre = new Color(0.09f, 0.24f, 0.14f); // rich dark green
    private static readonly Color FeltEdge   = new Color(0.03f, 0.10f, 0.05f); // near-black green edge

    // Faint diagonal lattice tint (traditional motif) - kept low enough to read
    // as material texture, never as a visible pattern.
    private static readonly Color LatticeTint = new Color(0.18f, 0.42f, 0.24f);

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
        var rng = new System.Random(20260908);
        // Lattice frequency scales with bake resolution so the on-screen cell
        // count matches the approved 480px preview (0.045 per px at 480).
        float latticeFreq = 0.045f * 480f / size;
        // This square texture is stretched to fill a PORTRAIT screen, which would
        // make a circular radial read as a vertical ellipse. Amplifying the v term
        // of the bloom/vignette distance by the screen aspect (h/w) pre-compresses
        // it vertically so it renders as a proper CIRCLE on-screen (round-2 fix 6).
        const float ScreenAspectVY = 2340f / 1080f; // target portrait aspect
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)(size - 1) * 2f - 1f;
            float v = y / (float)(size - 1) * 2f - 1f;
            // Unity texture y=0 is the BOTTOM, so +v is toward the top of the
            // final image; the overhead bloom sits in the upper third at v=0.32.
            float vy = (v - 0.32f) * ScreenAspectVY;
            float d = Mathf.Sqrt(u * u + vy * vy);

            // Stage 1: bloom core fading to the dominant deep jade by d=0.62.
            float core = Mathf.Clamp01(d / 0.9f);
            core = core * core * (3f - 2f * core);
            var baseCol = Color.Lerp(FeltHighlight, FeltCentre, core);

            // Stage 2: deep jade fading to the near-black jade edge.
            float t = Mathf.Clamp01(d / 1.7f); // wider so the aspect-corrected vertical vignette isn't harsh
            t = t * t * (3f - 2f * t); // smooth vignette
            var c = Color.Lerp(baseCol, FeltEdge, t);

            // Deliberate corner darkening so corners read darkest and the eye is
            // drawn up-centre to the content (guidelines: corners darker on purpose).
            float cx = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v) / 1.414f);
            float cornerT = Mathf.Clamp01((cx - 0.5f) / 0.5f);
            cornerT = cornerT * cornerT * (3f - 2f * cornerT);
            float cornerMul = 1f - 0.60f * cornerT;
            c.r *= cornerMul; c.g *= cornerMul; c.b *= cornerMul;

            // Very faint diagonal lattice (two crossed sine gratings), amplitude
            // ~3% - felt as texture, not seen as a pattern.
            float latt = (0.5f + 0.5f * Mathf.Sin((x + y) * latticeFreq))
                       * (0.5f + 0.5f * Mathf.Sin((x - y) * latticeFreq));
            float lattA = (latt - 0.25f) * 0.030f;
            c.r = Mathf.Clamp01(c.r + lattA * LatticeTint.r);
            c.g = Mathf.Clamp01(c.g + lattA * LatticeTint.g);
            c.b = Mathf.Clamp01(c.b + lattA * LatticeTint.b);

            // Faint felt grain.
            float n = ((float)rng.NextDouble() - 0.5f) * 0.014f;
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
