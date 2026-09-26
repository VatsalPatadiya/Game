using System.IO;
using UnityEditor;
using UnityEngine;

// Ivory backdrop: a soft cream radial gradient with a gentle overhead bloom
// in the upper third and a cool steel-blue vignette toward the corners (the
// palette approved as splash Candidate A). Baked to a texture + material;
// GameSceneBuilder3D puts a screen-filling quad behind the board with this
// so the ivory tiles sit on a soft, airy backdrop instead of floating in a
// flat colour void. Baked at the app's native portrait resolution (not a
// square texture stretched onto a portrait screen) so every texel maps
// close to 1:1 with an on-screen pixel - crisp at any zoom, no stretch softening.
public static class FeltBackgroundGenerator
{
    // Two-stage falloff (bloom core -> dominant cream -> steel-blue vignette
    // edge) instead of one flat lerp - a single-stage gradient reads as a
    // tinted flat colour with no depth. The bloom simulates a soft overhead
    // light pool on the table; the edge stays a cool blue-grey rather than
    // going toward black, keeping the theme light and airy.
    private static readonly Color FeltHighlight = new Color(0.969f, 0.965f, 0.949f); // bright cream bloom core
    private static readonly Color FeltCentre = new Color(0.941f, 0.945f, 0.925f); // dominant ivory
    private static readonly Color FeltEdge   = new Color(0.274f, 0.353f, 0.431f); // cool steel-blue vignette

    // Faint diagonal lattice tint, kept low enough to read as material
    // texture, never as a visible pattern.
    private static readonly Color LatticeTint = new Color(0.471f, 0.549f, 0.647f);

    // Baked at the app's actual portrait resolution so the backdrop is a 1:1
    // texel-to-pixel match on device instead of a square texture stretched
    // non-uniformly onto a portrait screen.
    private const int Width = 1080;
    private const int Height = 2340;

    [MenuItem("Tools/Mahjong/Generate Felt Background")]
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Textures");
        Directory.CreateDirectory("Assets/Materials");

        var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, mipChain: true)
        {
            name = "Felt",
            wrapMode = TextureWrapMode.Clamp
        };
        var rng = new System.Random(20260925);
        // Lattice frequency scales with bake resolution so the on-screen cell
        // count matches the approved 480px preview (0.045 per px at 480).
        float latticeFreq = 0.045f * 480f / Width;

        // Two independent layers instead of one distance-chained lerp (a
        // single shared radius blew past "edge" well inside the visible
        // frame on a tall portrait canvas, drowning the ivory in blue-grey -
        // caught by rendering a numpy preview before touching Unity). Each
        // layer's distance is normalized so d=1 lands exactly at the corner
        // FARTHEST from its own centre, so it always spans the full image
        // regardless of aspect ratio - same technique as the approved splash
        // background (scratchpad/splash_v2).
        float bloomCx = Width * 0.5f;
        // Unity texture y=0 is the BOTTOM, so the overhead bloom (upper third
        // of the final on-screen image) sits at a HIGH y pixel coordinate.
        float bloomCy = Height * 0.60f;
        float bloomMaxD = Mathf.Sqrt(
            Mathf.Max(bloomCx, Width - bloomCx) * Mathf.Max(bloomCx, Width - bloomCx) +
            Mathf.Max(bloomCy, Height - bloomCy) * Mathf.Max(bloomCy, Height - bloomCy));

        float imgCx = Width * 0.5f;
        float imgCy = Height * 0.5f;
        float vigMaxD = Mathf.Sqrt(imgCx * imgCx + imgCy * imgCy);

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            // Layer 1: bloom gradient (bright core -> dominant ivory).
            float bdx = x - bloomCx, bdy = y - bloomCy;
            float dBloom = Mathf.Clamp01(Mathf.Sqrt(bdx * bdx + bdy * bdy) / bloomMaxD);
            dBloom = Mathf.Pow(dBloom, 1.5f);
            var c = Color.Lerp(FeltHighlight, FeltCentre, dBloom);

            // Layer 2: separate steel-blue vignette overlay centred on the
            // true image centre, alpha-composited on top so its strength is
            // independent of the bloom and can't dominate the whole frame.
            float vdx = x - imgCx, vdy = y - imgCy;
            float dVig = Mathf.Clamp01(Mathf.Sqrt(vdx * vdx + vdy * vdy) / vigMaxD);
            float vigAlpha = Mathf.Pow(dVig, 2f) * 0.42f;
            c = Color.Lerp(c, FeltEdge, vigAlpha);

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
