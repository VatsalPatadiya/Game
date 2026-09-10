using NUnit.Framework;
using System.Collections.Generic;
using GameDomain.Generation;

namespace GameDomain.Tests.Generation
{
    public class DifficultyProfileTests
    {
        [Test]
        public void For_HarderLevels_HaveSmallerOpeningAndMoreConfusability()
        {
            var easy = DifficultyProfile.For(1, MatchMode.Pair);
            var hard = DifficultyProfile.For(5, MatchMode.Pair);

            Assert.That(hard.OpeningFraction, Is.LessThan(easy.OpeningFraction));
            Assert.That(hard.OpeningBranchingMin, Is.LessThan(easy.OpeningBranchingMin));
            Assert.That(hard.ConfusabilityLevel, Is.GreaterThan(easy.ConfusabilityLevel));
        }

        [Test]
        public void For_ClampsDifficultyAndSetsMode()
        {
            Assert.That(DifficultyProfile.For(0, MatchMode.Triple).Mode, Is.EqualTo(MatchMode.Triple));
            Assert.That(DifficultyProfile.For(99, MatchMode.Pair).OpeningFraction,
                        Is.EqualTo(DifficultyProfile.For(5, MatchMode.Pair).OpeningFraction));
        }

        [Test]
        public void Accepts_TrueWhenOpeningMeetsMinAndCurveRampsDown()
        {
            var p = DifficultyProfile.For(1, MatchMode.Pair); // OpeningBranchingMin = 6
            var good = new List<int> { 8, 7, 7, 6, 4, 3, 2, 1 };
            Assert.That(p.Accepts(good), Is.True);
        }

        [Test]
        public void Accepts_FalseWhenOpeningTooLow()
        {
            var p = DifficultyProfile.For(1, MatchMode.Pair);
            var flatLow = new List<int> { 2, 2, 2, 1, 1, 1, 1, 1 };
            Assert.That(p.Accepts(flatLow), Is.False);
        }

        [Test]
        public void Accepts_FalseWhenCurveRampsUp()
        {
            var p = DifficultyProfile.For(1, MatchMode.Pair);
            var rising = new List<int> { 6, 6, 6, 6, 8, 9, 10, 11 };
            Assert.That(p.Accepts(rising), Is.False);
        }
    }
}
