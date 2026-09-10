using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;

namespace GameDomain.Tests.Generation
{
    public class PaletteSelectorTests
    {
        // 6 models: clusters {0,1,2}=A(0), {3,4}=B(1), {5}=unique(-1)
        private static readonly int[] Clusters = { 0, 0, 0, 1, 1, -1 };

        private static List<string[]> Order(int groups)
        {
            var o = new List<string[]>();
            for (int i = 0; i < groups; i++) o.Add(new[] { "s" + (2 * i), "s" + (2 * i + 1) });
            return o;
        }

        [Test]
        public void AssignValues_AllTilesInAGroupShareValue()
        {
            var values = PaletteSelector.AssignValues(Order(3), Clusters, 6, 0, new Random(1));
            foreach (var g in Order(3))
                Assert.That(values[g[0]], Is.EqualTo(values[g[1]]));
        }

        [Test]
        public void AssignValues_Level0_NoTwoValuesShareACluster()
        {
            // 3 groups, 3 clusters available -> all distinct clusters feasible.
            var values = PaletteSelector.AssignValues(Order(3), Clusters, 6, 0, new Random(2));
            var distinctModels = values.Values.Select(int.Parse).Distinct().ToList();
            var usedClusters = distinctModels.Select(m => Clusters[m] == -1 ? -100 - m : Clusters[m]).ToList();
            Assert.That(usedClusters.Count, Is.EqualTo(usedClusters.Distinct().Count()));
        }

        [Test]
        public void AssignValues_HighConfusability_PutsLookAlikesOnBoard()
        {
            // 3 groups, level 2 -> should draw >=2 models from the same cluster.
            var values = PaletteSelector.AssignValues(Order(3), Clusters, 6, 2, new Random(3));
            var distinctModels = values.Values.Select(int.Parse).Distinct().ToList();
            var realClusters = distinctModels.Select(m => Clusters[m]).Where(c => c >= 0).ToList();
            bool hasLookAlikePair = realClusters.GroupBy(c => c).Any(grp => grp.Count() >= 2);
            Assert.That(hasLookAlikePair, Is.True);
        }

        [Test]
        public void AssignValues_ValuesStayInModelRange()
        {
            var values = PaletteSelector.AssignValues(Order(10), Clusters, 6, 3, new Random(4));
            Assert.That(values.Values, Has.All.Matches<string>(v =>
            {
                int m = int.Parse(v);
                return m >= 0 && m < 6;
            }));
        }
    }
}
