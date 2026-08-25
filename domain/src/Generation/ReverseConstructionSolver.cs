using System;
using System.Collections.Generic;
using GameDomain.Model;

namespace GameDomain.Generation
{
    public static class ReverseConstructionSolver
    {
        public static List<(string a, string b)> TryBuildRemovalOrder(
            Dictionary<string, TileSlot> slotsById, HashSet<string> slotIds, Random random)
        {
            var remaining = new HashSet<string>(slotIds);
            var order = new List<(string a, string b)>();

            while (remaining.Count > 0)
            {
                var freeSlots = FreedomRuleCalculator.ComputeFreeSlots(slotsById, remaining);
                if (freeSlots.Count < 2)
                    return null;

                var a = freeSlots[random.Next(freeSlots.Count)];
                TileSlot b;
                do
                {
                    b = freeSlots[random.Next(freeSlots.Count)];
                } while (b.Id == a.Id);

                order.Add((a.Id, b.Id));
                remaining.Remove(a.Id);
                remaining.Remove(b.Id);
            }

            return order;
        }

        public static Dictionary<string, string> AssignValuesFromRemovalOrder(
            List<(string a, string b)> removalOrder, Random random)
        {
            int pairCount = removalOrder.Count;
            var pool = new List<string>(pairCount);
            for (int i = 0; i < pairCount; i++)
                pool.Add("tile_" + i);

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            var values = new Dictionary<string, string>();
            for (int i = removalOrder.Count - 1; i >= 0; i--)
            {
                var pair = removalOrder[i];
                string value = pool[i];
                values[pair.a] = value;
                values[pair.b] = value;
            }

            return values;
        }
    }
}
