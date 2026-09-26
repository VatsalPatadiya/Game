using System;

namespace GameDomain.Progression
{
    // Deterministic star rule for v1 (no timing dependency):
    //   0 stars if not won;
    //   1 star for a win;
    //   +1 if aids used <= the level's par;
    //   +1 for a flawless win (0 aids used).
    public static class StarRating
    {
        public static int Evaluate(LevelData level, int aidsUsed, bool won)
        {
            if (!won) return 0;
            int stars = 1;
            if (aidsUsed <= level.ParAids) stars++;
            if (aidsUsed == 0) stars++;
            return Math.Min(stars, 3);
        }
    }
}
