using System.IO;
using GameClient.Data;
using GameClient.Presentation.Board3D;
using UnityEditor;
using UnityEngine;

public static class TileMaterialGenerator
{
    // Widened top/bottom delta (was 0.969/0.918 -> 0.949/0.882, a ~0.05 gap
    // barely visible at tile size) for a more visible glossy sheen on the
    // card body - part of a pass giving the whole HUD/board more dimensional
    // shading instead of flat single colors.
    // Warm ivory with a very subtle top->bottom gradient (was pure white, which
    // read cold/plastic). Target #F4F1E7 -> #ECE7DA, matching the reference's
    // warm cream tile face (spec: closer to #F4F3E8 than #FFFFFF).
    private static readonly Color IvoryTop    = new Color(0.957f, 0.945f, 0.906f);
    private static readonly Color IvoryBottom = new Color(0.925f, 0.906f, 0.855f);
    // Midnight Indigo Jade (#243342) - harmonizes with #B0C4DE steel blue table backdrop.
    // Used for the tile side wall, the thin face rim, and the mesh base material.
    private static readonly Color Jade        = new Color(0.141f, 0.200f, 0.259f);

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
        // Thin, subtle dark-jade trim hugging the rim (was a thicker inset
        // picture-frame). The reference face is essentially clean ivory with only
        // a faint dark hairline just inside the edge - the green mass comes from
        // the extruded SIDE wall, not a frame painted on the face.
        var tex = TileFaceTexture.Build(texW, texH, IvoryTop, IvoryBottom, Jade,
            framePadding: 0.028f, frameThickness: 0.011f, cornerRadius: 0.15f,
            bevelStrength: 0.45f, sheenStrength: 0.05f);
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
        mat.SetFloat("_Smoothness", 0.32f);                // satin, not glossy (was 0.65 - reference faces are matte/satin)
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
            // 0.62 -> 0.78: the ivory backdrop (was steel blue) reads lighter, so
            // tiles need a stronger contact shadow to keep the same grounded,
            // "sitting on the table" depth separation.
            float a = 0.78f * (1f - t);
            // Dark GREEN-black tint (not pure black) so overlaps cast the
            // reference's deep-jade shadow onto the tile below.
            shTex.SetPixel(x, y, new Color(0.015f, 0.055f, 0.035f, Mathf.Clamp01(a)));
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
        var shadowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TileShadow.mat");
        if (shadowMat == null)
        {
            shadowMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(shadowMat, "Assets/Materials/TileShadow.mat");
        }
        shadowMat.mainTexture = shadowTex;
        shadowMat.SetFloat("_Surface", 1f);
        shadowMat.SetFloat("_Blend", 0f);
        shadowMat.renderQueue = 3000;
        shadowMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        shadowMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        shadowMat.SetInt("_ZWrite", 0);
        EditorUtility.SetDirty(shadowMat);

        var baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/TileBase.mat");
        if (baseMat == null)
        {
            baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(baseMat, "Assets/Materials/TileBase.mat");
        }
        baseMat.SetColor("_BaseColor", Jade);
        baseMat.SetFloat("_Smoothness", 0f);   // fully matte side wall - kills the glossy neon-mint highlight on the top edge
        EditorUtility.SetDirty(baseMat);

        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Mahjong/Generate Tile Back")]
    public static void GenerateCardBack()
    {
        Directory.CreateDirectory("Assets/Sprites/Tiles");

        const int texW = 512;
        int texH = Mathf.RoundToInt(texW / CardStyle.CardAspectRatio);
        // Same ivory body as the front face - reads as "part of the same
        // deck," distinguished purely by having no icon - with a bold jade
        // border (4x the front face's hairline) so the fill clearly reads as
        // contained rather than bleeding to the tile edge. An earlier pass
        // inverted this (dark jade fill, thin ivory frame) but read as too
        // dark/heavy on the table; approved direction is this lighter one.
        var tex = TileFaceTexture.Build(texW, texH, IvoryTop, IvoryBottom, Jade,
            framePadding: 0.028f, frameThickness: 0.045f, cornerRadius: 0.15f,
            bevelStrength: 0.45f, sheenStrength: 0.05f);
        File.WriteAllBytes("Assets/Sprites/Tiles/TileBack.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset("Assets/Sprites/Tiles/TileBack.png");

        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Sprites/Tiles/TileBack.png");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsToUnits = 100;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        // Now that TileFaceTexture.Build clips to the rounded silhouette (alpha=0
        // outside it), this avoids color fringing at that alpha edge under
        // compression - same reasoning as the drop-shadow sprite's importer below.
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();

        var tileSetAsset = AssetDatabase.LoadAssetAtPath<TileSetAsset>("Assets/Data/DefaultTileSet.asset");
        if (tileSetAsset != null)
        {
            tileSetAsset.CardBack = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Tiles/TileBack.png");
            EditorUtility.SetDirty(tileSetAsset);
            AssetDatabase.SaveAssets();
        }

        Debug.Log("TILE_BACK_GENERATOR_DONE");
    }
}
