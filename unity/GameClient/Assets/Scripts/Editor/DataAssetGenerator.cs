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
    //
    // Index order below is GAMEPLAY-LOAD-BEARING, not cosmetic: a tile's
    // assigned value indexes directly into this array (TileVisual.IconFor),
    // and TileMatchRules.BucketKey() reserves index ranges 34-37 and 38-41
    // as the Flower/Season wildcard groups (any tile in one of those ranges
    // matches any other tile in the SAME range, regardless of exact value -
    // standard Mahjong Solitaire rule). Reordering this array without
    // updating TileMatchRules breaks that mapping.
    private static readonly string[] IconNames =
    {
        // 0-33: the 34 exact-match suit/honor tiles - only an identical
        // value matches another identical value.
        "dots_1", "dots_2", "dots_3", "dots_4", "dots_5", "dots_6", "dots_7", "dots_8", "dots_9",
        "bamboo_1", "bamboo_2", "bamboo_3", "bamboo_4", "bamboo_5", "bamboo_6", "bamboo_7", "bamboo_8", "bamboo_9",
        "characters_1", "characters_2", "characters_3", "characters_4", "characters_5", "characters_6", "characters_7", "characters_8", "characters_9",
        "wind_east", "wind_north", "wind_south", "wind_west",
        "dragon_green", "dragon_red", "dragon_white",
        // 34-37: Flowers - wildcard group, order among these four doesn't matter.
        "flower_bamboo_leaf", "flower_chrysanthemum", "flower_orchid", "flower_plum",
        // 38-41: Seasons - wildcard group, order among these four doesn't matter.
        "season_autumn", "season_spring", "season_summer", "season_winter",
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

        const string tokensPath = "Assets/Data/DefaultAccessibilityTokens.asset";
        if (AssetDatabase.LoadAssetAtPath<AccessibilityTokens>(tokensPath) != null)
            AssetDatabase.DeleteAsset(tokensPath);
        var tokens = ScriptableObject.CreateInstance<AccessibilityTokens>();
        AssetDatabase.CreateAsset(tokens, tokensPath);

        var tileSet = ScriptableObject.CreateInstance<TileSetAsset>();
        tileSet.TileSetId = "default";
        tileSet.Icons = new Sprite[IconNames.Length];
        for (int i = 0; i < IconNames.Length; i++)
        {
            string path = IconFolder + IconNames[i] + ".png";
            EnsureSpriteImportSettings(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
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

        const string tileSetPath = "Assets/Data/DefaultTileSet.asset";
        if (AssetDatabase.LoadAssetAtPath<TileSetAsset>(tileSetPath) != null)
            AssetDatabase.DeleteAsset(tileSetPath);
        AssetDatabase.CreateAsset(tileSet, tileSetPath);

        const string levelPath = "Assets/Data/SmallTestLevel.asset";
        if (AssetDatabase.LoadAssetAtPath<LevelShapeAsset>(levelPath) != null)
            AssetDatabase.DeleteAsset(levelPath);
        var level = ScriptableObject.CreateInstance<LevelShapeAsset>();
        level.LevelId = 1;
        level.RowLengthsByLayer = new[] { 8 };
        level.TileSetId = "default";
        AssetDatabase.CreateAsset(level, levelPath);

        AssetDatabase.SaveAssets();
        Debug.Log("DATA_ASSET_GENERATOR_DONE");
    }

    // Freshly-copied PNGs land with Unity's Default texture-import settings,
    // which AssetDatabase.LoadAssetAtPath<Sprite> can't resolve (returns
    // null) - force Sprite/Single, same fix as UpdateTileSetAsset.cs.
    private static void EnsureSpriteImportSettings(string path)
    {
        AssetDatabase.ImportAsset(path);
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (changed) importer.SaveAndReimport();
        }
    }
}
