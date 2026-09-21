using GameClient.Data;
using UnityEngine;

namespace GameClient.Presentation.Board
{
    // Icons is 1:1 with the value range a tile can be assigned (42 entries -
    // see DataAssetGenerator.IconNames and TileMatchRules for the exact
    // ordering/wildcard ranges), so IconFor alone already gives every value
    // its own distinct tile face - no accent-color multiplier needed.
    // AccentColorFor/FoodModelFor below are unused by the live render path
    // (TileView3D.Initialize takes a single Sprite) - kept for now, not
    // wired to anything.
    public static class TileVisual
    {
        public static Sprite IconFor(TileSetAsset tileSet, string value)
        {
            int index = int.Parse(value);
            return tileSet.Icons[index % tileSet.Icons.Length];
        }

        public static Color AccentColorFor(TileSetAsset tileSet, string value)
        {
            int index = int.Parse(value);
            int colorIndex = (index / tileSet.Icons.Length) % tileSet.AccentColors.Length;
            return tileSet.AccentColors[colorIndex];
        }

        public static GameObject FoodModelFor(TileSetAsset tileSet, string value)
        {
            int index = int.Parse(value);
            return tileSet.FoodModels[index % tileSet.FoodModels.Length];
        }
    }
}
