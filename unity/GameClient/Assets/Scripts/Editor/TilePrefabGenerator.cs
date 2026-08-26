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

        var shadow = CreateSlicedChild(root.transform, "Shadow",
            new Vector2(CardStyle.ShadowSizeRatio, CardStyle.ShadowSizeRatio), -2,
            new Vector3(0.05f, -0.05f, 0.06f), CardStyle.ShadowColor);

        var selectionGlow = CreateSlicedChild(root.transform, "SelectionGlow",
            new Vector2(CardStyle.GlowSizeRatio, CardStyle.GlowSizeRatio), -1,
            Vector3.zero, CardStyle.GlowColor);

        var accent = CreateSlicedChild(root.transform, "AccentBorder",
            new Vector2(CardStyle.AccentSizeRatio, CardStyle.AccentSizeRatio), 0,
            Vector3.zero, CardStyle.AccentDefaultColor);

        var card = CreateSlicedChild(root.transform, "Card",
            new Vector2(CardStyle.CardSizeRatio, CardStyle.CardSizeRatio), 1,
            Vector3.zero, CardStyle.CardColor);

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(root.transform, false);
        iconGO.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        iconGO.transform.localScale = new Vector3(CardStyle.IconSizeRatio, CardStyle.IconSizeRatio, 1f);
        var iconRenderer = iconGO.AddComponent<SpriteRenderer>();
        iconRenderer.drawMode = SpriteDrawMode.Simple;
        iconRenderer.sortingOrder = 2;

        var collider = root.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        var tileView = root.AddComponent<TileView>();
        var serialized = new SerializedObject(tileView);
        serialized.FindProperty("_shadowRenderer").objectReferenceValue = shadow;
        serialized.FindProperty("_selectionGlowRenderer").objectReferenceValue = selectionGlow;
        serialized.FindProperty("_accentRenderer").objectReferenceValue = accent;
        serialized.FindProperty("_cardRenderer").objectReferenceValue = card;
        serialized.FindProperty("_iconRenderer").objectReferenceValue = iconRenderer;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Tile.prefab");
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log("TILE_PREFAB_GENERATOR_DONE");
    }

    private static SpriteRenderer CreateSlicedChild(
        Transform parent, string name, Vector2 size, int sortingOrder, Vector3 localPos, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = GetOrCreateSquareSprite();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.sortingOrder = sortingOrder;
        renderer.color = color;
        return renderer;
    }

    private static Sprite GetOrCreateSquareSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }
}
