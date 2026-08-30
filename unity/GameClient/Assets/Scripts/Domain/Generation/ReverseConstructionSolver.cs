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

        // Triple-match variant: builds a forward clearing order in GROUPS OF THREE
        // simultaneously-free tiles. Following this order (tap a group's 3 tiles
        // back-to-back) clears them as a triple with the tray never exceeding 3, so
        // a board built from it is always beatable. Returns null if fewer than 3
        // free tiles ever remain (caller retries with a new seed).
        public static List<string[]> TryBuildRemovalOrderTriples(
            Dictionary<string, TileSlot> slotsById, HashSet<string> slotIds, Random random)
        {
            var remaining = new HashSet<string>(slotIds);
            var order = new List<string[]>();

            while (remaining.Count > 0)
            {
                var freeSlots = FreedomRuleCalculator.ComputeFreeSlots(slotsById, remaining);
                if (freeSlots.Count < 3)
                    return null;

                // pick 3 distinct free tiles
                var picked = new string[3];
                var chosen = new HashSet<int>();
                for (int k = 0; k < 3; k++)
                {
                    int idx;
                    do { idx = random.Next(freeSlots.Count); } while (!chosen.Add(idx));
                    picked[k] = freeSlots[idx].Id;
                }

                order.Add(picked);
                foreach (var id in picked)
                    remaining.Remove(id);
            }

            return order;
        }

        // Assigns each triple a distinct value (mod 26), so every value appears
        // exactly 3 times on the board.
        public static Dictionary<string, string> AssignValuesFromRemovalOrderTriples(
            List<string[]> removalOrder, Random random)
        {
            int groupCount = removalOrder.Count;
            var pool = new List<string>(groupCount);
            for (int i = 0; i < groupCount; i++)
                pool.Add((i % 26).ToString());

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            var values = new Dictionary<string, string>();
            for (int i = 0; i < removalOrder.Count; i++)
                foreach (var id in removalOrder[i])
                    values[id] = pool[i];

            return values;
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
