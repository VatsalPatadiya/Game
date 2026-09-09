using System;

namespace GameDomain.Progression
{
    // Daily challenge (sub-project #5): a single deterministic board per calendar
    // day, seeded from the date so every player gets the same layout that day and
    // it changes at midnight. Separate from main progression.
    public static class DailyChallenge
    {
        // A fixed mid-size board for the daily, so it's a consistent challenge.
        public const int Difficulty = 3;

        // Deterministic per-day seed (same board for everyone on a given date).
        public static int SeedFor(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;

        // Stable per-day key used to record completion.
        public static string DateKey(DateTime date) => date.ToString("yyyy-MM-dd");
    }
}
