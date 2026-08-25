using System.Collections.Generic;
using GameDomain.Model;

namespace GameDomain.Tests.Fixtures
{
    public static class TestLayoutShapes
    {
        public static List<TileSlot> BuildLayeredRowShape(int[] rowLengthsByLayer)
        {
            var slots = new List<TileSlot>();
            var byLayerIndex = new Dictionary<(int layer, int index), TileSlot>();

            for (int layer = 0; layer < rowLengthsByLayer.Length; layer++)
            {
                int length = rowLengthsByLayer[layer];
                for (int index = 0; index < length; index++)
                {
                    var slot = new TileSlot
                    {
                        Id = "L" + layer + "_" + index,
                        X = index,
                        Y = 0,
                        Layer = layer,
                        CoveredByIds = new List<string>(),
                        LeftNeighborId = index > 0 ? "L" + layer + "_" + (index - 1) : null,
                        RightNeighborId = index < length - 1 ? "L" + layer + "_" + (index + 1) : null
                    };
                    slots.Add(slot);
                    byLayerIndex[(layer, index)] = slot;
                }
            }

            // Upper-layer slot at index i covers lower-layer slots at index i and i+1,
            // producing the classic overlapping "turtle" pyramid structure.
            for (int layer = 1; layer < rowLengthsByLayer.Length; layer++)
            {
                int upperLength = rowLengthsByLayer[layer];
                int lowerLength = rowLengthsByLayer[layer - 1];
                for (int index = 0; index < upperLength; index++)
                {
                    var upperSlot = byLayerIndex[(layer, index)];
                    if (index < lowerLength && byLayerIndex.TryGetValue((layer - 1, index), out var lowerA))
                        lowerA.CoveredByIds.Add(upperSlot.Id);
                    if (index + 1 < lowerLength && byLayerIndex.TryGetValue((layer - 1, index + 1), out var lowerB))
                        lowerB.CoveredByIds.Add(upperSlot.Id);
                }
            }

            return slots;
        }

        public static List<TileSlot> SmallShape() => BuildLayeredRowShape(new[] { 8 });
        public static List<TileSlot> MediumShape() => BuildLayeredRowShape(new[] { 12, 6 });
        public static List<TileSlot> LargeShape() => BuildLayeredRowShape(new[] { 20, 12, 6, 2 });
    }
}
