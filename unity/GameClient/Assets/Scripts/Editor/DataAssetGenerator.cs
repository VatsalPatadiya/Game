using System.IO;
using GameClient.Data;
using UnityEditor;
using UnityEngine;

public static class DataAssetGenerator
{
    private static readonly string[] IconNames =
    {
        "icon_dots", "icon_flower", "icon_star", "icon_diamond", "icon_ring", "icon_cross", "icon_leaf"
    };

    private static readonly Color[] AccentColors =
    {
        new Color(176f / 255f, 66f / 255f, 40f / 255f, 1f),  // terracotta
        new Color(150f / 255f, 92f / 255f, 0f / 255f, 1f),   // dark amber
        new Color(18f / 255f, 97f / 255f, 97f / 255f, 1f),   // teal
        new Color(94f / 255f, 51f / 255f, 133f / 255f, 1f),  // plum
    };

    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Data");

        var tokens = ScriptableObject.CreateInstance<AccessibilityTokens>();
        AssetDatabase.CreateAsset(tokens, "Assets/Data/DefaultAccessibilityTokens.asset");

        var tileSet = ScriptableObject.CreateInstance<TileSetAsset>();
        tileSet.TileSetId = "default";
        tileSet.Icons = new Sprite[IconNames.Length];
        for (int i = 0; i < IconNames.Length; i++)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Icons/" + IconNames[i] + ".png");
            if (sprite == null)
                throw new System.Exception(
                    "DATA_ASSET_GENERATOR_MISSING_ICON: " + IconNames[i] + " - run TileIconGenerator.Generate() first");
            tileSet.Icons[i] = sprite;
        }
        tileSet.AccentColors = AccentColors;
        AssetDatabase.CreateAsset(tileSet, "Assets/Data/DefaultTileSet.asset");

        var level = ScriptableObject.CreateInstance<LevelShapeAsset>();
        level.LevelId = 1;
        level.RowLengthsByLayer = new[] { 8 };
        level.TileSetId = "default";
        AssetDatabase.CreateAsset(level, "Assets/Data/SmallTestLevel.asset");

        AssetDatabase.SaveAssets();
        Debug.Log("DATA_ASSET_GENERATOR_DONE");
    }
}
