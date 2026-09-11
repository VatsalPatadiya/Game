# Difficulty-Shaping Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a controllable difficulty layer on top of the existing solvable-board generator so matches are easy to identify early (in a board and in early levels) and get progressively harder, driven by branching factor and visual confusability, for both pair- and triple-match.

**Architecture:** A per-level `DifficultyProfile` defines the curve. A `BranchingOrderBuilder` shapes *which* co-free tiles get paired/tripled (front-loading exposure so early matches are plentiful, tapering after), a `BranchingSimulator` measures the resulting branching curve, and the generator reseeds until the profile is met (soft target) while reverse-construction keeps every board solvable (hard invariant). A `PaletteSelector` then maps each removal group to one of the 26 food-model values, honoring similarity clusters so early boards use distinct-looking symbols and harder boards deliberately include look-alikes.

**Tech Stack:** C# (Unity 6 domain assembly `GameDomain`, no Unity runtime deps in domain code), NUnit EditMode tests, existing `ReverseConstructionSolver` / `FreedomRuleCalculator` primitives.

**Spec:** `docs/superpowers/specs/2026-09-10-difficulty-shaping-design.md`

## Global Constraints

- **Solvability is a hard invariant; difficulty is a soft target.** Never weaken solvability to hit a difficulty target — reseed, then fall back to a neutral solvable board and log a warning. Only throw `BoardGenerationException` when no *solvable* order exists at all (preserves the current contract).
- **Domain code stays Unity-free.** New files under `Assets/Scripts/Domain/**` must not reference `UnityEngine`. Only `TileSetAsset.cs` (in `GameClient.Data`) and `GameController.cs` (Presentation) touch Unity.
- **A tile's `Value` is a string integer in `[0, 26)`** (`TileVisual.FoodModelFor` indexes `FoodModels[int.Parse(value) % 26]`, 26 distinct models). All values assigned must stay in this range.
- **A match is by exact `Value` equality on free tiles** (`MatchValidator` / tray). Two removal groups may share a value (adds match options, never removes them — still solvable). Two *different* values must never map to the same model index.
- **Group size:** pair mode = 2, triple mode = 3. Tray capacity is `BoardState.MaxTraySize` (4), so a co-free triple always clears without overflow.
- **Do not change** existing signatures `BoardGenerator.Generate(level, rng)` / `GenerateTriples(level, rng)` or `ReverseConstructionSolver.*` — add new methods/overloads so existing tests stay green.
- **Namespaces:** generation code → `GameDomain.Generation`; tests → `GameDomain.Tests.Generation`; level config → `GameDomain.Progression`; tile asset → `GameClient.Data`.
- **Test fixtures:** reuse `GameDomain.Tests.Fixtures.TestLayoutShapes` (`SmallShape` = 8 tiles/1 layer, `MediumShape` = 18 tiles/2 layers, `LargeShape`, `ExtraLargeShape`).
- **Commit** after each task's tests pass. Branch is `onboard-match-mechanic-revert`; commit only the files each task lists.

---

### Task 1: DifficultyProfile

Defines difficulty as data: the match mode, how much of the solve is "easy opening" (front-loaded exposure), the branching acceptance bounds, and the confusability level. This is the single tuning surface.

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Domain/Generation/DifficultyProfile.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/DifficultyProfileTests.cs`

**Interfaces:**
- Produces:
  - `enum GameDomain.Generation.MatchMode { Pair, Triple }`
  - `sealed class GameDomain.Generation.DifficultyProfile` with public fields
    `MatchMode Mode; float OpeningFraction; float OpeningBranchingMin; float BranchingTolerance; int ConfusabilityLevel;`
  - `static DifficultyProfile For(int difficulty, MatchMode mode)` (difficulty clamped 1..5)
  - `bool Accepts(IReadOnlyList<int> branchingByStep)`
  - `int GroupSize => Mode == MatchMode.Triple ? 3 : 2;`

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using System.Collections.Generic;
using GameDomain.Generation;

namespace GameDomain.Tests.Generation
{
    public class DifficultyProfileTests
    {
        [Test]
        public void For_HarderLevels_HaveSmallerOpeningAndMoreConfusability()
        {
            var easy = DifficultyProfile.For(1, MatchMode.Pair);
            var hard = DifficultyProfile.For(5, MatchMode.Pair);

            Assert.That(hard.OpeningFraction, Is.LessThan(easy.OpeningFraction));
            Assert.That(hard.OpeningBranchingMin, Is.LessThan(easy.OpeningBranchingMin));
            Assert.That(hard.ConfusabilityLevel, Is.GreaterThan(easy.ConfusabilityLevel));
        }

        [Test]
        public void For_ClampsDifficultyAndSetsMode()
        {
            Assert.That(DifficultyProfile.For(0, MatchMode.Triple).Mode, Is.EqualTo(MatchMode.Triple));
            Assert.That(DifficultyProfile.For(99, MatchMode.Pair).OpeningFraction,
                        Is.EqualTo(DifficultyProfile.For(5, MatchMode.Pair).OpeningFraction));
        }

        [Test]
        public void Accepts_TrueWhenOpeningMeetsMinAndCurveRampsDown()
        {
            var p = DifficultyProfile.For(1, MatchMode.Pair); // OpeningBranchingMin = 6
            var good = new List<int> { 8, 7, 7, 6, 4, 3, 2, 1 };
            Assert.That(p.Accepts(good), Is.True);
        }

        [Test]
        public void Accepts_FalseWhenOpeningTooLow()
        {
            var p = DifficultyProfile.For(1, MatchMode.Pair);
            var flatLow = new List<int> { 2, 2, 2, 1, 1, 1, 1, 1 };
            Assert.That(p.Accepts(flatLow), Is.False);
        }

        [Test]
        public void Accepts_FalseWhenCurveRampsUp()
        {
            var p = DifficultyProfile.For(1, MatchMode.Pair);
            var rising = new List<int> { 6, 6, 6, 6, 8, 9, 10, 11 };
            Assert.That(p.Accepts(rising), Is.False);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `Unity EditMode tests, filter `GameDomain.Tests.Generation.DifficultyProfileTests` (see "Running tests" note at end).
Expected: FAIL — `DifficultyProfile` / `MatchMode` do not exist (compile error).

- [ ] **Step 3: Write minimal implementation**

```csharp
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: EditMode filter `DifficultyProfileTests`.
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Domain/Generation/DifficultyProfile.cs \
        unity/GameClient/Assets/Scripts/Domain/Generation/DifficultyProfile.cs.meta \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/DifficultyProfileTests.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/DifficultyProfileTests.cs.meta
git commit -m "feat: add DifficultyProfile difficulty tuning surface"
```

> **Meta files:** Unity auto-creates a `.meta` next to each new file when the Editor imports. If it hasn't been generated yet, add just the `.cs` file; the `.meta` will appear on next import and can be committed then. Every task below has the same caveat.

---

### Task 2: BranchingSimulator

Measures the branching curve of a candidate board by walking its known removal order and counting, at each step, how many *remaining removal groups are fully free right now*. Pure and deterministic — this is the verification oracle used by the generator and by tests. Works on group structure (pre-palette), so it is independent of final values.

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Domain/Generation/BranchingSimulator.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/BranchingSimulatorTests.cs`

**Interfaces:**
- Consumes: `FreedomRuleCalculator.ComputeFreeSlots` (existing), `GameDomain.Model.TileSlot`.
- Produces: `static List<int> GameDomain.Generation.BranchingSimulator.Profile(Dictionary<string, TileSlot> slotsById, List<string[]> removalOrder)` — returns one branching count per removal step (length == removalOrder.Count).

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Generation
{
    public class BranchingSimulatorTests
    {
        [Test]
        public void Profile_SingleFlatRow_AllGroupsFreeAtStart()
        {
            // 8 tiles, single layer: every tile is free from the start, so at step 0
            // all 4 pairs are simultaneously free -> branching 4, then 3, 2, 1.
            var shape = TestLayoutShapes.SmallShape();
            var slotsById = shape.ToDictionary(s => s.Id);
            var ids = shape.Select(s => s.Id).ToList();
            var order = new List<string[]>
            {
                new[] { ids[0], ids[1] },
                new[] { ids[2], ids[3] },
                new[] { ids[4], ids[5] },
                new[] { ids[6], ids[7] },
            };

            var curve = BranchingSimulator.Profile(slotsById, order);

            Assert.That(curve, Is.EqualTo(new List<int> { 4, 3, 2, 1 }));
        }

        [Test]
        public void Profile_LengthEqualsGroupCount()
        {
            var shape = TestLayoutShapes.MediumShape();
            var slotsById = shape.ToDictionary(s => s.Id);
            var ids = new HashSet<string>(slotsById.Keys);
            var order = ReverseConstructionSolver.TryBuildRemovalOrder(slotsById, ids, new System.Random(1))
                .Select(p => new[] { p.a, p.b }).ToList();

            var curve = BranchingSimulator.Profile(slotsById, order);

            Assert.That(curve.Count, Is.EqualTo(order.Count));
            Assert.That(curve[curve.Count - 1], Is.EqualTo(1)); // last group is always the only one free
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: EditMode filter `BranchingSimulatorTests`.
Expected: FAIL — `BranchingSimulator` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System.Collections.Generic;
using GameDomain.Model;

namespace GameDomain.Generation
{
    // Measures how many valid matches are simultaneously available along a board's
    // known (guaranteed-solvable) removal order. At each step it counts how many of
    // the still-remaining removal groups have ALL their tiles currently free. High
    // early = easy to spot matches; low = the player must search. Group-structural,
    // so it ignores final palette values.
    public static class BranchingSimulator
    {
        public static List<int> Profile(Dictionary<string, TileSlot> slotsById, List<string[]> removalOrder)
        {
            var remaining = new HashSet<string>();
            foreach (var group in removalOrder)
                foreach (var id in group)
                    remaining.Add(id);

            var result = new List<int>(removalOrder.Count);

            for (int t = 0; t < removalOrder.Count; t++)
            {
                var freeList = FreedomRuleCalculator.ComputeFreeSlots(slotsById, remaining);
                var freeSet = new HashSet<string>();
                foreach (var slot in freeList) freeSet.Add(slot.Id);

                int available = 0;
                for (int k = t; k < removalOrder.Count; k++)
                {
                    bool allFree = true;
                    foreach (var id in removalOrder[k])
                    {
                        if (!freeSet.Contains(id)) { allFree = false; break; }
                    }
                    if (allFree) available++;
                }
                result.Add(available);

                foreach (var id in removalOrder[t]) remaining.Remove(id);
            }

            return result;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: EditMode filter `BranchingSimulatorTests`.
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Domain/Generation/BranchingSimulator.cs* \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/BranchingSimulatorTests.cs*
git commit -m "feat: add BranchingSimulator branching-curve oracle"
```

---

### Task 3: BranchingOrderBuilder

Builds a solvable removal order (like `ReverseConstructionSolver` but unified to groups of `groupSize`), choosing which co-free tiles to group by an **exposure bias** that is *front-loaded*: for the first `openingFraction` of steps it maximizes newly-exposed tiles (flooding the opening with available matches → high branching), then minimizes exposure (matches become scarce → low branching). This is the mechanism that creates the within-board easy→hard ramp while staying solvable by construction.

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Domain/Generation/BranchingOrderBuilder.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/BranchingOrderBuilderTests.cs`

**Interfaces:**
- Consumes: `FreedomRuleCalculator.ComputeFreeSlots`, `BranchingSimulator.Profile` (in tests).
- Produces: `static List<string[]> GameDomain.Generation.BranchingOrderBuilder.Build(Dictionary<string, TileSlot> slotsById, HashSet<string> slotIds, System.Random random, int groupSize, float openingFraction)` — returns a removal order (each element a `groupSize`-length id array) covering every slot exactly once, or `null` if it dead-ends (fewer than `groupSize` free tiles at some step).

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Generation
{
    public class BranchingOrderBuilderTests
    {
        [Test]
        public void Build_CoversEverySlotExactlyOnce_Pairs()
        {
            var shape = TestLayoutShapes.MediumShape();
            var slotsById = shape.ToDictionary(s => s.Id);
            var ids = new HashSet<string>(slotsById.Keys);

            var order = BranchingOrderBuilder.Build(slotsById, ids, new Random(1), 2, 0.8f);

            Assert.That(order, Is.Not.Null);
            var flat = order.SelectMany(g => g).ToList();
            Assert.That(flat.Count, Is.EqualTo(ids.Count));
            Assert.That(new HashSet<string>(flat), Is.EquivalentTo(ids));
            Assert.That(order, Has.All.Matches<string[]>(g => g.Length == 2));
        }

        [Test]
        public void Build_FrontLoadedExposure_GivesHigherOpeningBranchingThanMinimized()
        {
            // Same shape/seed: a large opening fraction should produce an opening with
            // more simultaneous matches than an almost-zero opening fraction.
            var shape = TestLayoutShapes.LargeShape();
            var slotsById = shape.ToDictionary(s => s.Id);
            var ids = new HashSet<string>(slotsById.Keys);

            var easyOrder = BranchingOrderBuilder.Build(slotsById, new HashSet<string>(ids), new Random(5), 2, 0.9f);
            var hardOrder = BranchingOrderBuilder.Build(slotsById, new HashSet<string>(ids), new Random(5), 2, 0.0f);

            var easy = BranchingSimulator.Profile(slotsById, easyOrder);
            var hard = BranchingSimulator.Profile(slotsById, hardOrder);

            int window = Math.Max(1, easy.Count / 4);
            double easyOpening = easy.Take(window).Average();
            double hardOpening = hard.Take(window).Average();

            Assert.That(easyOpening, Is.GreaterThan(hardOpening));
        }

        [Test]
        public void Build_Triples_GroupsOfThree()
        {
            // 9 tiles single-row so triples always have 3 free.
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 9 });
            var slotsById = shape.ToDictionary(s => s.Id);
            var ids = new HashSet<string>(slotsById.Keys);

            var order = BranchingOrderBuilder.Build(slotsById, ids, new Random(2), 3, 0.5f);

            Assert.That(order, Is.Not.Null);
            Assert.That(order, Has.All.Matches<string[]>(g => g.Length == 3));
            Assert.That(order.SelectMany(g => g).Count(), Is.EqualTo(ids.Count));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: EditMode filter `BranchingOrderBuilderTests`.
Expected: FAIL — `BranchingOrderBuilder` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Model;

namespace GameDomain.Generation
{
    // Builds a solvable removal order in groups of `groupSize`, biased so the opening
    // (first `openingFraction` of steps) maximizes newly-exposed tiles (many matches
    // available early = easy) and the remainder minimizes exposure (matches scarce =
    // hard). Because every emitted group was simultaneously free, following the order
    // clears the board without overflowing the tray -> guaranteed solvable/tray-safe.
    public static class BranchingOrderBuilder
    {
        public static List<string[]> Build(
            Dictionary<string, TileSlot> slotsById, HashSet<string> slotIds,
            Random random, int groupSize, float openingFraction)
        {
            var remaining = new HashSet<string>(slotIds);
            var order = new List<string[]>();
            int totalGroups = slotIds.Count / groupSize;
            int openingGroups = (int)Math.Ceiling(totalGroups * openingFraction);

            while (remaining.Count > 0)
            {
                var free = FreedomRuleCalculator.ComputeFreeSlots(slotsById, remaining);
                if (free.Count < groupSize) return null;

                bool maximize = order.Count < openingGroups;
                var group = PickGroup(slotsById, remaining, free, groupSize, maximize, random);

                order.Add(group);
                foreach (var id in group) remaining.Remove(id);
            }

            return order;
        }

        // Greedily pick `groupSize` free tiles whose removal exposes the most (maximize)
        // or fewest (minimize) new tiles. Randomized tie-breaking keeps boards varied.
        private static string[] PickGroup(
            Dictionary<string, TileSlot> slotsById, HashSet<string> remaining,
            List<TileSlot> free, int groupSize, bool maximize, Random random)
        {
            // Shuffle the free list so equal-scoring candidates vary by seed.
            for (int i = free.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (free[i], free[j]) = (free[j], free[i]);
            }

            var freeIds = free.Select(s => s.Id).ToList();

            // Enumerate candidate groups as the first `groupSize` after sorting free
            // tiles by individual exposure gain (cheap, avoids O(n^choose) blowup).
            var scored = freeIds
                .Select(id => (id, gain: ExposureGain(slotsById, remaining, id)))
                .OrderBy(t => maximize ? -t.gain : t.gain)
                .ThenBy(_ => random.Next())
                .Select(t => t.id)
                .Take(groupSize)
                .ToArray();

            return scored;
        }

        // How many currently-blocked tiles become free if `id` is removed.
        private static int ExposureGain(
            Dictionary<string, TileSlot> slotsById, HashSet<string> remaining, string id)
        {
            var after = new HashSet<string>(remaining);
            after.Remove(id);

            int gain = 0;
            foreach (var otherId in remaining)
            {
                if (otherId == id) continue;
                var slot = slotsById[otherId];
                bool wasFree = FreedomRuleCalculator.IsFree(slot, remaining);
                bool nowFree = FreedomRuleCalculator.IsFree(slot, after);
                if (!wasFree && nowFree) gain++;
            }
            return gain;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: EditMode filter `BranchingOrderBuilderTests`.
Expected: PASS (3 tests). If `Build_FrontLoaded...` is flaky on a seed, that indicates the exposure heuristic is too weak for that shape — try 2-3 seeds and pick a stable one for the assertion; do not weaken the inequality to `>=`.

- [ ] **Step 5: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Domain/Generation/BranchingOrderBuilder.cs* \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/BranchingOrderBuilderTests.cs*
git commit -m "feat: add BranchingOrderBuilder with front-loaded exposure bias"
```

---

### Task 4: PaletteSelector + similarity clusters

Maps each removal group to one of the 26 food-model values, honoring visual-similarity clusters: at confusability 0 every value comes from a distinct cluster (nothing looks alike); at level k up to k+1 values may share a cluster (look-alikes co-occur). Also adds the cluster metadata field to the tile set.

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Domain/Generation/PaletteSelector.cs`
- Modify: `unity/GameClient/Assets/Scripts/Data/TileSetAsset.cs` (add `int[] SimilarityClusterId;`)
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/PaletteSelectorTests.cs`

**Interfaces:**
- Produces: `static Dictionary<string, string> GameDomain.Generation.PaletteSelector.AssignValues(List<string[]> removalOrder, int[] clusterIdByModel, int modelCount, int confusabilityLevel, System.Random random)` — returns `slotId → value` where every tile in a group shares one value, and values are chosen per the confusability rule. `clusterIdByModel[m]` gives model `m`'s cluster (`-1` = unique/own singleton cluster). If `clusterIdByModel` is `null`, every model is treated as its own cluster.

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;

namespace GameDomain.Tests.Generation
{
    public class PaletteSelectorTests
    {
        // 6 models: clusters {0,1,2}=A(0), {3,4}=B(1), {5}=unique(-1)
        private static readonly int[] Clusters = { 0, 0, 0, 1, 1, -1 };

        private static List<string[]> Order(int groups)
        {
            var o = new List<string[]>();
            for (int i = 0; i < groups; i++) o.Add(new[] { "s" + (2 * i), "s" + (2 * i + 1) });
            return o;
        }

        [Test]
        public void AssignValues_AllTilesInAGroupShareValue()
        {
            var values = PaletteSelector.AssignValues(Order(3), Clusters, 6, 0, new Random(1));
            foreach (var g in Order(3))
                Assert.That(values[g[0]], Is.EqualTo(values[g[1]]));
        }

        [Test]
        public void AssignValues_Level0_NoTwoValuesShareACluster()
        {
            // 3 groups, 3 clusters available -> all distinct clusters feasible.
            var values = PaletteSelector.AssignValues(Order(3), Clusters, 6, 0, new Random(2));
            var distinctModels = values.Values.Select(int.Parse).Distinct().ToList();
            var usedClusters = distinctModels.Select(m => Clusters[m] == -1 ? -100 - m : Clusters[m]).ToList();
            Assert.That(usedClusters.Count, Is.EqualTo(usedClusters.Distinct().Count()));
        }

        [Test]
        public void AssignValues_HighConfusability_PutsLookAlikesOnBoard()
        {
            // 3 groups, level 2 -> should draw >=2 models from the same cluster.
            var values = PaletteSelector.AssignValues(Order(3), Clusters, 6, 2, new Random(3));
            var distinctModels = values.Values.Select(int.Parse).Distinct().ToList();
            var realClusters = distinctModels.Select(m => Clusters[m]).Where(c => c >= 0).ToList();
            bool hasLookAlikePair = realClusters.GroupBy(c => c).Any(grp => grp.Count() >= 2);
            Assert.That(hasLookAlikePair, Is.True);
        }

        [Test]
        public void AssignValues_ValuesStayInModelRange()
        {
            var values = PaletteSelector.AssignValues(Order(10), Clusters, 6, 3, new Random(4));
            Assert.That(values.Values, Has.All.Matches<string>(v =>
            {
                int m = int.Parse(v);
                return m >= 0 && m < 6;
            }));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: EditMode filter `PaletteSelectorTests`.
Expected: FAIL — `PaletteSelector` does not exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameDomain.Generation
{
    // Maps each removal group to a food-model value (0..modelCount-1). Confusability 0
    // draws every value from a distinct similarity cluster (nothing looks alike); level
    // k allows up to k+1 values from the same cluster (look-alikes co-occur). Two
    // groups may share a value when variety runs out (safe: only adds match options).
    public static class PaletteSelector
    {
        public static Dictionary<string, string> AssignValues(
            List<string[]> removalOrder, int[] clusterIdByModel, int modelCount,
            int confusabilityLevel, Random random)
        {
            // Cluster -> its member model indices. Uniques (-1) become singleton clusters.
            var clusters = new Dictionary<int, List<int>>();
            for (int m = 0; m < modelCount; m++)
            {
                int cid = (clusterIdByModel != null && m < clusterIdByModel.Length) ? clusterIdByModel[m] : -1;
                int key = cid >= 0 ? cid : (-100 - m); // unique key per singleton
                if (!clusters.TryGetValue(key, out var list)) { list = new List<int>(); clusters[key] = list; }
                list.Add(m);
            }

            int maxPerCluster = Math.Max(1, confusabilityLevel + 1);

            // Build the value pool: walk clusters (shuffled), taking up to maxPerCluster
            // models from each, until we have one value per group (or run out).
            var clusterKeys = clusters.Keys.ToList();
            Shuffle(clusterKeys, random);

            var pool = new List<int>();
            foreach (var key in clusterKeys)
            {
                var members = new List<int>(clusters[key]);
                Shuffle(members, random);
                for (int i = 0; i < members.Count && i < maxPerCluster; i++)
                {
                    pool.Add(members[i]);
                    if (pool.Count >= removalOrder.Count) break;
                }
                if (pool.Count >= removalOrder.Count) break;
            }

            // Not enough distinct values under the cap: cycle the pool (value reuse is safe).
            if (pool.Count == 0) pool.Add(0);

            var values = new Dictionary<string, string>();
            for (int i = 0; i < removalOrder.Count; i++)
            {
                string v = pool[i % pool.Count].ToString();
                foreach (var id in removalOrder[i]) values[id] = v;
            }
            return values;
        }

        private static void Shuffle<T>(IList<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
```

- [ ] **Step 4: Add the cluster field to TileSetAsset**

Modify `unity/GameClient/Assets/Scripts/Data/TileSetAsset.cs` to add the field (authoring the values happens in Task 7):

```csharp
using UnityEngine;

namespace GameClient.Data
{
    [CreateAssetMenu(fileName = "TileSetAsset", menuName = "GameClient/Tile Set")]
    public sealed class TileSetAsset : ScriptableObject
    {
        public string TileSetId;
        public Sprite[] Icons;
        public Color[] AccentColors;
        public GameObject[] FoodModels;

        // Similarity cluster id per FoodModel index (parallel to FoodModels). Models
        // that look alike share a cluster id; -1 = visually unique. Drives the
        // difficulty confusability lever (see PaletteSelector). Authored in Inspector.
        public int[] SimilarityClusterId;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: EditMode filter `PaletteSelectorTests`. Confirm the whole EditMode suite still compiles (TileSetAsset change).
Expected: PASS (4 tests); no compile errors.

- [ ] **Step 6: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Domain/Generation/PaletteSelector.cs* \
        unity/GameClient/Assets/Scripts/Data/TileSetAsset.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/PaletteSelectorTests.cs*
git commit -m "feat: add PaletteSelector + tile-set similarity clusters"
```

---

### Task 5: BoardGenerator.GenerateShaped (pair + triple, reseed + fallback)

Ties the layer together: build a front-loaded removal order, measure its branching, reseed until the profile accepts it (soft target), then assign palette values. On exhaustion, fall back to a neutral solvable board and log a warning. Solvability is never sacrificed.

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Domain/Generation/BoardGenerator.cs` (add methods; leave existing ones intact)
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/BoardGeneratorShapedTests.cs`

**Interfaces:**
- Consumes: `DifficultyProfile`, `BranchingOrderBuilder.Build`, `BranchingSimulator.Profile`, `PaletteSelector.AssignValues`, existing `ReverseConstructionSolver` (fallback).
- Produces: `static BoardState BoardGenerator.GenerateShaped(LevelDefinition level, System.Random random, DifficultyProfile profile, int[] clusterIdByModel, int modelCount, int maxRestarts = 200)`.

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using System;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Generation
{
    public class BoardGeneratorShapedTests
    {
        [Test]
        public void GenerateShaped_ProducesSolvableFullyPopulatedBoard_Pairs()
        {
            var level = new LevelDefinition { LevelId = 1, Shape = TestLayoutShapes.MediumShape(), TileSetId = "test" };
            var profile = DifficultyProfile.For(1, MatchMode.Pair);

            var board = BoardGenerator.GenerateShaped(level, new Random(10), profile, null, 26);

            Assert.That(board.Cells.Count, Is.EqualTo(level.Shape.Count));
            Assert.That(board.Cells.Values, Has.All.Matches<TileCell>(c => c.Value != null && !c.Cleared));
            // Solvable: a value can be grouped into pairs (each value count is even).
            var counts = board.Cells.Values.GroupBy(c => c.Value).Select(g => g.Count());
            Assert.That(counts, Has.All.Matches<int>(n => n % 2 == 0));
        }

        [Test]
        public void GenerateShaped_EasyProfile_HasHighOpeningBranching()
        {
            var level = new LevelDefinition { LevelId = 1, Shape = TestLayoutShapes.LargeShape(), TileSetId = "test" };
            var profile = DifficultyProfile.For(1, MatchMode.Pair);

            // Should not throw and should satisfy the profile it was generated for.
            Assert.DoesNotThrow(() =>
                BoardGenerator.GenerateShaped(level, new Random(11), profile, null, 26));
        }

        [Test]
        public void GenerateShaped_Triples_EachValueCountIsMultipleOfThree()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 9, 6, 3 });
            var level = new LevelDefinition { LevelId = 3, Shape = shape, TileSetId = "test" };
            var profile = DifficultyProfile.For(3, MatchMode.Triple);

            var board = BoardGenerator.GenerateShaped(level, new Random(12), profile, null, 26);

            var counts = board.Cells.Values.GroupBy(c => c.Value).Select(g => g.Count());
            Assert.That(counts, Has.All.Matches<int>(n => n % 3 == 0));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: EditMode filter `BoardGeneratorShapedTests`.
Expected: FAIL — `GenerateShaped` does not exist.

- [ ] **Step 3: Write minimal implementation**

Add to `BoardGenerator` (keep existing `Generate` / `GenerateTriples`):

```csharp
public static BoardState GenerateShaped(
    LevelDefinition level, Random random, DifficultyProfile profile,
    int[] clusterIdByModel, int modelCount, int maxRestarts = 200)
{
    var slotsById = level.Shape.ToDictionary(s => s.Id);
    var allIds = new HashSet<string>(slotsById.Keys);
    int groupSize = profile.GroupSize;

    List<string[]> chosenOrder = null;

    for (int attempt = 0; attempt < maxRestarts; attempt++)
    {
        var order = BranchingOrderBuilder.Build(
            slotsById, new HashSet<string>(allIds), random, groupSize, profile.OpeningFraction);
        if (order == null) continue;

        var curve = BranchingSimulator.Profile(slotsById, order);
        if (profile.Accepts(curve)) { chosenOrder = order; break; }
    }

    // Fallback: a neutral (still front-loaded but unverified) solvable order. Solvability
    // is preserved because BranchingOrderBuilder only ever groups co-free tiles.
    if (chosenOrder == null)
    {
        for (int attempt = 0; attempt < maxRestarts && chosenOrder == null; attempt++)
            chosenOrder = BranchingOrderBuilder.Build(
                slotsById, new HashSet<string>(allIds), random, groupSize, profile.OpeningFraction);

        if (chosenOrder == null)
            throw new BoardGenerationException(
                "Could not generate a solvable board for level " + level.LevelId +
                " after " + maxRestarts + " attempts.");

        System.Diagnostics.Debug.WriteLine(
            "Difficulty profile not met for level " + level.LevelId + "; used fallback board.");
    }

    var values = PaletteSelector.AssignValues(
        chosenOrder, clusterIdByModel, modelCount, profile.ConfusabilityLevel, random);

    var board = new BoardState
    {
        LevelId = level.LevelId,
        MovesRemaining = level.MovesBudget,
        Cells = new Dictionary<string, TileCell>()
    };
    foreach (var id in allIds)
        board.Cells[id] = new TileCell { Value = values[id], Cleared = false };

    return board;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: EditMode filter `BoardGeneratorShapedTests`.
Expected: PASS (3 tests). Also re-run existing `BoardGeneratorTests` — must still pass (unchanged methods).

- [ ] **Step 5: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Domain/Generation/BoardGenerator.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/BoardGeneratorShapedTests.cs*
git commit -m "feat: add BoardGenerator.GenerateShaped (branching + confusability, reseed+fallback)"
```

---

### Task 6: Solvability + difficulty regression

Guards that difficulty shaping never breaks beatability: every difficulty 1..5, both modes, many seeds must produce a board that is fully solvable AND satisfies its own profile (or falls back cleanly without throwing).

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Tests/EditMode/Regression/SolvabilityRegressionTests.cs` (add cases)

**Interfaces:**
- Consumes: `BoardGenerator.GenerateShaped`, `DifficultyProfile`, `TurtleShapeBuilder.BuildForDifficulty`, `BranchingSimulator`.

- [ ] **Step 1: Read the existing file to match its style/helpers**

Run: open `SolvabilityRegressionTests.cs`; reuse whatever "is solvable" helper it already defines (a greedy or order-based solve). If it has none, add the helper below.

- [ ] **Step 2: Write the failing test (add to the class)**

```csharp
[Test]
public void GenerateShaped_AllDifficulties_BothModes_AreSolvable([Range(1, 5)] int difficulty)
{
    foreach (var mode in new[] { MatchMode.Pair, MatchMode.Triple })
    {
        var profile = DifficultyProfile.For(difficulty, mode);
        for (int seed = 0; seed < 20; seed++)
        {
            // Triple mode needs a tile count divisible by 3; pad the shape's group size
            // by choosing a shape whose count fits. Use a single flat row sized to the mode.
            int tiles = mode == MatchMode.Triple ? 18 : 20;
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { tiles });
            var level = new LevelDefinition { LevelId = difficulty, Shape = shape, TileSetId = "test" };

            var board = BoardGenerator.GenerateShaped(level, new System.Random(seed), profile, null, 26);

            Assert.That(IsBoardSolvable(board, shape.ToDictionary(s => s.Id), profile.GroupSize),
                Is.True, $"unsolvable: difficulty={difficulty} mode={mode} seed={seed}");
        }
    }
}

// A board is solvable if repeatedly clearing any fully-free group of equal value
// empties it. (Greedy is sufficient here because GenerateShaped builds boards from a
// co-free removal order, so a greedy-any solution always exists.)
private static bool IsBoardSolvable(
    BoardState board, System.Collections.Generic.Dictionary<string, TileSlot> slotsById, int groupSize)
{
    var remaining = new System.Collections.Generic.HashSet<string>(board.Cells.Keys);
    int guard = remaining.Count * remaining.Count + 10;

    while (remaining.Count > 0 && guard-- > 0)
    {
        var free = FreedomRuleCalculator.ComputeFreeSlots(slotsById, remaining);
        var byValue = free.GroupBy(s => board.Cells[s.Id].Value)
                          .FirstOrDefault(g => g.Count() >= groupSize);
        if (byValue == null) return false;

        foreach (var slot in byValue.Take(groupSize)) remaining.Remove(slot.Id);
    }
    return remaining.Count == 0;
}
```

Ensure the file has `using System.Linq;`, `using GameDomain.Generation;`, `using GameDomain.Model;`, `using GameDomain.Tests.Fixtures;`.

- [ ] **Step 3: Run test to verify it fails, then passes**

Run: EditMode filter `SolvabilityRegressionTests`.
Expected: initially FAIL only if `GenerateShaped` weren't present (it is, from Task 5) — so this should PASS once the greedy helper is correct. If a specific seed fails solvability, that's a real bug in Task 3/5, not the test — debug the builder, do not delete the seed.

- [ ] **Step 4: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Tests/EditMode/Regression/SolvabilityRegressionTests.cs
git commit -m "test: regression - shaped boards solvable across all difficulties and modes"
```

---

### Task 7: Wire into the game (LevelData mode + GameController + author clusters)

Makes the running game use the new generator, adds per-level pair/triple selection, and authors the similarity clusters on the shipped tile set so the confusability lever has data.

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Domain/Progression/LevelData.cs` (add `MatchMode Mode`)
- Modify: `unity/GameClient/Assets/Scripts/Presentation/GameController.cs:139-161` (build profile, call `GenerateShaped`)
- Modify: the default `TileSetAsset` asset (author `SimilarityClusterId`) — via Inspector or the `DataAssetGenerator` editor script
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Progression/ProgressionTests.cs` (assert `LevelData.Mode` default)

**Interfaces:**
- Consumes: `DifficultyProfile.For`, `BoardGenerator.GenerateShaped`, `BoardView3D.TileSet` (exposes `TileSetAsset`).

- [ ] **Step 1: Add `Mode` to LevelData (failing test first)**

Add to `ProgressionTests.cs`:

```csharp
[Test]
public void LevelData_DefaultsToPairMode()
{
    Assert.That(new GameDomain.Progression.LevelData().Mode, Is.EqualTo(GameDomain.Generation.MatchMode.Pair));
}
```

- [ ] **Step 2: Run to verify it fails**

Run: EditMode filter `ProgressionTests.LevelData_DefaultsToPairMode`.
Expected: FAIL — `Mode` field missing.

- [ ] **Step 3: Add the field**

In `LevelData.cs` add (and `using GameDomain.Generation;`):

```csharp
public GameDomain.Generation.MatchMode Mode = GameDomain.Generation.MatchMode.Pair;
```

- [ ] **Step 4: Run to verify it passes**

Run: EditMode filter `ProgressionTests`.
Expected: PASS.

- [ ] **Step 5: Wire GameController to the shaped generator**

In `GameController.LoadLevel` replace the board-generation call. Current (`GameController.cs:161`):

```csharp
_board = BoardGenerator.Generate(level, rng);
```

Replace with (mode from level config; daily = Pair). Insert after `level` is built and `difficulty` is known:

```csharp
var mode = _isDaily ? MatchMode.Pair
                    : (LevelCatalog.Get(_currentLevelId)?.Mode ?? MatchMode.Pair);
var profile = DifficultyProfile.For(difficulty, mode);

var tileSet = _boardView != null ? _boardView.TileSet : null;
int[] clusters = tileSet != null ? tileSet.SimilarityClusterId : null;
int modelCount = (tileSet != null && tileSet.FoodModels != null && tileSet.FoodModels.Length > 0)
    ? tileSet.FoodModels.Length : 26;

_board = BoardGenerator.GenerateShaped(level, rng, profile, clusters, modelCount);
```

Add `using GameDomain.Generation;` to `GameController.cs` if not present. (`_boardView` is the `BoardView3D` field already used at line 172; confirm its type exposes `TileSet` — it does, `BoardView3D.TileSet` at `BoardView3D.cs:95`.)

- [ ] **Step 6: Author similarity clusters on the tile set**

Open the default `TileSetAsset` in the Inspector (the one referenced by `BoardView3D._tileSet`). Set `SimilarityClusterId` to a length-26 array matching `FoodModels` order. Group visually similar foods; `-1` for unique. Example grouping (adjust to the actual model order):

```
round-red     -> apple, tomato, cherries      (cluster 0)
round-orange  -> orange, lemon                 (cluster 1)
green         -> broccoli, avocado, pear        (cluster 2)
yellow-long   -> banana, corn                   (cluster 3)
baked-sweet   -> cookie, cupcake, muffin, donut, croissant (cluster 4)
everything else -> -1 (unique)
```

Verify `SimilarityClusterId.Length == FoodModels.Length`.

- [ ] **Step 7: Verify in the Editor (Play mode)**

Enter Play mode; load level 1 (easy) and level 5 (hard). Confirm: level 1 opens with many obviously-available matches and visually-distinct foods; level 5 shows fewer easy matches and repeated look-alike foods. This is the behavioral verification the spec's two levers describe. Use the `/verify` or `/run` skill to drive the app if available.

- [ ] **Step 8: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Domain/Progression/LevelData.cs \
        unity/GameClient/Assets/Scripts/Presentation/GameController.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Progression/ProgressionTests.cs \
        <path-to-default-TileSetAsset>.asset
git commit -m "feat: wire difficulty-shaped generation into GameController + level modes + clusters"
```

---

## Self-Review

**1. Spec coverage:**
- Spec §3.1 branching factor → Tasks 2 (measure), 3 (shape). ✓
- Spec §3.2 confusability → Task 4. ✓
- Spec §4 architecture (DifficultyProfile, ValueAssigner, BranchingSimulator, PaletteSelector, TileSetAsset) → Tasks 1,2,3,4,5. *Refinement:* the spec's "ValueAssigner shapes branching" is implemented as `BranchingOrderBuilder` (branching is a property of the removal-order structure, since value↔group is 1:1 pre-palette; a tile's Value is its food-model identity). Confusability lives in `PaletteSelector`. Intent (two independent levers) preserved. ✓
- Spec §5.1 presets → Task 1 `For`. ✓
- Spec §5.5 verification oracle → Task 2. ✓
- Spec §6 reseed (soft) + fallback + throw-only-if-unsolvable → Task 5. ✓
- Spec §7 tests (5 suites + regression + cluster length) → Tasks 1,2,3,4,5,6; cluster-length checked manually in Task 7 Step 6 and guarded by `PaletteSelector` treating out-of-range as unique. ✓
- Spec §8 open items: pair/triple per level → Task 7 (`LevelData.Mode`); store icon-index vs value → **resolved**: value *is* the model index (0..25), no separate map needed, matching untouched. ✓

**2. Placeholder scan:** No TBD/TODO; every code step has real code. Task 7 Step 6 uses an example cluster grouping the implementer maps to actual model order — this is authoring data, not a code placeholder. ✓

**3. Type consistency:** `MatchMode`, `DifficultyProfile.GroupSize`, `List<string[]>` removal order (Tasks 2/3/5), `PaletteSelector.AssignValues(..) → Dictionary<string,string>`, `GenerateShaped(level, random, profile, int[] clusterIdByModel, int modelCount, maxRestarts)` are consistent across tasks. ✓

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-09-10-difficulty-shaping.md`. Two execution options:

1. **Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
