using System.Collections.Generic;

namespace GameDomain.Model
{
    public sealed class LevelDefinition
    {
        public int LevelId;
        public List<TileSlot> Shape = new List<TileSlot>();
        public string TileSetId;
        public int MovesBudget = -1; // <= 0 means unlimited
    }
}
