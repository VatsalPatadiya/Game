using GameClient.Presentation.Board3D;
using UnityEditor;
using UnityEngine;

public static class TileMeshGenerator
{
    private const float CardThickness = 0.24f;

    public static void Generate()
    {
        System.IO.Directory.CreateDirectory("Assets/Prefabs");

        var root = new GameObject("Tile3D");
        
        var body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        
        var bodyRenderer = body.AddComponent<SpriteRenderer>();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Tiles/dots_1.png");
        bodyRenderer.sprite = sprite;
        
        if (sprite != null)
        {
            float targetHeight = CardStyle.CardSizeRatio; // 0.92
            float spriteHeight = sprite.bounds.size.y;
            float scaleFactor = spriteHeight > 0f ? (targetHeight / spriteHeight) : 1f;
            body.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
        }
        
        var collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(
            CardStyle.CardSizeRatio * CardStyle.CardAspectRatio,
            CardStyle.CardSizeRatio,
            CardThickness);
        collider.center = Vector3.zero;

        var tileView = root.AddComponent<TileView3D>();

        var serialized = new SerializedObject(tileView);
        serialized.FindProperty("_bodyRenderer").objectReferenceValue = bodyRenderer;
        serialized.FindProperty("_bodyCollider").objectReferenceValue = collider;
        serialized.FindProperty("_freeCardColor").colorValue = Color.white;
        serialized.FindProperty("_highlightColor").colorValue = new Color(1f, 0.9f, 0.6f, 1f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Tile3D.prefab");
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log("TILE_MESH_GENERATOR_DONE (SVG DYNAMIC SCALE)");
    }
}
