using GameClient.Data;
using UnityEngine;

namespace GameClient.Presentation.Board
{
    // Icons.Length (7) * AccentColors.Length (4) = 28 unique combinations, which
    // covers the full 0-25 value range the domain layer can assign (values are
    // capped at mod 26 by ReverseConstructionSolver), so no two distinct pair
    // values ever render as the same icon+color combo. Shared by BoardView and
    // TrayView so a tile looks identical on the board and in the tray.
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
    }
}
