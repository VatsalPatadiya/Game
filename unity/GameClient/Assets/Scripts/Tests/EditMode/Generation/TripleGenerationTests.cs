using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Tests.Generation
{
    public class TripleGenerationTests
    {
        [Test]
        public void TurtleShape_Has48Tiles_DivisibleBy3()
        {
            var shape = TurtleShapeBuilder.Build();
            Assert.That(shape.Count, Is.EqualTo(48));
            Assert.That(shape.Count % 3, Is.EqualTo(0));
        }

        [Test]
        public void TryBuildRemovalOrderTriples_CoversEverySlotInGroupsOfThree()
        {
            var shape = TurtleShapeBuilder.Build();
            var slotsById = shape.ToDictionary(s => s.Id);
            var allIds = new HashSet<string>(slotsById.Keys);

            var order = ReverseConstructionSolver.TryBuildRemovalOrderTriples(slotsById, allIds, new Random(1));

            Assert.That(order, Is.Not.Null);
            Assert.That(order.All(g => g.Length == 3), Is.True);
            var covered = order.SelectMany(g => g).ToList();
            Assert.That(covered.Count, Is.EqualTo(allIds.Count));
            Assert.That(new HashSet<string>(covered), Is.EquivalentTo(allIds));
        }

        [Test]
        public void GenerateTriples_AssignsEveryValueAMultipleOfThreeTimes()
        {
            var shape = TurtleShapeBuilder.Build();
            var level = new LevelDefinition { LevelId = 1, Shape = shape, TileSetId = "default" };

            var board = BoardGenerator.GenerateTriples(level, new Random(42));

            Assert.That(board.Cells.Count, Is.EqualTo(shape.Count));
            foreach (var group in board.Cells.Values.GroupBy(c => c.Value))
                Assert.That(group.Count() % 3, Is.EqualTo(0),
                    "value " + group.Key + " appeared " + group.Count() + " times (not a multiple of 3)");
        }

        [Test]
        public void GenerateTriples_SucceedsAcrossManySeeds()
        {
            var shape = TurtleShapeBuilder.Build();
            var level = new LevelDefinition { LevelId = 1, Shape = shape, TileSetId = "default" };

            for (int seed = 0; seed < 100; seed++)
                Assert.DoesNotThrow(() => BoardGenerator.GenerateTriples(level, new Random(seed)),
                    "GenerateTriples threw for seed " + seed);
        }
    }
}
