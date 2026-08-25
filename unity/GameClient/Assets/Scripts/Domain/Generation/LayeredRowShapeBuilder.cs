using System.Collections.Generic;
using GameDomain.Model;

namespace GameDomain.Generation
{
    public static class LayeredRowShapeBuilder
    {
        public static List<TileSlot> Build(int[] rowLengthsByLayer)
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
    }
}
