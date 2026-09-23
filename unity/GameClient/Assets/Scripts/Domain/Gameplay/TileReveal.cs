using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    // Owns the reveal/peek state machine for hidden (face-down) tiles. Caller
    // contract: only call TryReveal when the cell's Revealed is currently
    // false (GameController.OnTileTapped enforces this before calling in).
    // See docs/superpowers/specs/2026-09-23-hidden-tile-reveal-design.md.
    public static class TileReveal
    {
        // Attempts to reveal a currently-face-down tile. Returns false if it
        // isn't free right now (caller treats this exactly like today's
        // invalid-tap case - same shake/vibrate/sound). On success, sets
        // reHiddenSlotId to whichever OTHER tile got auto-re-hidden as a side
        // effect (null if none), so the caller knows which other tile view to
        // flip back down.
        public static bool TryReveal(
            BoardState board, Dictionary<string, TileSlot> slotsById, string slotId,
            out string reHiddenSlotId)
        {
            reHiddenSlotId = null;

            var remaining = new HashSet<string>(
                board.Cells.Where(kv => !kv.Value.Cleared && !board.TrayTileIds.Contains(kv.Key))
                    .Select(kv => kv.Key));
            if (!FreedomRuleCalculator.IsFree(slotsById[slotId], remaining))
                return false;

            if (board.PeekedTileId != null && board.PeekedTileId != slotId)
            {
                reHiddenSlotId = board.PeekedTileId;
                board.Cells[reHiddenSlotId].Revealed = false;
            }

            board.Cells[slotId].Revealed = true;
            board.PeekedTileId = slotId;
            return true;
        }
    }
}
