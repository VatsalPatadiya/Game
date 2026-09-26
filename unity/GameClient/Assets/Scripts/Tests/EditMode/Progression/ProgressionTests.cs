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
        public void LevelCatalog_NextLevelId_AdvancesPastAuthoredLevels()
        {
            Assert.That(LevelCatalog.NextLevelId(1), Is.EqualTo(2));
            int lastAuthoredId = LevelCatalog.Levels[LevelCatalog.Levels.Count - 1].LevelId;
            Assert.That(LevelCatalog.NextLevelId(lastAuthoredId), Is.EqualTo(lastAuthoredId + 1));
            Assert.That(LevelCatalog.NextLevelId(lastAuthoredId + 1), Is.EqualTo(lastAuthoredId + 2));
        }

        [Test]
        public void LevelCatalog_Get_Level6_StartsProceduralRampAtDifficulty1()
        {
            Assert.That(LevelCatalog.Get(6)?.Difficulty, Is.EqualTo(1));
        }

        [Test]
        public void LevelCatalog_Get_RampsOneDifficultyStepEvery8Levels()
        {
            Assert.That(LevelCatalog.Get(13)?.Difficulty, Is.EqualTo(1));
            Assert.That(LevelCatalog.Get(14)?.Difficulty, Is.EqualTo(2));
            Assert.That(LevelCatalog.Get(22)?.Difficulty, Is.EqualTo(3));
            Assert.That(LevelCatalog.Get(30)?.Difficulty, Is.EqualTo(4));
            Assert.That(LevelCatalog.Get(38)?.Difficulty, Is.EqualTo(5));
        }

        [Test]
        public void LevelCatalog_Get_PlateausAtDifficulty5_ForVeryHighLevels()
        {
            Assert.That(LevelCatalog.Get(1000)?.Difficulty, Is.EqualTo(5));
        }

        [Test]
        public void LevelCatalog_Get_ProceduralLevel_HasPairModeAndParAidsAtLeastOne()
        {
            var level = LevelCatalog.Get(6);
            Assert.That(level.Mode, Is.EqualTo(GameDomain.Generation.MatchMode.Pair));
            Assert.That(level.ParAids, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void LevelData_DefaultsToPairMode()
        {
            Assert.That(new GameDomain.Progression.LevelData().Mode, Is.EqualTo(GameDomain.Generation.MatchMode.Pair));
        }

        [Test]
        public void DifficultyTier_Difficulty1And2_AreEasy()
        {
            Assert.That(DifficultyTier.For(1), Is.EqualTo("EASY"));
            Assert.That(DifficultyTier.For(2), Is.EqualTo("EASY"));
        }

        [Test]
        public void DifficultyTier_Difficulty3_IsMedium()
        {
            Assert.That(DifficultyTier.For(3), Is.EqualTo("MEDIUM"));
        }

        [Test]
        public void DifficultyTier_Difficulty4And5_AreHard()
        {
            Assert.That(DifficultyTier.For(4), Is.EqualTo("HARD"));
            Assert.That(DifficultyTier.For(5), Is.EqualTo("HARD"));
        }

        [Test]
        public void DifficultyTier_OutOfRangeDifficulty_Clamps()
        {
            Assert.That(DifficultyTier.For(0), Is.EqualTo("EASY"));
            Assert.That(DifficultyTier.For(99), Is.EqualTo("HARD"));
        }
    }
}
