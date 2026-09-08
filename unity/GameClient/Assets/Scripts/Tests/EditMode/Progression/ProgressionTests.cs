using NUnit.Framework;
using GameDomain.Progression;
using UnityEngine;

namespace GameDomain.Tests.Progression
{
    public class ProgressionTests
    {
        private static LevelData Level(int par) => new LevelData { LevelId = 1, ParAids = par };

        [Test]
        public void StarRating_NotWon_ZeroStars()
        {
            Assert.That(StarRating.Evaluate(Level(2), aidsUsed: 0, won: false), Is.EqualTo(0));
        }

        [Test]
        public void StarRating_WonWithManyAids_OneStar()
        {
            Assert.That(StarRating.Evaluate(Level(2), aidsUsed: 5, won: true), Is.EqualTo(1));
        }

        [Test]
        public void StarRating_WonWithinPar_TwoStars()
        {
            Assert.That(StarRating.Evaluate(Level(2), aidsUsed: 2, won: true), Is.EqualTo(2));
        }

        [Test]
        public void StarRating_FlawlessWin_ThreeStars()
        {
            Assert.That(StarRating.Evaluate(Level(2), aidsUsed: 0, won: true), Is.EqualTo(3));
        }

        [Test]
        public void GameProgress_RecordResult_KeepsMaxStars_AndUnlocksNext()
        {
            var p = new GameProgress();
            p.RecordResult(levelId: 1, stars: 2, nextLevelId: 2);
            Assert.That(p.GetStars(1), Is.EqualTo(2));
            Assert.That(p.HighestUnlockedLevelId, Is.EqualTo(2));
            Assert.That(p.IsUnlocked(2), Is.True);
            Assert.That(p.IsUnlocked(3), Is.False);

            // A worse replay must not lower the recorded stars, nor re-lock.
            p.RecordResult(levelId: 1, stars: 1, nextLevelId: 2);
            Assert.That(p.GetStars(1), Is.EqualTo(2));
            Assert.That(p.HighestUnlockedLevelId, Is.EqualTo(2));
        }

        [Test]
        public void GameProgress_NotWon_DoesNotUnlock()
        {
            var p = new GameProgress();
            p.RecordResult(levelId: 1, stars: 0, nextLevelId: 2);
            Assert.That(p.HighestUnlockedLevelId, Is.EqualTo(1));
        }

        [Test]
        public void GameProgress_JsonRoundTrip_PreservesState()
        {
            var p = new GameProgress();
            p.RecordResult(1, 3, 2);
            p.RecordResult(2, 1, 3);

            string json = JsonUtility.ToJson(p);
            var loaded = JsonUtility.FromJson<GameProgress>(json);

            Assert.That(loaded.HighestUnlockedLevelId, Is.EqualTo(3));
            Assert.That(loaded.GetStars(1), Is.EqualTo(3));
            Assert.That(loaded.GetStars(2), Is.EqualTo(1));
        }

        [Test]
        public void LevelCatalog_NextLevelId_AdvancesThenClampsAtLast()
        {
            Assert.That(LevelCatalog.NextLevelId(1), Is.EqualTo(2));
            int lastId = LevelCatalog.Levels[LevelCatalog.Levels.Count - 1].LevelId;
            Assert.That(LevelCatalog.NextLevelId(lastId), Is.EqualTo(lastId));
        }
    }
}
