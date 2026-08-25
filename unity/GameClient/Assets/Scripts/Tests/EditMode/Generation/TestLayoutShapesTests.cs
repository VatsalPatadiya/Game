using NUnit.Framework;
using System.Collections.Generic;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Generation
{
    public class TestLayoutShapesTests
    {
        [Test]
        public void SmallShape_HasEvenSlotCountAndValidReferences()
        {
            AssertShapeIsStructurallyValid(TestLayoutShapes.SmallShape());
        }

        [Test]
        public void MediumShape_HasEvenSlotCountAndValidReferences()
        {
            AssertShapeIsStructurallyValid(TestLayoutShapes.MediumShape());
        }

        [Test]
        public void LargeShape_HasEvenSlotCountAndValidReferences()
        {
            AssertShapeIsStructurallyValid(TestLayoutShapes.LargeShape());
        }

        private static void AssertShapeIsStructurallyValid(List<TileSlot> shape)
        {
            Assert.That(shape.Count % 2, Is.EqualTo(0), "Shape must have an even number of slots to pair completely.");

            var ids = new HashSet<string>();
            foreach (var slot in shape)
            {
                Assert.That(ids.Add(slot.Id), "Duplicate slot id: " + slot.Id);
            }

            foreach (var slot in shape)
            {
                foreach (var coveredById in slot.CoveredByIds)
                {
                    Assert.That(ids.Contains(coveredById), "Slot " + slot.Id + " has unknown CoveredByIds entry " + coveredById);
                }

                if (slot.LeftNeighborId != null)
                    Assert.That(ids.Contains(slot.LeftNeighborId), "Slot " + slot.Id + " has unknown LeftNeighborId " + slot.LeftNeighborId);

                if (slot.RightNeighborId != null)
                    Assert.That(ids.Contains(slot.RightNeighborId), "Slot " + slot.Id + " has unknown RightNeighborId " + slot.RightNeighborId);
            }
        }
    }
}
