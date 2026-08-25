using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Gameplay;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Gameplay
{
    public class HintFinderTests
    {
        [Test]
        public void FindFreePair_ReturnsAValidFreeMatchingPair()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var slotsById = shape.ToDictionary(s => s.Id);
            var board = new BoardState
            {
                Cells = new Dictionary<string, TileCell>
                {
                    ["L0_0"] = new TileCell { Value = "a" },
                    ["L0_1"] = new TileCell { Value = "b" },
                    ["L0_2"] = new TileCell { Value = "b" },
                    ["L0_3"] = new TileCell { Value = "a" }
                }
            };

            var hint = HintFinder.FindFreePair(board, slotsById);

            Assert.That(hint, Is.Not.Null);
            Assert.That(new[] { hint.Value.slotIdA, hint.Value.slotIdB }, Is.EquivalentTo(new[] { "L0_0", "L0_3" }));
        }

        [Test]
        public void FindFreePair_NoFreeMatch_ReturnsNull()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var slotsById = shape.ToDictionary(s => s.Id);
            var board = new BoardState
            {
                Cells = new Dictionary<string, TileCell>
                {
                    ["L0_0"] = new TileCell { Value = "a" },
                    ["L0_1"] = new TileCell { Value = "x" },
                    ["L0_2"] = new TileCell { Value = "x" },
                    ["L0_3"] = new TileCell { Value = "b" }
                }
            };

            var hint = HintFinder.FindFreePair(board, slotsById);

            Assert.That(hint, Is.Null);
        }
    }
}
