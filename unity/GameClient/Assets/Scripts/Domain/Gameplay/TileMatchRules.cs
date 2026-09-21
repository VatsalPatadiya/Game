namespace GameDomain.Gameplay
{
    // Shared match-compatibility rule, consulted by every site that decides
    // whether two tiles can clear together (MatchValidator, TrayManager,
    // HintFinder, TrayHintFinder) so there is one definition of "compatible"
    // instead of four independent equality checks.
    //
    // Value ranges are assigned by DataAssetGenerator's IconNames order and
    // are gameplay-load-bearing: 0-33 are the 34 exact-match suit/honor
    // tiles (dots/bamboo/characters/winds/dragons), 34-37 are the four
    // Flower designs, and 38-41 are the four Season designs. Standard
    // Mahjong Solitaire rule: any Flower matches any other Flower, and any
    // Season matches any other Season, regardless of which specific design -
    // unlike the suit/honor tiles, which only match an identical value.
    public static class TileMatchRules
    {
        private const int FlowerStart = 34, FlowerEnd = 37;
        private const int SeasonStart = 38, SeasonEnd = 41;

        public static bool AreCompatible(string valueA, string valueB) => BucketKey(valueA) == BucketKey(valueB);

        // The exact value for ordinary (exact-match) tiles, or a shared
        // category label for wildcard tiles - grouping/comparing by this key
        // instead of raw Value implements "any Flower matches any Flower"
        // without every call site needing its own wildcard-aware logic.
        // TryParse (not Parse): several existing tests use opaque non-numeric
        // placeholder values ("a", "b", "x") as pure identity tokens - those
        // fall through to plain exact-value equality unchanged, since only a
        // real numeric value can ever fall in a wildcard range.
        public static string BucketKey(string value)
        {
            if (int.TryParse(value, out int v))
            {
                if (v >= FlowerStart && v <= FlowerEnd) return "FLOWER";
                if (v >= SeasonStart && v <= SeasonEnd) return "SEASON";
            }
            return value;
        }
    }
}
