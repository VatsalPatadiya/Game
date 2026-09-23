using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;

namespace GameDomain.Progression
{
    // A single level's definition. Data-driven so levels are trivial to add
    // (a code catalog now; authored assets in sub-project #7).
    [System.Serializable]
    public class LevelData
    {
        public int LevelId;
        public string Name;
        public int TileCount = 48;
        public int Difficulty = 1;   // 1-5, drives board size/variety later
        public int ParAids = 2;      // using <= this many aids still earns the 2nd star
        public GameDomain.Generation.MatchMode Mode = GameDomain.Generation.MatchMode.Pair;
    }

    // Ordered list of levels. A small starter set now; expandable to authored
    // assets without touching consumers (they read via Get/Count/Next).
    public static class LevelCatalog
    {
        public static readonly List<LevelData> Levels = new List<LevelData>
        {
            new LevelData { LevelId = 1, Name = "Level 1", TileCount = 24, Difficulty = 1, ParAids = 3 },
            new LevelData { LevelId = 2, Name = "Level 2", TileCount = 36, Difficulty = 2, ParAids = 3 },
            new LevelData { LevelId = 3, Name = "Level 3", TileCount = 48, Difficulty = 2, ParAids = 2 },
            new LevelData { LevelId = 4, Name = "Level 4", TileCount = 48, Difficulty = 3, ParAids = 2 },
            new LevelData { LevelId = 5, Name = "Level 5", TileCount = 144, Difficulty = 5, ParAids = 1 },
        };

        // Levels beyond the last authored one are generated procedurally so
        // progression never dead-ends - Difficulty ramps 1->5 one step every
        // RampLevelsPerDifficultyStep levels, then plateaus at 5 forever.
        // ParAids steps down as difficulty rises (floored at 1 so a 2nd star
        // always stays reachable). Mode stays Pair - Triple levels are a
        // deliberately curated variant, not something to auto-assign here.
        private const int RampLevelsPerDifficultyStep = 8;

        public static LevelData Get(int levelId)
        {
            var authored = Levels.FirstOrDefault(l => l.LevelId == levelId);
            if (authored != null) return authored;

            int lastAuthoredId = Levels[Levels.Count - 1].LevelId;
            return levelId > lastAuthoredId ? GenerateProcedural(levelId, lastAuthoredId) : null;
        }

        private static LevelData GenerateProcedural(int levelId, int lastAuthoredId)
        {
            int stepsIn = (levelId - lastAuthoredId - 1) / RampLevelsPerDifficultyStep;
            int difficulty = 1 + stepsIn;
            if (difficulty > 5) difficulty = 5;

            int parAids = 4 - difficulty;
            if (parAids < 1) parAids = 1;
            if (parAids > 3) parAids = 3;

            return new LevelData
            {
                LevelId = levelId,
                Name = "Level " + levelId,
                Difficulty = difficulty,
                ParAids = parAids,
                Mode = GameDomain.Generation.MatchMode.Pair,
            };
        }

        public static int NextLevelId(int levelId) => levelId + 1;
    }
}
