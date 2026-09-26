using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDomain.Progression
{
    [System.Serializable]
    public class LevelStarEntry
    {
        public int LevelId;
        public int Stars;
    }

    // Persistent player progress. Field-based (public fields, List) so Unity's
    // JsonUtility can round-trip it. Level 1 is unlocked by default.
    [System.Serializable]
    public class GameProgress
    {
        public int HighestUnlockedLevelId = 1;
        public List<LevelStarEntry> Stars = new List<LevelStarEntry>();

        public int GetStars(int levelId)
        {
            var e = Stars.FirstOrDefault(x => x.LevelId == levelId);
            return e != null ? e.Stars : 0;
        }

        public bool IsUnlocked(int levelId) => levelId <= HighestUnlockedLevelId;

        // Records a level result: keeps the best (max) stars for that level, and
        // unlocks the next level when the level was cleared (stars > 0).
        public void RecordResult(int levelId, int stars, int nextLevelId)
        {
            var e = Stars.FirstOrDefault(x => x.LevelId == levelId);
            if (e == null)
            {
                e = new LevelStarEntry { LevelId = levelId, Stars = stars };
                Stars.Add(e);
            }
            else
            {
                e.Stars = Math.Max(e.Stars, stars);
            }

            if (stars > 0 && nextLevelId > HighestUnlockedLevelId)
                HighestUnlockedLevelId = nextLevelId;
        }
    }
}
