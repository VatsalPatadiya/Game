using System.Collections.Generic;

namespace GameDomain.Model
{
    public sealed class Move
    {
        public string SlotIdA;
        public string SlotIdB;
        public string ValueA;
        public string ValueB;

        // Tray triple-match: the full set of slot ids cleared together (3 for a
        // triple) and their shared value. SlotIdA/B/ValueA/B stay populated with
        // the first two ids so the pair-based ShuffleService/UndoStack still read
        // a valid value. Null for legacy pair moves.
        public List<string> ClearedSlotIds;
        public string Value;
    }
}
