using NUnit.Framework;
using GameDomain.Gameplay;

namespace GameDomain.Tests.Gameplay
{
    public class TileMatchRulesTests
    {
        [Test]
        public void AreCompatible_IdenticalSuitValue_ReturnsTrue()
        {
            Assert.That(TileMatchRules.AreCompatible("5", "5"), Is.True);
        }

        [Test]
        public void AreCompatible_DifferentSuitValues_ReturnsFalse()
        {
            Assert.That(TileMatchRules.AreCompatible("5", "6"), Is.False);
        }

        [Test]
        public void AreCompatible_TwoDifferentFlowerValues_ReturnsTrue()
        {
            // 34-37 are the Flower wildcard range - any two match regardless
            // of exact value.
            Assert.That(TileMatchRules.AreCompatible("34", "36"), Is.True);
        }

        [Test]
        public void AreCompatible_TwoDifferentSeasonValues_ReturnsTrue()
        {
            // 38-41 are the Season wildcard range.
            Assert.That(TileMatchRules.AreCompatible("38", "41"), Is.True);
        }

        [Test]
        public void AreCompatible_FlowerAndSeason_ReturnsFalse()
        {
            // Flowers and Seasons are separate wildcard groups - a Flower
            // never matches a Season.
            Assert.That(TileMatchRules.AreCompatible("34", "38"), Is.False);
        }

        [Test]
        public void AreCompatible_WildcardValueAndSuitValue_ReturnsFalse()
        {
            Assert.That(TileMatchRules.AreCompatible("34", "9"), Is.False);
        }

        [Test]
        public void AreCompatible_NonNumericPlaceholderValues_FallBackToExactEquality()
        {
            // Several existing tests use opaque non-numeric values ("a"/"b")
            // as pure identity tokens - these must keep behaving as plain
            // equality, never accidentally landing in a wildcard bucket.
            Assert.That(TileMatchRules.AreCompatible("a", "a"), Is.True);
            Assert.That(TileMatchRules.AreCompatible("a", "b"), Is.False);
        }

        [Test]
        public void BucketKey_FlowerValues_ReturnSameKey()
        {
            Assert.That(TileMatchRules.BucketKey("34"), Is.EqualTo(TileMatchRules.BucketKey("37")));
        }

        [Test]
        public void BucketKey_SuitValue_ReturnsValueItself()
        {
            Assert.That(TileMatchRules.BucketKey("12"), Is.EqualTo("12"));
        }
    }
}
