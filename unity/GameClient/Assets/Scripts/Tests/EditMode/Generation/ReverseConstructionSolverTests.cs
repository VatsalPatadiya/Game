using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Generation
{
    public class ReverseConstructionSolverTests
    {
        [Test]
        public void TryBuildRemovalOrder_CoversEverySlotExactlyOnce()
        {
            var shape = TestLayoutShapes.MediumShape();
            var slotsById = shape.ToDictionary(s => s.Id);
            var allIds = new HashSet<string>(slotsById.Keys);

            var order = ReverseConstructionSolver.TryBuildRemovalOrder(slotsById, allIds, new Random(1));

            Assert.That(order, Is.Not.Null);
            var coveredIds = order.SelectMany(pair => new[] { pair.a, pair.b }).ToList();
            Assert.That(coveredIds.Count, Is.EqualTo(allIds.Count));
            Assert.That(new HashSet<string>(coveredIds), Is.EquivalentTo(allIds));
        }

        [Test]
        public void AssignValuesFromRemovalOrder_GivesEachPairASharedUniqueValue()
        {
            var shape = TestLayoutShapes.SmallShape();
            var slotsById = shape.ToDictionary(s => s.Id);
            var allIds = new HashSet<string>(slotsById.Keys);
            var order = ReverseConstructionSolver.TryBuildRemovalOrder(slotsById, allIds, new Random(2));

            var values = ReverseConstructionSolver.AssignValuesFromRemovalOrder(order, new Random(3));

            Assert.That(values.Count, Is.EqualTo(allIds.Count));
            foreach (var pair in order)
            {
                Assert.That(values[pair.a], Is.EqualTo(values[pair.b]));
            }
            var distinctValues = values.Values.Distinct().Count();
            Assert.That(distinctValues, Is.EqualTo(order.Count));
        }
    }
}
