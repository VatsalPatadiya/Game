using System.IO;
using GameClient.Presentation.Board3D;
using UnityEditor;
using UnityEngine;

public static class TileMaterialGenerator
{
    // Widened top/bottom delta (was 0.969/0.918 -> 0.949/0.882, a ~0.05 gap
    // barely visible at tile size) for a more visible glossy sheen on the
    // card body - part of a pass giving the whole HUD/board more dimensional
    // shading instead of flat single colors.
    private static readonly Color IvoryTop    = new Color(0.99f, 0.972f, 0.93f);  // #FCF8ED
    private static readonly Color IvoryBottom = new Color(0.87f, 0.825f, 0.72f);  // #DED2B8
    private static readonly Color Jade        = new Color(0.184f, 0.541f, 0.329f); // #2F8A54

    [MenuItem("Tools/Mahjong/Generate Tile Material")]
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Textures");
        Directory.CreateDirectory("Assets/Materials");

        // Portrait texture matching the tile aspect (CardAspectRatio = width/height)
        // so the jade edge is a uniform stroke all round. Padding pulled in from
        // the very edge (was 0.012, hugging the perimeter) to a visible inset so
        // the frame reads as a border sitting slightly inside the tile, not a
        // trim line flush with the silhouette. Fractions of tile WIDTH.
        const int texW = 512;
        int texH = Mathf.RoundToInt(texW / CardStyle.CardAspectRatio);
        // bevelStrength/sheenStrength give the face a raised lacquered edge + a
        // soft top-left specular pool (premium re-theme Pass B, guidelines s5).
        // Approved as candidate "diagonal + bevel rim" (scratchpad/tile_face.py).
        var tex = TileFaceTexture.Build(texW, texH, IvoryTop, IvoryBottom, Jade,
            framePadding: 0.045f, frameThickness: 0.018f, cornerRadius: 0.15f,
            bevelStrength: 0.5f, sheenStrength: 0.06f);
        File.WriteAllBytes("Assets/Textures/TileFace.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset("Assets/Textures/TileFace.png");

        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/TileFace.png");
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        var faceTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/TileFace.png");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TileBody.mat");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, "Assets/Materials/TileBody.mat");
        }
        else
        {
            mat.shader = shader;
        }
        mat.SetTexture("_BaseMap", faceTex);
        mat.SetColor("_BaseColor", Color.white);           // tint stays white; MeshRendererTint drives free/blocked
        mat.SetFloat("_Smoothness", 0.15f);                // matte bone
        mat.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(mat);

        BakeDropShadow();

        AssetDatabase.SaveAssets();
        Debug.Log("TILE_MATERIAL_GENERATOR_DONE");
    }

    // Soft rounded drop-shadow sprite + an unlit transparent material. Each tile
    // prefab carries a quad using this behind its body (see TileMeshGenerator),
    // giving the mock's tight contact shadow reliably and cheaply - independent
    // of real-time shadow budget on low-end devices.
    private static void BakeDropShadow()
    {
        const int size = 256;
        var shTex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: true)
        {
            name = "TileShadow",
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)(size - 1);
            float v = y / (float)(size - 1);
            float px = Mathf.Abs(u - 0.5f);
            float py = Mathf.Abs(v - 0.5f);
            float half = 0.5f - 0.10f;    // extent inside the sprite
            float inner = half - 0.16f;   // straight-section half-size
            float qx = Mathf.Max(px - inner, 0f);
            float qy = Mathf.Max(py - inner, 0f);
            float d = Mathf.Sqrt(qx * qx + qy * qy) - 0.16f; // rounded-rect SDF, <0 inside
            float t = Mathf.Clamp01((d + 0.14f) / 0.20f);    // 0 well inside -> 1 outside, soft band
            t = t * t * (3f - 2f * t);
            float a = 0.34f * (1f - t); // was 0.28 - nudged up for clearer depth separation between stacked layers; still short of the original un-softened value to avoid muddying the dense straddle
            shTex.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Clamp01(a)));
        }
        shTex.Apply(updateMipmaps: true);
        File.WriteAllBytes("Assets/Textures/TileShadow.png", shTex.EncodeToPNG());
        Object.DestroyImmediate(shTex);
        AssetDatabase.ImportAsset("Assets/Textures/TileShadow.png");
        var shImp = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/TileShadow.png");
        shImp.textureType = TextureImporterType.Default;
        shImp.alphaIsTransparency = true;
        shImp.sRGBTexture = true;
        shImp.wrapMode = TextureWrapMode.Clamp;
        shImp.SaveAndReimport();
        var shadowTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/TileShadow.png");

        // URP/Lit + SetTransparent is the project's proven transparent path (the
        // HUD icons use it); Unlit needs different blend keywords and rendered
        // the shadow opaque-black. Black albedo stays dark under any light, and
        // the sprite's alpha drives the soft falloff.
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var shadowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TileShadow.mat");
        if (shadowMat == null)
        {
            shadowMat = new Material(shader);
            AssetDatabase.CreateAsset(shadowMat, "Assets/Materials/TileShadow.mat");
        }
        else
        {
            shadowMat.shader = shader;
        }
        shadowMat.SetTexture("_BaseMap", shadowTex);
        shadowMat.SetColor("_BaseColor", new Color(0f, 0f, 0f, 1f)); // black; alpha comes from the sprite
        shadowMat.SetFloat("_Smoothness", 0f);
        shadowMat.SetColor("_EmissionColor", Color.black);
        URPMaterialUtil.SetTransparent(shadowMat);
        shadowMat.SetFloat("_Cull", 0f); // double-sided: tile-facing orientation never culls it
        EditorUtility.SetDirty(shadowMat);
    }
}
