using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Flat, code-generated mahjong-style tile symbols (three geometric suits:
// filled dots, bamboo sticks, hollow rings) baked to transparent sprites and
// wrapped in a quad prefab each, so they drop into the same TileSet.FoodModels
// slot the 3D food prefabs used. 26 symbols cover the domain's 0-25 value range.
public static class MahjongSymbolGenerator
{
    private const int Size = 256;
    private const string TexDir = "Assets/Textures/Symbols";
    private const string MatDir = "Assets/Materials/Symbols";
    private const string PrefabDir = "Assets/Prefabs/Symbols";

    private static readonly Color Dot    = new Color(0.145f, 0.36f, 0.62f, 1f);  // blue
    private static readonly Color DotEdge = new Color(0.08f, 0.22f, 0.42f, 1f);
    private static readonly Color Bamboo  = new Color(0.16f, 0.52f, 0.30f, 1f);  // green
    private static readonly Color BambooEdge = new Color(0.09f, 0.34f, 0.19f, 1f);
    private static readonly Color Ring    = new Color(0.72f, 0.20f, 0.16f, 1f);  // red

    public static readonly string[] SymbolNames = BuildNames();

    private static string[] BuildNames()
    {
        var list = new List<string>();
        for (int i = 1; i <= 9; i++) list.Add("dots_" + i);
        for (int i = 1; i <= 9; i++) list.Add("bamboo_" + i);
        for (int i = 1; i <= 8; i++) list.Add("rings_" + i);
        return list.ToArray(); // 26
    }

    [MenuItem("Tools/Mahjong/Generate Mahjong Symbols")]
    public static void Generate()
    {
        Directory.CreateDirectory(TexDir);
        Directory.CreateDirectory(MatDir);
        Directory.CreateDirectory(PrefabDir);

        var shader = Shader.Find("Universal Render Pipeline/Lit");

        foreach (var name in SymbolNames)
        {
            var px = new Color[Size * Size]; // transparent
            int n = int.Parse(name.Substring(name.IndexOf('_') + 1));
            if (name.StartsWith("dots")) DrawDots(px, n, filled: true, Dot, DotEdge);
            else if (name.StartsWith("bamboo")) DrawBamboo(px, n);
            else DrawDots(px, n, filled: false, Ring, Ring);

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: true) { name = name };
            tex.SetPixels(px);
            tex.Apply(true);
            var path = TexDir + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.sRGBTexture = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            var symTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            var matPath = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            else mat.shader = shader;
            mat.SetTexture("_BaseMap", symTex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0f);
            // Alpha CUTOUT (not blend): transparent pixels are discarded, so no
            // opaque black square and no transparency sort order issues - crisp
            // flat icons over the tile face. (URP alpha-blend from script proved
            // unreliable in Unity 6 here.)
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.5f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            mat.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(mat);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.localScale = new Vector3(0.36f, 0.36f, 1f); // bigger symbols; x FoodAnchor(1.8) ~= 0.65 world
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
            quad.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(quad, PrefabDir + "/" + name + ".prefab");
            Object.DestroyImmediate(quad);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("MAHJONG_SYMBOL_GENERATOR_DONE");
    }

    // 3x3 grid positions (normalized), choosing a balanced subset per count.
    private static Vector2[] Positions(int n)
    {
        float a = 0.28f, b = 0.5f, c = 0.72f;
        Vector2 TL = new(a, c), TC = new(b, c), TR = new(c, c);
        Vector2 ML = new(a, b), MC = new(b, b), MR = new(c, b);
        Vector2 BL = new(a, a), BC = new(b, a), BR = new(c, a);
        return n switch
        {
            1 => new[] { MC },
            2 => new[] { TC, BC },
            3 => new[] { TL, MC, BR },
            4 => new[] { TL, TR, BL, BR },
            5 => new[] { TL, TR, MC, BL, BR },
            6 => new[] { TL, TR, ML, MR, BL, BR },
            7 => new[] { TL, TR, ML, MC, MR, BL, BR },
            8 => new[] { TL, TC, TR, ML, MR, BL, BC, BR },
            _ => new[] { TL, TC, TR, ML, MC, MR, BL, BC, BR },
        };
    }

    private static void DrawDots(Color[] px, int n, bool filled, Color fill, Color edge)
    {
        float r = n <= 4 ? 0.11f : 0.085f; // fewer dots -> bigger
        foreach (var p in Positions(n))
        {
            if (filled) Disc(px, p.x, p.y, r, fill, edge);
            else RingShape(px, p.x, p.y, r, r * 0.42f, fill);
        }
    }

    private static void DrawBamboo(Color[] px, int n)
    {
        float bw = n <= 4 ? 0.055f : 0.045f;
        float bh = n <= 3 ? 0.30f : 0.20f;
        foreach (var p in Positions(n))
            RoundBar(px, p.x, p.y, bw, bh, Bamboo, BambooEdge);
    }

    // ---- rasterizers (soft-edged, alpha-composited) ----
    private static void Disc(Color[] px, float cxN, float cyN, float rN, Color fill, Color edge)
    {
        int cx = (int)(cxN * Size), cy = (int)(cyN * Size);
        float r = rN * Size, edgeW = 2f;
        Blit(px, cx, cy, (int)(r + 3), (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float aFill = Mathf.Clamp01((r - d) / edgeW);
            float aRing = Mathf.Clamp01((edgeW * 1.5f - Mathf.Abs(d - r)) / edgeW);
            return (aRing > aFill) ? new Color(edge.r, edge.g, edge.b, aRing) : new Color(fill.r, fill.g, fill.b, aFill);
        });
    }

    private static void RingShape(Color[] px, float cxN, float cyN, float rN, float thickN, Color col)
    {
        int cx = (int)(cxN * Size), cy = (int)(cyN * Size);
        float r = rN * Size, half = thickN * Size, edgeW = 1.5f;
        Blit(px, cx, cy, (int)(r + half + 3), (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Clamp01((half - Mathf.Abs(d - r) + edgeW) / edgeW);
            return new Color(col.r, col.g, col.b, Mathf.Clamp01(a));
        });
    }

    private static void RoundBar(Color[] px, float cxN, float cyN, float wN, float hN, Color fill, Color edge)
    {
        int cx = (int)(cxN * Size), cy = (int)(cyN * Size);
        float hw = wN * Size, hh = hN * Size * 0.5f, rad = hw, edgeW = 2f;
        Blit(px, cx, cy, (int)(Mathf.Max(hw, hh) + 3), (x, y) =>
        {
            float qx = Mathf.Abs(x) - (hw - rad);
            float qy = Mathf.Abs(y) - (hh - rad);
            float d = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0))
                      + Mathf.Min(Mathf.Max(qx, qy), 0) - rad;
            float aFill = Mathf.Clamp01((-d) / edgeW);
            float aEdge = Mathf.Clamp01((edgeW - Mathf.Abs(d)) / edgeW);
            return (aEdge > aFill) ? new Color(edge.r, edge.g, edge.b, aEdge) : new Color(fill.r, fill.g, fill.b, aFill);
        });
    }

    private static void Blit(Color[] px, int cx, int cy, int rad, System.Func<int, int, Color> f)
    {
        for (int dy = -rad; dy <= rad; dy++)
        for (int dx = -rad; dx <= rad; dx++)
        {
            int x = cx + dx, y = cy + dy;
            if (x < 0 || x >= Size || y < 0 || y >= Size) continue;
            var c = f(dx, dy);
            if (c.a <= 0f) continue;
            int i = y * Size + x;
            var b = px[i];
            float outA = c.a + b.a * (1f - c.a);
            if (outA <= 0f) continue;
            px[i] = new Color(
                (c.r * c.a + b.r * b.a * (1f - c.a)) / outA,
                (c.g * c.a + b.g * b.a * (1f - c.a)) / outA,
                (c.b * c.a + b.b * b.a * (1f - c.a)) / outA,
                outA);
        }
    }
}
