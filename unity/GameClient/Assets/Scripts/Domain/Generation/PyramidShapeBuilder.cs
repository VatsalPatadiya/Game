using System;
using System.Collections.Generic;
using GameDomain.Model;

namespace GameDomain.Generation
{
    public static class PyramidShapeBuilder
    {
        public static List<TileSlot> BuildRandom(int targetTileCount, Random random)
        {
            // Instead of random scattering, let's build a structured 3-layer pyramid layout.
            // Layer 0: 6x4 rectangle (24 tiles)
            // Layer 1: 4x2 rectangle centered (8 tiles)
            // Layer 2: 2x1 rectangle centered (2 tiles)
            // Total: 34 tiles

            var slots = new List<TileSlot>();
            var byPos = new Dictionary<(int x, int y, int l), TileSlot>();

            // Layer 0
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    AddSlot(slots, byPos, x, y, 0);
                }
            }

            // Layer 1
            for (int y = 1; y < 3; y++)
            {
                for (int x = 1; x < 5; x++)
                {
                    AddSlot(slots, byPos, x, y, 1);
                }
            }

            // Layer 2
            for (int y = 1; y < 3; y++)
            {
                for (int x = 2; x < 4; x++)
                {
                    AddSlot(slots, byPos, x, y, 2);
                }
            }

            int cols = 6;
            int maxLayers = 3;

            // Calculate Left, Right, and CoveredBy
            foreach (var slot in slots)
            {
                int x = slot.X;
                int y = -slot.Y; // Revert back to positive array index
                int l = slot.Layer;

                // Left neighbor
                if (x > 0 && byPos.ContainsKey((x - 1, y, l)))
                {
                    slot.LeftNeighborId = byPos[(x - 1, y, l)].Id;
                }

                // Right neighbor
                if (x < cols - 1 && byPos.ContainsKey((x + 1, y, l)))
                {
                    slot.RightNeighborId = byPos[(x + 1, y, l)].Id;
                }

                // If there is a tile directly above us on layer + 1
                if (l < maxLayers - 1 && byPos.TryGetValue((x, y, l + 1), out var tileAbove))
                {
                    slot.CoveredByIds.Add(tileAbove.Id);
                }
            }

            return slots;
        }

        private static void AddSlot(List<TileSlot> slots, Dictionary<(int x, int y, int l), TileSlot> byPos, int x, int y, int l)
        {
            var slot = new TileSlot
            {
                Id = $"T_{x}_{y}_{l}",
                X = x,
                Y = -y, // Render downwards in Unity space
                Layer = l,
                CoveredByIds = new List<string>()
            };
            slots.Add(slot);
            byPos[(x, y, l)] = slot;
        }
    }
}
