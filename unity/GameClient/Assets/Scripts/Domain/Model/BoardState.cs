using System.Collections.Generic;

namespace GameDomain.Model
{
    public sealed class BoardState
    {
        public int LevelId;
        public Dictionary<string, TileCell> Cells = new Dictionary<string, TileCell>();
        public List<Move> MoveHistory = new List<Move>();
        public int Score;
        public int ComboCount;
    }
}
