using System.IO;
using GameClient.Data;
using UnityEditor;
using UnityEngine;

public static class DataAssetGenerator
{
    // The shipped tile art (full card faces, not bare symbol glyphs) - see
    // Tile3D.prefab's Body sprite and TileVisual.IconFor, which reads this
    // list directly. Do not point this back at Assets/Textures/Icons/icon_*.png
    // (an older, superseded generation pass) - that set has no card backing
    // and makes board tiles render as floating bare symbols.
    private static readonly string[] IconNames =
    {
        "Tile_1", "Tile_2", "Tile_3", "Tile_4", "Tile_5", "Tile_6", "Tile_7", "Tile_8", "Tile_9"
    };

    private const string IconFolder = "Assets/Sprites/Tiles/";

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
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + IconNames[i] + ".png");
            if (sprite == null)
                throw new System.Exception(
                    "DATA_ASSET_GENERATOR_MISSING_ICON: " + IconNames[i] + " - expected hand-authored tile art at " + IconFolder);
            tileSet.Icons[i] = sprite;
        }
        tileSet.AccentColors = AccentColors;

        // Tile faces now use flat mahjong-symbol quad prefabs (dots/bamboo/rings)
        // instead of the 3D food models. They flow through the same FoodModels
        // slot so the board/tray rendering pipeline is unchanged.
        tileSet.FoodModels = new GameObject[MahjongSymbolGenerator.SymbolNames.Length];
        for (int i = 0; i < MahjongSymbolGenerator.SymbolNames.Length; i++)
        {
            var name = MahjongSymbolGenerator.SymbolNames[i];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Symbols/" + name + ".prefab");
            if (prefab == null)
                throw new System.Exception(
                    "DATA_ASSET_GENERATOR_MISSING_SYMBOL: " + name + " - run MahjongSymbolGenerator.Generate() first");
            tileSet.FoodModels[i] = prefab;
        }

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
