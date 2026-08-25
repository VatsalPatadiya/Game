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
        backgroundRenderer.drawMode = SpriteDrawMode.Sliced;
        backgroundRenderer.size = new Vector2(0.9f, 0.9f);
        backgroundRenderer.sortingOrder = 0;

        var icon = new GameObject("Icon");
        icon.transform.SetParent(root.transform);
        var iconRenderer = icon.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = GetOrCreateSquareSprite();
        iconRenderer.drawMode = SpriteDrawMode.Sliced;
        iconRenderer.size = new Vector2(0.7f, 0.7f);
        iconRenderer.sortingOrder = 1;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(root.transform);
        var textMesh = textGO.AddComponent<TextMesh>();
        textMesh.characterSize = 0.2f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.black;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textMesh.font = font;
        textGO.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        // Move it slightly forward in Z so it renders above sprites (or use sorting order if using TMP, but TextMesh uses Z)
        textGO.transform.localPosition = new Vector3(0f, 0f, -0.1f);

        var collider = root.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        var tileView = root.AddComponent<TileView>();
        var serialized = new SerializedObject(tileView);
        serialized.FindProperty("_backgroundRenderer").objectReferenceValue = backgroundRenderer;
        serialized.FindProperty("_iconRenderer").objectReferenceValue = iconRenderer;
        serialized.FindProperty("_textMesh").objectReferenceValue = textMesh;
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
