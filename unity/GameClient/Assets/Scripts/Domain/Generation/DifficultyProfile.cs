using System;
using System.Collections.Generic;

namespace GameDomain.Generation
{
    public enum MatchMode { Pair, Triple }

    // The single tuning surface for difficulty. Higher difficulty = shorter easy
    // opening (OpeningFraction), fewer guaranteed simultaneous matches
    // (OpeningBranchingMin), and more look-alike symbols (ConfusabilityLevel).
    public sealed class DifficultyProfile
    {
        public MatchMode Mode;
        public float OpeningFraction;    // fraction of the solve built with max exposure (easy window)
        public float OpeningBranchingMin; // required avg simultaneous matches over the opening window
        public float BranchingTolerance; // slack below OpeningBranchingMin still accepted
        public int ConfusabilityLevel;    // 0 = one symbol per cluster; k = up to k+1 per cluster

        public int GroupSize => Mode == MatchMode.Triple ? 3 : 2;

        public static DifficultyProfile For(int difficulty, MatchMode mode)
        {
            int d = difficulty < 1 ? 1 : (difficulty > 5 ? 5 : difficulty);
            // Starting presets (tuned later against real boards, per spec 5.1).
            // Triple mode uses a lower confusability at equal difficulty (3 look-alikes is already hard).
            float[] openingFraction     = { 0.80f, 0.70f, 0.55f, 0.40f, 0.30f };
            float[] openingBranchingMin = { 6f,    5f,    4f,    3f,    2f    };
            int[]   confusability       = { 0,     0,     1,     2,     3     };

            int i = d - 1;
            int confusion = confusability[i];
            if (mode == MatchMode.Triple) confusion = Math.Max(0, confusion - 1);

            return new DifficultyProfile
            {
                Mode = mode,
                OpeningFraction = openingFraction[i],
                OpeningBranchingMin = openingBranchingMin[i],
                BranchingTolerance = 1f,
                ConfusabilityLevel = confusion,
            };
        }

        // Opening window (first OpeningFraction of steps) must average at least
        // (OpeningBranchingMin - tolerance) simultaneous matches, and the tail must
        // not have MORE matches than the opening (the curve ramps down, not up).
        public bool Accepts(IReadOnlyList<int> branchingByStep)
        {
            if (branchingByStep == null || branchingByStep.Count == 0) return false;

            int n = branchingByStep.Count;
            int window = Math.Max(1, (int)Math.Ceiling(n * OpeningFraction));
            if (window > n) window = n;

            double openingSum = 0;
            for (int t = 0; t < window; t++) openingSum += branchingByStep[t];
            double openingAvg = openingSum / window;

            if (openingAvg < OpeningBranchingMin - BranchingTolerance) return false;

            int tailStart = Math.Max(window, n - window);
            double tailSum = 0; int tailCount = 0;
            for (int t = tailStart; t < n; t++) { tailSum += branchingByStep[t]; tailCount++; }
            double tailAvg = tailCount > 0 ? tailSum / tailCount : openingAvg;

            return tailAvg <= openingAvg + BranchingTolerance;
        }
    }
}
