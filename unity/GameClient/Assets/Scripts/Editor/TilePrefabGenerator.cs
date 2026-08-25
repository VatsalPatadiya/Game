using System.IO;
using GameClient.Presentation.Board;
using UnityEditor;
using UnityEngine;

public static class TilePrefabGenerator
{
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Prefabs");

        var root = new GameObject("Tile");

        var background = new GameObject("Background");
        background.transform.SetParent(root.transform);
        var backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = GetOrCreateSquareSprite();
        backgroundRenderer.sortingOrder = 0;

        var icon = new GameObject("Icon");
        icon.transform.SetParent(root.transform);
        var iconRenderer = icon.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = GetOrCreateSquareSprite();
        iconRenderer.sortingOrder = 1;
        icon.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

        var collider = root.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        var tileView = root.AddComponent<TileView>();
        var serialized = new SerializedObject(tileView);
        serialized.FindProperty("_backgroundRenderer").objectReferenceValue = backgroundRenderer;
        serialized.FindProperty("_iconRenderer").objectReferenceValue = iconRenderer;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Tile.prefab");
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log("TILE_PREFAB_GENERATOR_DONE");
    }

    private static Sprite GetOrCreateSquareSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }
}
