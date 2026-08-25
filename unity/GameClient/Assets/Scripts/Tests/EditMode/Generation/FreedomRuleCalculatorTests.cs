using NUnit.Framework;
using System.Collections.Generic;
using GameDomain.Generation;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Generation
{
    public class FreedomRuleCalculatorTests
    {
        [Test]
        public void IsFree_EndsOfFlatRow_AreFreeInitially()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var remaining = new HashSet<string>(new[] { "L0_0", "L0_1", "L0_2", "L0_3" });

            Assert.That(FreedomRuleCalculator.IsFree(shape[0], remaining), Is.True);
            Assert.That(FreedomRuleCalculator.IsFree(shape[3], remaining), Is.True);
        }

        [Test]
        public void IsFree_InteriorOfFlatRow_IsNotFreeInitially()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var remaining = new HashSet<string>(new[] { "L0_0", "L0_1", "L0_2", "L0_3" });

            Assert.That(FreedomRuleCalculator.IsFree(shape[1], remaining), Is.False);
            Assert.That(FreedomRuleCalculator.IsFree(shape[2], remaining), Is.False);
        }

        [Test]
        public void IsFree_LowerSlotCoveredByUpperSlot_IsNotFree()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 2, 1 });
            var slotsById = new Dictionary<string, TileSlot>();
            foreach (var slot in shape) slotsById[slot.Id] = slot;

            var remaining = new HashSet<string>(new[] { "L0_0", "L0_1", "L1_0" });

            Assert.That(FreedomRuleCalculator.IsFree(slotsById["L0_0"], remaining), Is.False);
            Assert.That(FreedomRuleCalculator.IsFree(slotsById["L0_1"], remaining), Is.False);
            Assert.That(FreedomRuleCalculator.IsFree(slotsById["L1_0"], remaining), Is.True);
        }
    }
}
