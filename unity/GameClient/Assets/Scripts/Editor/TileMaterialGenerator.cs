using System.IO;
using GameClient.Presentation.Board3D;
using UnityEditor;
using UnityEngine;

public static class TileMaterialGenerator
{
    private static readonly Color IvoryTop    = new Color(0.969f, 0.949f, 0.902f); // #F7F2E6
    private static readonly Color IvoryBottom = new Color(0.918f, 0.882f, 0.788f); // #EAE1C9
    private static readonly Color Jade        = new Color(0.184f, 0.541f, 0.329f); // #2F8A54

    [MenuItem("Tools/Mahjong/Generate Tile Material")]
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Textures");
        Directory.CreateDirectory("Assets/Materials");

        // Portrait texture matching the tile aspect (CardAspectRatio = width/height)
        // so the jade frame is a uniform inset all round. Frame values match the
        // approved mock (fractions of tile WIDTH): padding 0.055, stroke 0.022,
        // corner 0.11.
        const int texW = 512;
        int texH = Mathf.RoundToInt(texW / CardStyle.CardAspectRatio);
        var tex = TileFaceTexture.Build(texW, texH, IvoryTop, IvoryBottom, Jade,
            framePadding: 0.055f, frameThickness: 0.022f, cornerRadius: 0.11f);
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
            float a = 0.42f * (1f - t);
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
