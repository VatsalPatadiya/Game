using System.Collections.Generic;

namespace GameDomain.Model
{
    public sealed class TileSlot
    {
        public string Id;
        public int X;
        public int Y;
        public int Layer;
        public List<string> CoveredByIds = new List<string>();
        public string LeftNeighborId;
        public string RightNeighborId;
    }
}
