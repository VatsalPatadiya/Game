namespace GameDomain.Progression
{
    // Player-facing relabeling of the internal 1-5 Difficulty scale used by
    // DifficultyProfile/TurtleShapeBuilder. Purely a display grouping - the
    // underlying generation knobs are untouched.
    public static class DifficultyTier
    {
        public static string For(int difficulty)
        {
            int d = difficulty < 1 ? 1 : (difficulty > 5 ? 5 : difficulty);
            if (d <= 2) return "EASY";
            if (d == 3) return "MEDIUM";
            return "HARD";
        }
    }
}
