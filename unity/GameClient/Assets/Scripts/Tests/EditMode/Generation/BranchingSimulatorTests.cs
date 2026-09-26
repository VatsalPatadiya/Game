using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Generation
{
    public class BranchingSimulatorTests
    {
        [Test]
        public void Profile_SingleFlatRow_AllGroupsFreeAtStart()
        {
            // 8 tiles, single layer, no covering and no left/right neighbors: every
            // tile is free from the start, so at step 0 all 4 pairs are
            // simultaneously free -> branching 4, then 3, 2, 1.
            // NOTE: TestLayoutShapes.SmallShape() is NOT used here because its
            // builder wraps an 8-length single layer into two rows of 4 (cols is
            // hardcoded to 4), which leaves interior tiles blocked at the start
            // (see FreedomRuleCalculatorTests.IsFree_InteriorOfFlatRow_IsNotFreeInitially).
            // That contradicts this test's "every tile free" premise, so the slots
            // are built directly here instead.
            var ids = new List<string>();
            var slotsById = new Dictionary<string, TileSlot>();
            for (int i = 0; i < 8; i++)
            {
                var id = "T" + i;
                ids.Add(id);
                slotsById[id] = new TileSlot { Id = id };
            }

            var order = new List<string[]>
            {
                new[] { ids[0], ids[1] },
                new[] { ids[2], ids[3] },
                new[] { ids[4], ids[5] },
                new[] { ids[6], ids[7] },
            };

            var curve = BranchingSimulator.Profile(slotsById, order);

            Assert.That(curve, Is.EqualTo(new List<int> { 4, 3, 2, 1 }));
        }

        [Test]
        public void Profile_LengthEqualsGroupCount()
        {
            var shape = TestLayoutShapes.MediumShape();
            var slotsById = shape.ToDictionary(s => s.Id);
            var ids = new HashSet<string>(slotsById.Keys);
            var order = ReverseConstructionSolver.TryBuildRemovalOrder(slotsById, ids, new System.Random(1))
                .Select(p => new[] { p.a, p.b }).ToList();

            var curve = BranchingSimulator.Profile(slotsById, order);

            Assert.That(curve.Count, Is.EqualTo(order.Count));
            Assert.That(curve[curve.Count - 1], Is.EqualTo(1)); // last group is always the only one free
        }
    }
}
