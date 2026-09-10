using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Generation
{
    public class BranchingOrderBuilderTests
    {
        [Test]
        public void Build_CoversEverySlotExactlyOnce_Pairs()
        {
            var shape = TestLayoutShapes.MediumShape();
            var slotsById = shape.ToDictionary(s => s.Id);
            var ids = new HashSet<string>(slotsById.Keys);

            var order = BranchingOrderBuilder.Build(slotsById, ids, new Random(1), 2, 0.8f);

            Assert.That(order, Is.Not.Null);
            var flat = order.SelectMany(g => g).ToList();
            Assert.That(flat.Count, Is.EqualTo(ids.Count));
            Assert.That(new HashSet<string>(flat), Is.EquivalentTo(ids));
            Assert.That(order, Has.All.Matches<string[]>(g => g.Length == 2));
        }

        [Test]
        public void Build_FrontLoadedExposure_GivesHigherOpeningBranchingThanMinimized()
        {
            // Same shape/seed: a large opening fraction should produce an opening with
            // more simultaneous matches than an almost-zero opening fraction.
            var shape = TestLayoutShapes.LargeShape();
            var slotsById = shape.ToDictionary(s => s.Id);
            var ids = new HashSet<string>(slotsById.Keys);

            var easyOrder = BranchingOrderBuilder.Build(slotsById, new HashSet<string>(ids), new Random(5), 2, 0.9f);
            var hardOrder = BranchingOrderBuilder.Build(slotsById, new HashSet<string>(ids), new Random(5), 2, 0.0f);

            var easy = BranchingSimulator.Profile(slotsById, easyOrder);
            var hard = BranchingSimulator.Profile(slotsById, hardOrder);

            int window = Math.Max(1, easy.Count / 4);
            double easyOpening = easy.Take(window).Average();
            double hardOpening = hard.Take(window).Average();

            Assert.That(easyOpening, Is.GreaterThan(hardOpening));
        }

        [Test]
        public void Build_ReturnsNull_WhenFewerThanGroupSizeFreeTiles()
        {
            // A single row of 4 (cols=4) exposes only its two ends as free (interior
            // tiles are blocked). Asking for triples (groupSize 3) can never satisfy
            // 3 co-free tiles at step 0, so Build must return null.
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var slotsById = shape.ToDictionary(s => s.Id);
            var ids = new HashSet<string>(slotsById.Keys);

            var order = BranchingOrderBuilder.Build(slotsById, ids, new System.Random(1), 3, 0.5f);

            Assert.That(order, Is.Null);
        }

        [Test]
        public void Build_Triples_GroupsOfThree()
        {
            // LayeredRowShapeBuilder wraps at cols=4, so new[]{9} is three chains
            // (4,4,1), not a single row. The shape exposes at least 3 free tiles at
            // each step, so triples (groupSize 3) can always be satisfied.
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 9 });
            var slotsById = shape.ToDictionary(s => s.Id);
            var ids = new HashSet<string>(slotsById.Keys);

            var order = BranchingOrderBuilder.Build(slotsById, ids, new Random(2), 3, 0.5f);

            Assert.That(order, Is.Not.Null);
            Assert.That(order, Has.All.Matches<string[]>(g => g.Length == 3));
            Assert.That(order.SelectMany(g => g).Count(), Is.EqualTo(ids.Count));
        }
    }
}
