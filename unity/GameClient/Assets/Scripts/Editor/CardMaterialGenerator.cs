using UnityEditor;
using UnityEngine;

public static class CardMaterialGenerator
{
    private const string Directory = "Assets/Materials";
    private const string MaterialPath = Directory + "/CardBody.mat";

    public static void Generate()
    {
        System.IO.Directory.CreateDirectory(Directory);

        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Card/card_rounded_rect.png");
        var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Card/card_rounded_rect_normal.png");
        if (baseTex == null || normalTex == null)
            throw new System.Exception(
                "CARD_MATERIAL_GENERATOR_MISSING_TEXTURES: run CardSpriteGenerator then CardNormalMapGenerator first");

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetTexture("_BaseMap", baseTex);
        material.SetTexture("_BumpMap", normalTex);
        material.EnableKeyword("_NORMALMAP");
        material.SetFloat("_BumpScale", 1f);
        material.SetFloat("_Smoothness", 0.35f);
        material.SetFloat("_Metallic", 0f);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        Debug.Log("CARD_MATERIAL_GENERATOR_DONE");
    }
}
