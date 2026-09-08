using System.Collections.Generic;
using System.Linq;

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
            new LevelData { LevelId = 5, Name = "Level 5", TileCount = 60, Difficulty = 4, ParAids = 1 },
        };

        public static LevelData Get(int levelId) => Levels.FirstOrDefault(l => l.LevelId == levelId);

        public static int NextLevelId(int levelId)
        {
            int idx = Levels.FindIndex(l => l.LevelId == levelId);
            return (idx >= 0 && idx + 1 < Levels.Count) ? Levels[idx + 1].LevelId : levelId;
        }
    }
}
