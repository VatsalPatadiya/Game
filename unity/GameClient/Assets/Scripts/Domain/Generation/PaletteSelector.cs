using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDomain.Generation
{
    // Maps each removal group to a food-model value (0..modelCount-1). Confusability 0
    // draws every value from a distinct similarity cluster (nothing looks alike); level
    // k allows up to k+1 values from the same cluster (look-alikes co-occur). Two
    // groups may share a value when variety runs out (safe: only adds match options).
    public static class PaletteSelector
    {
        // The number of distinct values a level may draw from must never exceed
        // the number of icons that can actually render them, or two different
        // values can land on the same icon (Icons[value % Icons.Length]) while
        // still comparing unequal everywhere match/hint/shuffle logic checks raw
        // Value equality - tiles that look identical then never clear together.
        // Callers should pass the live tile set's icon count; iconCount <= 0
        // (tile set not yet available) falls back to a safe default.
        public static int ResolveModelCount(int iconCount, int fallback = 26) =>
            iconCount > 0 ? iconCount : fallback;

        public static Dictionary<string, string> AssignValues(
            List<string[]> removalOrder, int[] clusterIdByModel, int modelCount,
            int confusabilityLevel, Random random)
        {
            // Cluster -> its member model indices. Uniques (-1) become singleton clusters.
            var clusters = new Dictionary<int, List<int>>();
            for (int m = 0; m < modelCount; m++)
            {
                int cid = (clusterIdByModel != null && m < clusterIdByModel.Length) ? clusterIdByModel[m] : -1;
                int key = cid >= 0 ? cid : (-100 - m); // unique key per singleton
                if (!clusters.TryGetValue(key, out var list)) { list = new List<int>(); clusters[key] = list; }
                list.Add(m);
            }

            int maxPerCluster = Math.Max(1, confusabilityLevel + 1);

            // Build the value pool: walk clusters (shuffled), taking up to maxPerCluster
            // models from each, until we have one value per group (or run out).
            var clusterKeys = clusters.Keys.ToList();
            Shuffle(clusterKeys, random);

            var pool = new List<int>();
            foreach (var key in clusterKeys)
            {
                var members = new List<int>(clusters[key]);
                Shuffle(members, random);
                for (int i = 0; i < members.Count && i < maxPerCluster; i++)
                {
                    pool.Add(members[i]);
                    if (pool.Count >= removalOrder.Count) break;
                }
                if (pool.Count >= removalOrder.Count) break;
            }

            // Not enough distinct values under the cap: cycle the pool (value reuse is safe).
            if (pool.Count == 0) pool.Add(0);

            var values = new Dictionary<string, string>();
            for (int i = 0; i < removalOrder.Count; i++)
            {
                string v = pool[i % pool.Count].ToString();
                foreach (var id in removalOrder[i]) values[id] = v;
            }
            return values;
        }

        private static void Shuffle<T>(IList<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
