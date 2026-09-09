using NUnit.Framework;
using System;
using GameDomain.Progression;

namespace GameDomain.Tests.Progression
{
    public class DailyChallengeTests
    {
        [Test]
        public void SeedFor_IsDeterministicPerDay_AndDiffersAcrossDays()
        {
            var day = new DateTime(2026, 9, 9);
            Assert.That(DailyChallenge.SeedFor(day), Is.EqualTo(DailyChallenge.SeedFor(new DateTime(2026, 9, 9))));
            Assert.That(DailyChallenge.SeedFor(day), Is.Not.EqualTo(DailyChallenge.SeedFor(new DateTime(2026, 9, 10))));
        }

        [Test]
        public void DateKey_IsStableIsoFormat()
        {
            Assert.That(DailyChallenge.DateKey(new DateTime(2026, 9, 9)), Is.EqualTo("2026-09-09"));
        }

        [Test]
        public void GameProgress_MarkDailyDone_TracksPerDate()
        {
            var p = new GameProgress();
            string today = DailyChallenge.DateKey(new DateTime(2026, 9, 9));
            Assert.That(p.IsDailyDone(today), Is.False);

            p.MarkDailyDone(today);
            Assert.That(p.IsDailyDone(today), Is.True);

            // A different day is not yet done.
            string tomorrow = DailyChallenge.DateKey(new DateTime(2026, 9, 10));
            Assert.That(p.IsDailyDone(tomorrow), Is.False);
        }

        [Test]
        public void GameProgress_DailyDone_SurvivesJsonRoundTrip()
        {
            var p = new GameProgress();
            string today = DailyChallenge.DateKey(new DateTime(2026, 9, 9));
            p.MarkDailyDone(today);

            var loaded = UnityEngine.JsonUtility.FromJson<GameProgress>(UnityEngine.JsonUtility.ToJson(p));
            Assert.That(loaded.IsDailyDone(today), Is.True);
        }
    }
}
