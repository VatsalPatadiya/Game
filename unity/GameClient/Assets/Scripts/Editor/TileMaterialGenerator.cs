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

        var tex = TileFaceTexture.Build(512, IvoryTop, IvoryBottom, Jade,
            framePadding: 0.085f, frameThickness: 0.028f, cornerRadius: 0.14f);
        File.WriteAllBytes("Assets/Textures/TileFace.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset("Assets/Textures/TileFace.png");

        var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Textures/TileFace.png");
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        var faceTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/TileFace.png");

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetTexture("_BaseMap", faceTex);
        mat.SetColor("_BaseColor", Color.white);           // tint stays white; MeshRendererTint drives free/blocked
        mat.SetFloat("_Smoothness", 0.15f);                // matte bone
        mat.SetColor("_EmissionColor", Color.black);
        AssetDatabase.CreateAsset(mat, "Assets/Materials/TileBody.mat");

        AssetDatabase.SaveAssets();
        Debug.Log("TILE_MATERIAL_GENERATOR_DONE");
    }
}
