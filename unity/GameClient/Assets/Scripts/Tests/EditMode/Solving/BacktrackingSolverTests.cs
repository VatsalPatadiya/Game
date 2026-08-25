using NUnit.Framework;
using System.Collections.Generic;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Solving
{
    public class BacktrackingSolverTests
    {
        [Test]
        public void IsSolvable_ReturnsTrue_ForAKnownSolvableFlatBoard()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var values = new Dictionary<string, string>
            {
                ["L0_0"] = "a",
                ["L0_1"] = "b",
                ["L0_2"] = "b",
                ["L0_3"] = "a"
            };

            Assert.That(BacktrackingSolver.IsSolvable(shape, values), Is.True);
        }

        [Test]
        public void IsSolvable_ReturnsFalse_ForAnUnsolvableFlatBoard()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var values = new Dictionary<string, string>
            {
                ["L0_0"] = "a",
                ["L0_1"] = "a",
                ["L0_2"] = "b",
                ["L0_3"] = "b"
            };

            Assert.That(BacktrackingSolver.IsSolvable(shape, values), Is.False);
        }
    }
}
