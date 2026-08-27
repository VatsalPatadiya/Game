using GameClient.Presentation.Board3D;
using UnityEditor;
using UnityEngine;

public static class TileMeshGenerator
{
    private const float CardThickness = 0.18f; // real Z depth, replaces the 2D drop-shadow trick

    public static void Generate()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs");

        var root = new GameObject("Tile3D");

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "CardBody";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(
            CardStyle.CardSizeRatio * CardStyle.CardAspectRatio,
            CardStyle.CardSizeRatio,
            CardThickness);
        Object.DestroyImmediate(body.GetComponent<BoxCollider>());

        var bodyRenderer = body.GetComponent<MeshRenderer>();
        var cardMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CardBody.mat");
        if (cardMaterial == null)
            throw new System.Exception("TILE_MESH_GENERATOR_MISSING_MATERIAL: run CardMaterialGenerator first");
        bodyRenderer.sharedMaterial = cardMaterial;

        var iconGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        iconGO.name = "Icon";
        iconGO.transform.SetParent(root.transform, false);
        Object.DestroyImmediate(iconGO.GetComponent<MeshCollider>());
        iconGO.transform.localPosition = new Vector3(0f, 0f, -(CardThickness / 2f + 0.01f));
        iconGO.transform.localScale = Vector3.one * CardStyle.IconSizeRatio;
        var iconRenderer = iconGO.GetComponent<MeshRenderer>();
        var iconShader = Shader.Find("Universal Render Pipeline/Lit");
        var iconMaterial = new Material(iconShader);
        URPMaterialUtil.SetTransparent(iconMaterial); // so the icon sprite's alpha shows the card body behind it
        iconMaterial.SetFloat("_Smoothness", 0f);
        System.IO.Directory.CreateDirectory("Assets/Materials");
        AssetDatabase.CreateAsset(iconMaterial, "Assets/Materials/TileIcon.mat");
        iconRenderer.sharedMaterial = iconMaterial;

        var tileView = root.AddComponent<TileView3D>(); // auto-adds a BoxCollider to root via [RequireComponent]
        var collider = root.GetComponent<BoxCollider>();
        collider.size = new Vector3(
            CardStyle.CardSizeRatio * CardStyle.CardAspectRatio,
            CardStyle.CardSizeRatio,
            CardThickness);
        collider.center = Vector3.zero;

        var serialized = new SerializedObject(tileView);
        serialized.FindProperty("_bodyRenderer").objectReferenceValue = bodyRenderer;
        serialized.FindProperty("_bodyCollider").objectReferenceValue = collider;
        serialized.FindProperty("_iconRenderer").objectReferenceValue = iconRenderer;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Tile3D.prefab");
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log("TILE_MESH_GENERATOR_DONE");
    }
}
