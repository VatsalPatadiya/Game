using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using UnityEngine;

namespace GameClient.Presentation
{
    // Thin wrapper around Firebase Analytics - the only place in this project
    // that touches the Firebase SDK directly, so every call site elsewhere just
    // calls a plain static method with domain values (level id, stars, ...)
    // and never has to think about FirebaseApp's async init/DependencyStatus.
    // Every logging method is a safe no-op until Initialize()'s dependency
    // check has completed successfully (e.g. no google-services.json yet, no
    // Google Play Services on the device, still mid-init) - callers never
    // need to null-check or wrap calls in a ready check themselves.
    public static class AnalyticsService
    {
        private static bool _ready;

        public static void Initialize()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result == DependencyStatus.Available)
                {
                    _ready = true;
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                }
                else
                {
                    Debug.LogWarning("AnalyticsService: Firebase dependencies unavailable (" +
                        task.Result + "); analytics disabled for this session.");
                }
            });
        }

        public static void LevelStart(int levelId)
        {
            if (!_ready) return;
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelStart,
                FirebaseAnalytics.ParameterLevel, levelId);
        }

        // Firebase's own recommended pattern is one "level_end" event with a
        // success flag (fewer, more useful funnels in the Firebase console)
        // rather than two separately-named custom events.
        public static void LevelComplete(int levelId, int stars, int score)
        {
            if (!_ready) return;
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelEnd, new[]
            {
                new Parameter(FirebaseAnalytics.ParameterLevel, levelId),
                new Parameter(FirebaseAnalytics.ParameterSuccess, 1L),
                new Parameter(FirebaseAnalytics.ParameterScore, score),
                new Parameter("stars", stars),
            });
        }

        public static void LevelFailed(int levelId)
        {
            if (!_ready) return;
            FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventLevelEnd, new[]
            {
                new Parameter(FirebaseAnalytics.ParameterLevel, levelId),
                new Parameter(FirebaseAnalytics.ParameterSuccess, 0L),
            });
        }

        public static void TileMatched(int levelId, int comboCount)
        {
            if (!_ready) return;
            FirebaseAnalytics.LogEvent("tile_matched", new[]
            {
                new Parameter(FirebaseAnalytics.ParameterLevel, levelId),
                new Parameter("combo_count", comboCount),
            });
        }

        public static void ComboReached(int levelId, int comboCount)
        {
            if (!_ready) return;
            // Only worth a distinct event once a streak forms (2+) - every
            // match already logs tile_matched with its own combo_count.
            if (comboCount < 2) return;
            FirebaseAnalytics.LogEvent("combo_reached", new[]
            {
                new Parameter(FirebaseAnalytics.ParameterLevel, levelId),
                new Parameter("combo_count", comboCount),
            });
        }

        public static void HintUsed(int levelId, int hintsRemaining)
        {
            if (!_ready) return;
            FirebaseAnalytics.LogEvent("hint_used", new[]
            {
                new Parameter(FirebaseAnalytics.ParameterLevel, levelId),
                new Parameter("hints_remaining", hintsRemaining),
            });
        }

        public static void UndoUsed(int levelId, int undosRemaining)
        {
            if (!_ready) return;
            FirebaseAnalytics.LogEvent("undo_used", new[]
            {
                new Parameter(FirebaseAnalytics.ParameterLevel, levelId),
                new Parameter("undos_remaining", undosRemaining),
            });
        }

        public static void ShuffleUsed(int levelId, int shufflesRemaining)
        {
            if (!_ready) return;
            FirebaseAnalytics.LogEvent("shuffle_used", new[]
            {
                new Parameter(FirebaseAnalytics.ParameterLevel, levelId),
                new Parameter("shuffles_remaining", shufflesRemaining),
            });
        }

        public static void HiddenTileRevealed(int levelId)
        {
            if (!_ready) return;
            FirebaseAnalytics.LogEvent("hidden_tile_revealed",
                FirebaseAnalytics.ParameterLevel, levelId);
        }
    }
}
