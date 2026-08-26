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
            List<(string a, string b)> removalOrder, Random random, HashSet<string> excludedValues = null)
        {
            int pairCount = removalOrder.Count;
            var pool = new List<string>(pairCount);

            // Skip any value already reserved by a cleared-but-undoable cell (see
            // ShuffleService.Shuffle) so a fresh shuffle can never collide with a
            // value an in-flight Undo could restore back onto the board.
            int candidate = 0;
            int attempts = 0;
            while (pool.Count < pairCount && attempts < 1000)
            {
                string value = (candidate % 26).ToString(); // Modulo 26 to keep it within A-Z if we have many pairs
                candidate++;
                attempts++;
                if (excludedValues != null && excludedValues.Contains(value))
                    continue;
                pool.Add(value);
            }

            if (pool.Count < pairCount)
                throw new BoardGenerationException(
                    "Could not find " + pairCount + " distinct values while avoiding " +
                    (excludedValues?.Count ?? 0) + " reserved values.");

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
