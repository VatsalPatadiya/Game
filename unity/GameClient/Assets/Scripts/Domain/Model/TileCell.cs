namespace GameDomain.Model
{
    public sealed class TileCell
    {
        public string Value;
        public bool Cleared;
        // Permanent - set once at generation by HiddenTileSelector, never
        // changes. Distinguishes "was originally a hidden tile" from "is
        // currently showing its front" (Revealed), which Undo needs to tell
        // apart (see TileReveal / GameController.AnimateUndo).
        public bool IsHiddenTile;
        // Mutable. True for every normal tile from generation on. False only
        // for IsHiddenTile cells until the player reveals them (TileReveal);
        // can flip back to false if a different hidden tile gets revealed
        // first (see BoardState.PeekedTileId).
        public bool Revealed = true;
    }
}
