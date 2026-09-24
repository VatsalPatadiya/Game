# Pyramid Reserve Stacks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Once a procedural level's difficulty maxes out (~level 38, where the
pyramid itself has plateaued at 54 tiles forever), further tile-count/difficulty
growth comes from a fixed row of 4 top + 4 bottom reserve stacks instead of the
pyramid growing — so levels never dead-end, and the board's on-screen footprint
never changes again.

**Architecture:** Two new pure, engine-free classes in `GameDomain.Generation`
(`StackColumnBuilder`, `BoardShapeBuilder`) build and compose stack-column
`TileSlot`s using the same abstract covering-graph model (`CoveredByIds` + null
neighbors) that `FreedomRuleCalculator`/`BoardGenerator`/every solver component
already consume generically — none of that code changes. `TileSlot` gains a
`SlotRegion` tag and `StackColumnId` so the presentation layer can tell stack
tiles apart from pyramid tiles. `LevelCatalog` gains a `StackDepthPerColumn`
progression knob. `GameController.PrepareLevel` composes the shape when that
knob is nonzero. `BoardView3D` gets two surgical fixes (its per-layer render
offset and camera-fit bounding box must not scale with a stack's true depth) plus
a small procedurally-built count-badge component, matching this project's
existing pattern of building all HUD elements in code rather than hand-authored
prefabs (see `GameSceneBuilder3D.cs`).

**Tech Stack:** Unity 6000.5.9f1, C#, NUnit EditMode tests (`Domain.Tests`
assembly), URP, TextMeshPro.

**Spec:** `docs/superpowers/specs/2026-09-24-pyramid-reserve-stacks-design.md`

## Global Constraints

- The pyramid itself (`TurtleShapeBuilder`) is never modified — all growth is
  additive, via composed stack columns (spec §1, Non-Goals §2).
- Levels 1 through the difficulty-5 plateau level (38, per the existing
  `RampLevelsPerDifficultyStep = 8` ramp in `LevelData.cs`) must produce
  byte-for-byte identical shapes to today — `StackDepthPerColumn == 0` for all of
  them (spec §2 Non-Goals, §5.4).
- No changes to `BoardGenerator`, `ReverseConstructionSolver`,
  `BranchingOrderBuilder`, `FreedomRuleCalculator`, `DifficultyProfile`,
  `HiddenTileSelector`, or `TileReveal` (spec §2, §4) — they already operate
  generically on the abstract slot graph.
- Stack column *count* never grows — always 4 top + 4 bottom (8 total). Only
  column *depth* grows, uniformly across all 8 columns (spec §2 Non-Goals, §5.4).
- Stack tiles must never receive the pyramid's `LayerRenderOffset`/camera
  `maxLayer` treatment in `BoardView3D` — that's the exact bug this feature
  exists to avoid re-introducing (spec §3.3, the critical finding).
- Every domain-layer task follows TDD: write the failing test, verify it fails
  for the right reason, then implement.
- Do not commit until told to. Each task below still ends with a "Step: Commit"
  for tracking, but hold actual `git commit` execution until the user says go —
  this project's established working convention.

## Review Focus

- **Undo restoring a stack tile.** `GameController.AnimateUndo` → `BoardView3D
  .RestoreTile` calls the same `PlaceTileView` every fresh tile uses. No test
  anywhere exercises restoring a *stack-region* slot specifically — Task 7's
  verification explicitly checks this, not just fresh `Build()`.
- **A hidden stack tile becomes revealable only once it's actually the top of its
  pile.** `HiddenTileSelector.Apply` marks ~35% of *all* cells hidden at
  difficulty ≥ 4 (including buried stack tiles that won't be free for a long
  time) — nothing currently proves `TileReveal.TryReveal` still correctly gates
  on freedom for a stack topology specifically. Task 5 adds this as an explicit
  integration test.
- **Generation time at extreme stack depth.** `BranchingOrderBuilder`'s
  exposure-gain scan is O(remaining) per candidate tile, called every step.
  Nothing currently proves a 100+ tile stack-heavy board generates in bounded
  time, not just that it eventually succeeds. Task 5 adds a wall-clock assertion.
- **Deal-in batching at deep stack levels.** `BoardView3D.Build` groups tiles
  into deal-in batches by unique `(Layer, Y)` (`BoardView3D.cs:143-155`) — a
  60-deep stack column contributes ~60 distinct batches by itself (one per
  layer), which no existing test or the domain-only tests in this plan can
  catch, since it's a presentation-layer animation-pacing concern. Task 9's
  on-device check explicitly watches deal-in duration/smoothness at a deep
  synthetic level, not just static camera framing.
- **The camera-fit fix must be verified on a live board, not just algebraically.**
  `FitCameraToBoard` needs a real `Camera`, so no NUnit test can cover it. Task 9
  compares the logged `orthographicSize` between a stack-free level and a
  200+-tile stack-heavy level to confirm they stay close — the actual point of
  the fix in spec §3.3/§5.3.

---

## File Structure

**Create:**
- `unity/GameClient/Assets/Scripts/Domain/Generation/StackColumnBuilder.cs` — builds one vertical LIFO stack column's `TileSlot`s.
- `unity/GameClient/Assets/Scripts/Domain/Generation/BoardShapeBuilder.cs` — positions a row of stack columns relative to a pyramid, and composes pyramid + stacks into one shape.
- `unity/GameClient/Assets/Scripts/Presentation/Board3D/StackPileBadge3D.cs` — procedurally-built `+N` count label per stack pile.
- `unity/GameClient/Assets/Scripts/Tests/EditMode/Model/TileSlotTests.cs`
- `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/StackColumnBuilderTests.cs`
- `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/BoardShapeBuilderTests.cs`
- `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/StackIntegrationTests.cs` — deep-stack solvability, timing, and hidden-tile-reveal-through-a-stack regression guards.

**Modify:**
- `unity/GameClient/Assets/Scripts/Domain/Model/TileSlot.cs` — add `SlotRegion` enum, `Region`, `StackColumnId`.
- `unity/GameClient/Assets/Scripts/Domain/Progression/LevelData.cs` — add `StackDepthPerColumn` field, extend `GenerateProcedural`.
- `unity/GameClient/Assets/Scripts/Tests/EditMode/Progression/ProgressionTests.cs` — extend.
- `unity/GameClient/Assets/Scripts/Presentation/GameController.cs` — `PrepareLevel`.
- `unity/GameClient/Assets/Scripts/Presentation/Board3D/BoardView3D.cs` — `PlaceTileView`, `FitCameraToBoard`, `Clear`, `RefreshFreeStates` (new `RefreshStackBadges` call).

---

### Task 1: `TileSlot` region tagging

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Domain/Model/TileSlot.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Model/TileSlotTests.cs` (new)

**Interfaces:**
- Produces: `SlotRegion` enum (`Pyramid`, `TopStack`, `BottomStack`) in
  `GameDomain.Model`; `TileSlot.Region` (default `SlotRegion.Pyramid`);
  `TileSlot.StackColumnId` (string, default `null`). Every later task reads
  these exact names.

- [ ] **Step 1: Write the failing test**

Create `unity/GameClient/Assets/Scripts/Tests/EditMode/Model/TileSlotTests.cs`:

```csharp
using NUnit.Framework;
using GameDomain.Model;

namespace GameDomain.Tests.Model
{
    public class TileSlotTests
    {
        [Test]
        public void NewTileSlot_DefaultsToPyramidRegion_AndNullStackColumnId()
        {
            var slot = new TileSlot();
            Assert.That(slot.Region, Is.EqualTo(SlotRegion.Pyramid));
            Assert.That(slot.StackColumnId, Is.Null);
        }
    }
}
```

- [ ] **Step 2: Verify the test fails to compile**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Check `get_console_logs` for a `CS0117`/`CS0246` error on `SlotRegion`/`Region`/`StackColumnId` not existing. (Headless fallback: `unity test /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --editor-version 6000.5.9f1 --mode EditMode`.)

- [ ] **Step 3: Implement**

`TileSlot.cs` — full new content:

```csharp
using System.Collections.Generic;

namespace GameDomain.Model
{
    // Which physical region of the board a slot belongs to. Pyramid tiles are
    // the classic turtle shape (TurtleShapeBuilder); TopStack/BottomStack tiles
    // belong to a reserve-stack column (StackColumnBuilder). Only the
    // presentation layer (BoardView3D) reads this - domain generation/solving
    // code is fully region-agnostic (see the design spec §3.2/§4).
    public enum SlotRegion { Pyramid, TopStack, BottomStack }

    public sealed class TileSlot
    {
        public string Id;
        public int X;
        public int Y;
        public int Layer;
        public List<string> CoveredByIds = new List<string>();
        public string LeftNeighborId;
        public string RightNeighborId;
        public SlotRegion Region = SlotRegion.Pyramid;
        // Which stack column this slot belongs to (e.g. "TS0", "BS2"), null for
        // pyramid tiles. Lets the presentation layer group a pile's tiles
        // together (badge placement/count) without re-parsing slot IDs.
        public string StackColumnId;
    }
}
```

- [ ] **Step 4: Run tests, verify pass**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: all EditMode tests pass, total count up by 1.

- [ ] **Step 5: Commit** (hold until the user says go)

```bash
git add unity/GameClient/Assets/Scripts/Domain/Model/TileSlot.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Model/TileSlotTests.cs
git commit -m "Add SlotRegion/StackColumnId to TileSlot for reserve-stack support"
```

---

### Task 2: `StackColumnBuilder`

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Domain/Generation/StackColumnBuilder.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/StackColumnBuilderTests.cs` (new)

**Interfaces:**
- Consumes: `TileSlot`, `SlotRegion` (Task 1); `FreedomRuleCalculator.IsFree`
  (existing, unchanged).
- Produces: `StackColumnBuilder.Build(int x, int y, int depth, SlotRegion
  region, string columnId) -> List<TileSlot>` — a depth-N vertical LIFO
  column, `Layer 0` = bottom (built/covered first), `Layer depth-1` = top
  (immediately free). Task 3 and `GameController` call this exact signature.

- [ ] **Step 1: Write the failing tests**

Create `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/StackColumnBuilderTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Tests.Generation
{
    public class StackColumnBuilderTests
    {
        [Test]
        public void Build_ProducesExactlyDepthSlots()
        {
            var column = StackColumnBuilder.Build(x: 5, y: 10, depth: 6, SlotRegion.TopStack, columnId: "TS0");
            Assert.That(column.Count, Is.EqualTo(6));
        }

        [Test]
        public void Build_EachSlot_CoveredOnlyByTheOneDirectlyAbove()
        {
            var column = StackColumnBuilder.Build(x: 5, y: 10, depth: 4, SlotRegion.TopStack, columnId: "TS0");
            var byLayer = column.ToDictionary(s => s.Layer);

            for (int l = 0; l < 3; l++)
                Assert.That(byLayer[l].CoveredByIds, Is.EquivalentTo(new[] { byLayer[l + 1].Id }),
                    $"layer {l} should be covered only by the tile directly above it");

            Assert.That(byLayer[3].CoveredByIds, Is.Empty, "the topmost tile must have no covering tile");
        }

        [Test]
        public void Build_EveryTile_HasNullLeftAndRightNeighbors()
        {
            var column = StackColumnBuilder.Build(x: 5, y: 10, depth: 5, SlotRegion.TopStack, columnId: "TS0");
            Assert.That(column, Has.All.Matches<TileSlot>(s => s.LeftNeighborId == null && s.RightNeighborId == null));
        }

        [Test]
        public void Build_SetsRegionAndStackColumnIdOnEverySlot()
        {
            var column = StackColumnBuilder.Build(x: 5, y: 10, depth: 3, SlotRegion.BottomStack, columnId: "BS2");
            Assert.That(column, Has.All.Matches<TileSlot>(s => s.Region == SlotRegion.BottomStack && s.StackColumnId == "BS2"));
        }

        [Test]
        public void Build_EverySlot_SharesTheSameXAndY()
        {
            var column = StackColumnBuilder.Build(x: 7, y: -12, depth: 4, SlotRegion.TopStack, columnId: "TS0");
            Assert.That(column, Has.All.Matches<TileSlot>(s => s.X == 7 && s.Y == -12));
        }

        [Test]
        public void Build_IdsAreColumnIdUnderscoreLayer()
        {
            var column = StackColumnBuilder.Build(x: 1, y: 1, depth: 3, SlotRegion.TopStack, columnId: "TS0");
            Assert.That(column.Select(s => s.Id), Is.EquivalentTo(new[] { "TS0_0", "TS0_1", "TS0_2" }));
        }

        [Test]
        public void Build_OnlyTopTileIsFree_UsingFreedomRuleCalculator()
        {
            var column = StackColumnBuilder.Build(x: 1, y: 1, depth: 4, SlotRegion.TopStack, columnId: "TS0");
            var remaining = new HashSet<string>(column.Select(s => s.Id));

            for (int i = 0; i < column.Count - 1; i++)
                Assert.That(FreedomRuleCalculator.IsFree(column[i], remaining), Is.False,
                    $"layer {column[i].Layer} should not be free while covered");

            var top = column.Single(s => s.Layer == column.Count - 1);
            Assert.That(FreedomRuleCalculator.IsFree(top, remaining), Is.True);
        }
    }
}
```

- [ ] **Step 2: Verify tests fail to compile**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Check `get_console_logs` for `CS0246: The type or namespace name 'StackColumnBuilder' could not be found`.

- [ ] **Step 3: Implement**

Create `unity/GameClient/Assets/Scripts/Domain/Generation/StackColumnBuilder.cs`:

```csharp
using System.Collections.Generic;
using GameDomain.Model;

namespace GameDomain.Generation
{
    // Builds one vertical LIFO reserve-stack column: a fixed number of tiles
    // sharing the same (x, y), differing only by Layer, each covered ONLY by
    // the tile directly above it in the same column. Left/right neighbors are
    // always null so FreedomRuleCalculator's side-open rule is trivially
    // satisfied and the column's freedom reduces to "am I currently the top
    // tile" - exactly a physical draw pile. See the design spec §3.2.
    public static class StackColumnBuilder
    {
        public static List<TileSlot> Build(int x, int y, int depth, SlotRegion region, string columnId)
        {
            var slots = new List<TileSlot>();
            for (int l = 0; l < depth; l++)
            {
                slots.Add(new TileSlot
                {
                    Id = columnId + "_" + l,
                    X = x,
                    Y = y,
                    Layer = l,
                    Region = region,
                    StackColumnId = columnId,
                    CoveredByIds = new List<string>()
                });
            }
            for (int l = 0; l < depth - 1; l++)
                slots[l].CoveredByIds.Add(slots[l + 1].Id);
            return slots;
        }
    }
}
```

- [ ] **Step 4: Run tests, verify pass**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: all pass, total count up by 6 from Task 1's baseline.

- [ ] **Step 5: Commit** (hold until the user says go)

```bash
git add unity/GameClient/Assets/Scripts/Domain/Generation/StackColumnBuilder.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/StackColumnBuilderTests.cs
git commit -m "Add StackColumnBuilder for vertical LIFO reserve-stack columns"
```

---

### Task 3: `BoardShapeBuilder` (positioning + composition)

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Domain/Generation/BoardShapeBuilder.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/BoardShapeBuilderTests.cs` (new)

**Interfaces:**
- Consumes: `StackColumnBuilder.Build` (Task 2); `TurtleShapeBuilder
  .BuildForDifficulty` (existing, unchanged).
- Produces:
  - `BoardShapeBuilder.DefaultColumnsPerRow` (const int, `4`) and
    `BoardShapeBuilder.DefaultRowGap` (const int, `6`, half-tile units) — Task 6
    (`GameController`) uses these exact names.
  - `BoardShapeBuilder.BuildStackRow(List<TileSlot> pyramid, int columnCount,
    int depth, SlotRegion region, int yGap) -> List<List<TileSlot>>`.
  - `BoardShapeBuilder.Compose(List<TileSlot> pyramid, List<List<TileSlot>>
    topColumns, List<List<TileSlot>> bottomColumns) -> List<TileSlot>`.

- [ ] **Step 1: Write the failing tests**

Create `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/BoardShapeBuilderTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Tests.Generation
{
    public class BoardShapeBuilderTests
    {
        [Test]
        public void BuildStackRow_ZeroDepth_ReturnsNoColumns()
        {
            var pyramid = TurtleShapeBuilder.BuildForDifficulty(5);
            var columns = BoardShapeBuilder.BuildStackRow(pyramid, columnCount: 4, depth: 0, SlotRegion.TopStack, yGap: 6);
            Assert.That(columns, Is.Empty);
        }

        [Test]
        public void BuildStackRow_ProducesRequestedColumnCountAndDepth()
        {
            var pyramid = TurtleShapeBuilder.BuildForDifficulty(5);
            var columns = BoardShapeBuilder.BuildStackRow(pyramid, columnCount: 4, depth: 6, SlotRegion.TopStack, yGap: 6);
            Assert.That(columns.Count, Is.EqualTo(4));
            Assert.That(columns, Has.All.Matches<List<TileSlot>>(c => c.Count == 6));
        }

        [Test]
        public void BuildStackRow_TopStack_SitsAbovePyramidBoundingBox()
        {
            var pyramid = TurtleShapeBuilder.BuildForDifficulty(5);
            int pyramidMaxY = pyramid.Max(s => s.Y);
            var columns = BoardShapeBuilder.BuildStackRow(pyramid, columnCount: 4, depth: 3, SlotRegion.TopStack, yGap: 6);
            Assert.That(columns.SelectMany(c => c), Has.All.Matches<TileSlot>(s => s.Y > pyramidMaxY));
        }

        [Test]
        public void BuildStackRow_BottomStack_SitsBelowPyramidBoundingBox()
        {
            var pyramid = TurtleShapeBuilder.BuildForDifficulty(5);
            int pyramidMinY = pyramid.Min(s => s.Y);
            var columns = BoardShapeBuilder.BuildStackRow(pyramid, columnCount: 4, depth: 3, SlotRegion.BottomStack, yGap: 6);
            Assert.That(columns.SelectMany(c => c), Has.All.Matches<TileSlot>(s => s.Y < pyramidMinY));
        }

        [Test]
        public void BuildStackRow_ColumnsAreDistinctXPositions()
        {
            var pyramid = TurtleShapeBuilder.BuildForDifficulty(5);
            var columns = BoardShapeBuilder.BuildStackRow(pyramid, columnCount: 4, depth: 2, SlotRegion.TopStack, yGap: 6);
            var xs = columns.Select(c => c[0].X).Distinct().ToList();
            Assert.That(xs.Count, Is.EqualTo(4), "each column must sit at its own X position");
        }

        [Test]
        public void Compose_ZeroDepthStacks_ReturnsPyramidUnchanged()
        {
            var pyramid = TurtleShapeBuilder.BuildForDifficulty(5);
            var composed = BoardShapeBuilder.Compose(pyramid, new List<List<TileSlot>>(), new List<List<TileSlot>>());
            Assert.That(composed.Select(s => s.Id), Is.EquivalentTo(pyramid.Select(s => s.Id)));
        }

        [Test]
        public void Compose_WithStacks_MergesAllSlotsWithNoIdCollisions()
        {
            var pyramid = TurtleShapeBuilder.BuildForDifficulty(5);
            var top = BoardShapeBuilder.BuildStackRow(pyramid, 4, 3, SlotRegion.TopStack, 6);
            var bottom = BoardShapeBuilder.BuildStackRow(pyramid, 4, 3, SlotRegion.BottomStack, 6);
            var composed = BoardShapeBuilder.Compose(pyramid, top, bottom);

            int expectedTotal = pyramid.Count + top.Sum(c => c.Count) + bottom.Sum(c => c.Count);
            Assert.That(composed.Count, Is.EqualTo(expectedTotal), "4x3 + 4x3 stack tiles is a multiple of 4, so parity trim never triggers here");
            Assert.That(composed.Select(s => s.Id).Distinct().Count(), Is.EqualTo(composed.Count));
        }

        [Test]
        public void Compose_OddTotal_TrimsOneTileFromDeepestColumn_NeverFromPyramid()
        {
            var pyramid = TurtleShapeBuilder.BuildForDifficulty(1); // 12, even
            // Hand-built mismatched depths - BuildStackRow never produces this
            // (all columns in a row always share one depth, spec §5.2), but
            // Compose must still defend against it.
            var top = new List<List<TileSlot>>
            {
                StackColumnBuilder.Build(x: 1, y: 100, depth: 3, SlotRegion.TopStack, columnId: "TS0"),
                StackColumnBuilder.Build(x: 3, y: 100, depth: 2, SlotRegion.TopStack, columnId: "TS1"),
            };
            var composed = BoardShapeBuilder.Compose(pyramid, top, new List<List<TileSlot>>());

            Assert.That(composed.Count % 2, Is.EqualTo(0));
            Assert.That(composed.Count, Is.EqualTo(pyramid.Count + 3 + 2 - 1));
            Assert.That(composed.Any(s => s.Id == "TS0_2"), Is.False, "the deepest column's top tile is the one trimmed");
            Assert.That(pyramid.All(p => composed.Any(s => s.Id == p.Id)), Is.True, "the pyramid must never be trimmed");
        }
    }
}
```

- [ ] **Step 2: Verify tests fail to compile**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Check `get_console_logs` for `CS0246: The type or namespace name 'BoardShapeBuilder' could not be found`.

- [ ] **Step 3: Implement**

Create `unity/GameClient/Assets/Scripts/Domain/Generation/BoardShapeBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Model;

namespace GameDomain.Generation
{
    // Positions a row of reserve-stack columns relative to a pyramid's own
    // bounding box, and composes pyramid + stacks into one board shape. See
    // the design spec §5.2 - stack column COUNT never changes (always
    // DefaultColumnsPerRow per row); only DEPTH grows, uniformly across every
    // column in a row, which is what keeps the total tile count always even
    // for Pair mode without ever needing the parity-trim fallback in practice.
    public static class BoardShapeBuilder
    {
        public const int DefaultColumnsPerRow = 4;
        // Half-tile units of clearance above/below the pyramid's own bounding
        // box. Concrete starting value; tune on-device like every other
        // rendering constant in this project (e.g. TileView3D.FlipHalfDuration).
        public const int DefaultRowGap = 6;

        public static List<List<TileSlot>> BuildStackRow(
            List<TileSlot> pyramid, int columnCount, int depth, SlotRegion region, int yGap)
        {
            var columns = new List<List<TileSlot>>();
            if (depth <= 0) return columns;

            int minX = pyramid.Min(s => s.X);
            int maxX = pyramid.Max(s => s.X);
            int minY = pyramid.Min(s => s.Y);
            int maxY = pyramid.Max(s => s.Y);

            int rowY = region == SlotRegion.TopStack ? maxY + yGap : minY - yGap;
            string prefix = region == SlotRegion.TopStack ? "TS" : "BS";

            for (int c = 0; c < columnCount; c++)
            {
                // Evenly spaced across the pyramid's own X span. Rounded to the
                // same odd parity every pyramid X already uses (TurtleShapeBuilder
                // always emits odd X), purely so stack columns land visually
                // on-grid - domain generation doesn't require this.
                int x = minX + (int)Math.Round((maxX - minX) * (c + 0.5) / columnCount);
                if (Math.Abs(x % 2) != Math.Abs(minX % 2)) x++;

                columns.Add(StackColumnBuilder.Build(x, rowY, depth, region, prefix + c));
            }
            return columns;
        }

        public static List<TileSlot> Compose(
            List<TileSlot> pyramid, List<List<TileSlot>> topColumns, List<List<TileSlot>> bottomColumns)
        {
            var all = new List<TileSlot>(pyramid);
            foreach (var col in topColumns) all.AddRange(col);
            foreach (var col in bottomColumns) all.AddRange(col);

            // Defensive fallback only: under BuildStackRow's uniform-depth-per-row
            // scheme, pyramid (always even) + columnCount * depth (always a
            // multiple of columnCount) is always even, so this should never
            // actually trigger in practice. Kept as a safety net - same
            // top-down-trim pattern TurtleShapeBuilder.Build already uses for its
            // own parity fix - in case columns are ever made uneven later.
            if (all.Count % 2 == 1)
            {
                var nonEmpty = topColumns.Concat(bottomColumns).Where(c => c.Count > 0).ToList();
                if (nonEmpty.Count > 0)
                {
                    var deepest = nonEmpty.OrderByDescending(c => c.Count).First();
                    all.Remove(deepest[deepest.Count - 1]);
                }
            }
            return all;
        }
    }
}
```

- [ ] **Step 4: Run tests, verify pass**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: all pass, total count up by 8 from Task 2's baseline.

- [ ] **Step 5: Commit** (hold until the user says go)

```bash
git add unity/GameClient/Assets/Scripts/Domain/Generation/BoardShapeBuilder.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/BoardShapeBuilderTests.cs
git commit -m "Add BoardShapeBuilder to position and compose reserve-stack rows"
```

---

### Task 4: `LevelCatalog` — `StackDepthPerColumn` progression

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Domain/Progression/LevelData.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Progression/ProgressionTests.cs`

**Interfaces:**
- Consumes: nothing new (pure arithmetic on existing `LevelData` fields).
- Produces: `LevelData.StackDepthPerColumn` (int, default `0`). Task 6
  (`GameController`) reads this exact name.

- [ ] **Step 1: Write the failing tests**

Add to `unity/GameClient/Assets/Scripts/Tests/EditMode/Progression/ProgressionTests.cs`,
inside the `ProgressionTests` class, after `LevelCatalog_Get_PlateausAtDifficulty5_ForVeryHighLevels`:

```csharp
        [Test]
        public void LevelCatalog_Get_BeforePlateau_HasNoStackDepth()
        {
            for (int id = 1; id <= 38; id++)
                Assert.That(LevelCatalog.Get(id)?.StackDepthPerColumn, Is.EqualTo(0), $"level {id} should have no stacks yet");
        }

        [Test]
        public void LevelCatalog_Get_PastPlateau_StackDepthGrowsEvery4Levels()
        {
            Assert.That(LevelCatalog.Get(41)?.StackDepthPerColumn, Is.EqualTo(0));
            Assert.That(LevelCatalog.Get(42)?.StackDepthPerColumn, Is.EqualTo(1));
            Assert.That(LevelCatalog.Get(46)?.StackDepthPerColumn, Is.EqualTo(2));
        }

        [Test]
        public void LevelCatalog_Get_StackDepth_NeverStopsGrowing()
        {
            Assert.That(LevelCatalog.Get(1000)?.StackDepthPerColumn, Is.GreaterThan(50));
        }
```

- [ ] **Step 2: Verify tests fail**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: the 3 new tests FAIL (`StackDepthPerColumn` doesn't exist yet — compile error).

- [ ] **Step 3: Implement**

In `unity/GameClient/Assets/Scripts/Domain/Progression/LevelData.cs`, add the
field to `LevelData` (after `public GameDomain.Generation.MatchMode Mode = ...`):

```csharp
        public int StackDepthPerColumn = 0; // tiles per reserve-stack column; 0 = no stacks
```

Then replace `LevelCatalog.GenerateProcedural` with:

```csharp
        // How often (in levels, past the difficulty-5 plateau) every stack
        // column grows by one tile. Uniform across all 8 columns always, so
        // there's never a per-column imbalance to distribute, and the total
        // stack tile count (BoardShapeBuilder.DefaultColumnsPerRow * 2 *
        // depth) is always a multiple of 8 - always even for Pair mode.
        private const int StackGrowthEveryLevels = 4;

        private static LevelData GenerateProcedural(int levelId, int lastAuthoredId)
        {
            int stepsIn = (levelId - lastAuthoredId - 1) / RampLevelsPerDifficultyStep;
            int difficulty = 1 + stepsIn;
            if (difficulty > 5) difficulty = 5;

            int parAids = 4 - difficulty;
            if (parAids < 1) parAids = 1;
            if (parAids > 3) parAids = 3;

            int stackDepth = 0;
            if (difficulty >= 5)
            {
                // plateauLevelId is the first level where difficulty reaches 5 -
                // derived from the ramp constants, not hardcoded, so it stays
                // correct if RampLevelsPerDifficultyStep ever changes.
                int plateauLevelId = lastAuthoredId + 1 + RampLevelsPerDifficultyStep * 4;
                int levelsPastPlateau = levelId - plateauLevelId;
                if (levelsPastPlateau > 0)
                    stackDepth = levelsPastPlateau / StackGrowthEveryLevels;
            }

            return new LevelData
            {
                LevelId = levelId,
                Name = "Level " + levelId,
                Difficulty = difficulty,
                ParAids = parAids,
                StackDepthPerColumn = stackDepth,
                Mode = GameDomain.Generation.MatchMode.Pair,
            };
        }
```

(Leave `Get`, `NextLevelId`, and the authored `Levels` list untouched — authored
levels default `StackDepthPerColumn` to `0` automatically since they don't set it.)

- [ ] **Step 4: Run tests, verify pass**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: all pass, including every existing `LevelCatalog_Get_*` test (levels
1-38's `Difficulty` values are untouched by this change) — total count up by 3.

- [ ] **Step 5: Commit** (hold until the user says go)

```bash
git add unity/GameClient/Assets/Scripts/Domain/Progression/LevelData.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Progression/ProgressionTests.cs
git commit -m "Add StackDepthPerColumn growth to LevelCatalog past the difficulty plateau"
```

---

### Task 5: Deep-stack integration regression guards

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/StackIntegrationTests.cs`

**Interfaces:**
- Consumes: `StackColumnBuilder` (Task 2), `BoardShapeBuilder` (Task 3),
  `BoardGenerator.GenerateShaped` (existing), `HiddenTileSelector.Apply` /
  `TileReveal.TryReveal` (existing, `GameDomain.Gameplay`).
- Produces: nothing new — this task is pure regression coverage for the two
  Review Focus items about deep stacks (hidden-tile-through-a-stack, and
  generation timing).

- [ ] **Step 1: Write the failing tests**

Create `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/StackIntegrationTests.cs`:

```csharp
using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using GameDomain.Gameplay;
using GameDomain.Generation;
using GameDomain.Model;
using GameDomain.Tests.Solving;

namespace GameDomain.Tests.Generation
{
    public class StackIntegrationTests
    {
        [Test]
        public void GenerateShaped_DeepStackBoard_IsSolvable_WithinReasonableTime()
        {
            var pyramid = TurtleShapeBuilder.BuildForDifficulty(5);
            var top = BoardShapeBuilder.BuildStackRow(pyramid, BoardShapeBuilder.DefaultColumnsPerRow, 60, SlotRegion.TopStack, BoardShapeBuilder.DefaultRowGap);
            var bottom = BoardShapeBuilder.BuildStackRow(pyramid, BoardShapeBuilder.DefaultColumnsPerRow, 60, SlotRegion.BottomStack, BoardShapeBuilder.DefaultRowGap);
            var shape = BoardShapeBuilder.Compose(pyramid, top, bottom);

            var level = new LevelDefinition { LevelId = 1000, Shape = shape, TileSetId = "test" };
            var profile = DifficultyProfile.For(5, MatchMode.Pair);

            var stopwatch = Stopwatch.StartNew();
            BoardState board = null;
            Assert.DoesNotThrow(() => board = BoardGenerator.GenerateShaped(level, new Random(42), profile, null, 26));
            stopwatch.Stop();

            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000),
                $"deep-stack board ({shape.Count} tiles) took {stopwatch.ElapsedMilliseconds}ms to generate");

            var values = board.Cells.ToDictionary(kv => kv.Key, kv => kv.Value.Value);
            bool solvable = BacktrackingSolver.IsSolvable(shape, values, profile.GroupSize);
            Assert.That(solvable, Is.True, "deep-stack board must still be solvable");
        }

        [Test]
        public void HiddenStackTile_BecomesRevealable_OnlyAfterTheOneAboveItClears()
        {
            var column = StackColumnBuilder.Build(x: 1, y: 1, depth: 3, SlotRegion.TopStack, columnId: "TS0");
            var board = new BoardState();
            foreach (var slot in column)
                board.Cells[slot.Id] = new TileCell { Value = "0" };

            // Force every cell hidden (bypasses the difficulty gate/randomness -
            // this test only cares about the reveal/freedom interaction, not
            // selection probability, which HiddenTileSelectorTests already covers).
            foreach (var cell in board.Cells.Values) { cell.IsHiddenTile = true; cell.Revealed = false; }

            var slotsById = column.ToDictionary(s => s.Id);
            var bottom = slotsById["TS0_0"];
            var middle = slotsById["TS0_1"];
            var top = slotsById["TS0_2"];

            // The buried bottom/middle tiles aren't free yet - revealing them must fail.
            Assert.That(TileReveal.TryReveal(board, slotsById, bottom.Id, out _), Is.False);
            Assert.That(TileReveal.TryReveal(board, slotsById, middle.Id, out _), Is.False);

            // The top tile IS free - revealing it must succeed.
            Assert.That(TileReveal.TryReveal(board, slotsById, top.Id, out _), Is.True);
            Assert.That(board.Cells[top.Id].Revealed, Is.True);

            // Clear the (now-revealed) top tile, exposing the middle one.
            board.Cells[top.Id].Cleared = true;
            board.PeekedTileId = null;

            // The middle tile is now the top of the pile - it must become revealable.
            Assert.That(TileReveal.TryReveal(board, slotsById, middle.Id, out _), Is.True);
            Assert.That(board.Cells[middle.Id].Revealed, Is.True);

            // The bottom tile is STILL covered by the (uncleared) middle tile.
            Assert.That(TileReveal.TryReveal(board, slotsById, bottom.Id, out _), Is.False);
        }
    }
}
```

- [ ] **Step 2: Verify tests fail for the right reason**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: both new tests FAIL — `TryReveal`'s "not free" checks pass fine, but
whichever piece isn't wired yet should surface as a real assertion failure (not
a compile error, since everything referenced already exists from Tasks 1-4) —
this task is coverage, not new production code, so if both already pass, that
confirms Tasks 1-4 composed correctly rather than indicating a broken test.

- [ ] **Step 3: N/A — no production code in this task**

If Step 2 shows a genuine logic failure (not just "not yet wired"), stop and
treat it as a real bug in Tasks 1-4 to fix before continuing, rather than
adjusting the test to match broken behavior.

- [ ] **Step 4: Run tests, verify pass**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: all pass, total count up by 2 from Task 4's baseline.

- [ ] **Step 5: Commit** (hold until the user says go)

```bash
git add unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/StackIntegrationTests.cs
git commit -m "Add deep-stack solvability/timing and hidden-tile-reveal regression tests"
```

---

### Task 6: `GameController.PrepareLevel` wiring

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/GameController.cs:156-172`

**Interfaces:**
- Consumes: `LevelData.StackDepthPerColumn` (Task 4), `BoardShapeBuilder
  .BuildStackRow`/`.Compose`/`.DefaultColumnsPerRow`/`.DefaultRowGap` (Task 3).
- Produces: `_shape` now includes stack tiles whenever the current level's
  `StackDepthPerColumn > 0`; everything downstream (`BoardGenerator
  .GenerateShaped`, `HiddenTileSelector.Apply`, tray/hint/undo) is unchanged
  and already consumes `_shape`/`_board` generically.

- [ ] **Step 1: Implement**

`GameController.cs` is a `MonoBehaviour` in the implicit `Assembly-CSharp`
assembly, which `Domain.Tests` cannot reference (confirmed during the
hidden-tile-reveal work) — so this task is verified via a live-Editor `eval`
check (Step 2) rather than an NUnit test.

Replace the shape-building block in `PrepareLevel`
(`unity/GameClient/Assets/Scripts/Presentation/GameController.cs:161-172`):

```csharp
            if (_currentLevelId == 5)
            {
                // The big showcase pyramid, capped at the project MAX of 54 tiles so
                // it fits the play area cleanly at full tile size (a 7x5 turtle) with
                // no overlap into the tray or the bottom buttons.
                _shape = TurtleShapeBuilder.BuildWithTileCount(TurtleShapeBuilder.MaxTiles);
            }
            else
            {
                _shape = TurtleShapeBuilder.BuildForDifficulty(difficulty);
            }
            _slotsById = _shape.ToDictionary(s => s.Id);
```

with:

```csharp
            if (_currentLevelId == 5)
            {
                // The big showcase pyramid, capped at the project MAX of 54 tiles so
                // it fits the play area cleanly at full tile size (a 7x5 turtle) with
                // no overlap into the tray or the bottom buttons.
                _shape = TurtleShapeBuilder.BuildWithTileCount(TurtleShapeBuilder.MaxTiles);
            }
            else
            {
                _shape = TurtleShapeBuilder.BuildForDifficulty(difficulty);
            }

            // Past the difficulty-5 plateau, further growth comes from reserve
            // stacks instead of the (now frozen) pyramid - see the design spec.
            if (levelData.StackDepthPerColumn > 0)
            {
                var topStacks = BoardShapeBuilder.BuildStackRow(
                    _shape, BoardShapeBuilder.DefaultColumnsPerRow, levelData.StackDepthPerColumn,
                    SlotRegion.TopStack, BoardShapeBuilder.DefaultRowGap);
                var bottomStacks = BoardShapeBuilder.BuildStackRow(
                    _shape, BoardShapeBuilder.DefaultColumnsPerRow, levelData.StackDepthPerColumn,
                    SlotRegion.BottomStack, BoardShapeBuilder.DefaultRowGap);
                _shape = BoardShapeBuilder.Compose(_shape, topStacks, bottomStacks);
            }

            _slotsById = _shape.ToDictionary(s => s.Id);
```

- [ ] **Step 2: Verify via live Editor**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Check `get_console_logs` for zero errors, then:

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json editor_play
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var gc = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.GameController>(); var f = typeof(GameClient.Presentation.GameController).GetField(\"_currentLevelId\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); f.SetValue(gc, 42); gc.ClearBoard(); gc.PrepareLevel(); var sf = typeof(GameClient.Presentation.GameController).GetField(\"_shape\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); var shape = (System.Collections.Generic.List<GameDomain.Model.TileSlot>)sf.GetValue(gc); int stackTiles = shape.FindAll(s => s.Region != GameDomain.Model.SlotRegion.Pyramid).Count; UnityEngine.Debug.Log(\"LEVEL42_TOTAL=\" + shape.Count + \" STACK_TILES=\" + stackTiles);"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json get_console_logs
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json editor_stop
```
Expected: `LEVEL42_TOTAL=62 STACK_TILES=8` (54-tile pyramid + 8 columns × 1 tile
depth, matching Task 4's `LevelCatalog.Get(42)?.StackDepthPerColumn == 1`), and
zero console errors.

- [ ] **Step 3: Run the full EditMode suite as a regression guard**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: same pass count as the end of Task 5 (this task only touches
`GameController`, which isn't covered by `Domain.Tests`).

- [ ] **Step 4: Commit** (hold until the user says go)

```bash
git add unity/GameClient/Assets/Scripts/Presentation/GameController.cs
git commit -m "Wire reserve-stack composition into GameController.PrepareLevel"
```

---

### Task 7: `BoardView3D` — decouple stack tiles from pyramid render/camera math

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/Board3D/BoardView3D.cs`

**Interfaces:**
- Consumes: `TileSlot.Region` (Task 1).
- Produces: `PlaceTileView` and `FitCameraToBoard` no longer let a stack
  column's true depth affect the rendered footprint or camera zoom — the exact
  fix the design spec §3.3 identified as required before stacks can be used at
  all. Task 8 (badges) and Task 9 (final verification) depend on this.

- [ ] **Step 1: Implement — `PlaceTileView`**

Replace `PlaceTileView` (`BoardView3D.cs:318-328`):

```csharp
        private void PlaceTileView(TileView3D view, TileSlot slot)
        {
            var jitter = JitterFor(slot.Id);
            var layerOffset = LayerRenderOffset(slot.Layer);
            view.transform.localPosition = new Vector3(
                slot.X * _cellWidth + jitter.x + layerOffset.x,
                slot.Y * _cellHeight + jitter.y + layerOffset.y,
                -slot.Layer * _layerHeight);
            view.transform.localRotation = Quaternion.Euler(0f, 0f, jitter.z);
            view.UpdateSortingOrder();
        }
```

with:

```csharp
        // Only the pyramid's diagonal per-layer straddle offset scales with
        // Layer - that's correct there (4-7 layers) but would be a serious bug
        // for a reserve stack, whose Layer can run into the hundreds (spec
        // §3.3). Stack tiles get a small, HARD-CAPPED nudge instead, so a
        // stack's on-screen footprint never grows no matter how deep it is -
        // only its Z depth does, and the current top tile (highest Layer,
        // closest to camera - see -slot.Layer * _layerHeight below) always
        // renders in front of, and mostly occludes, everything buried beneath it.
        private const int StackVisibleNudgeLayers = 3;
        private const float StackNudgeStep = 0.02f;

        private void PlaceTileView(TileView3D view, TileSlot slot)
        {
            var jitter = JitterFor(slot.Id);
            Vector2 layerOffset;
            if (slot.Region == SlotRegion.Pyramid)
            {
                layerOffset = LayerRenderOffset(slot.Layer);
            }
            else
            {
                int nudgeIndex = Mathf.Min(slot.Layer, StackVisibleNudgeLayers - 1);
                layerOffset = new Vector2(nudgeIndex * StackNudgeStep, -nudgeIndex * StackNudgeStep);
            }
            view.transform.localPosition = new Vector3(
                slot.X * _cellWidth + jitter.x + layerOffset.x,
                slot.Y * _cellHeight + jitter.y + layerOffset.y,
                -slot.Layer * _layerHeight);
            view.transform.localRotation = Quaternion.Euler(0f, 0f, jitter.z);
            view.UpdateSortingOrder();
        }
```

- [ ] **Step 2: Implement — `FitCameraToBoard`**

Replace the `maxLayer` line in `FitCameraToBoard` (`BoardView3D.cs:214`):

```csharp
            int maxLayer = slotsById.Values.Max(s => s.Layer);
```

with:

```csharp
            // Only pyramid tiles receive LayerRenderOffset's per-layer diagonal
            // drift (see PlaceTileView above), so only they should inflate the
            // camera-fit padding that compensates for it - a deep stack's Layer
            // can run into the hundreds and must NOT blow up this bound (spec §3.3).
            var pyramidSlots = slotsById.Values.Where(s => s.Region == SlotRegion.Pyramid).ToList();
            int maxLayer = pyramidSlots.Count > 0 ? pyramidSlots.Max(s => s.Layer) : 0;
```

- [ ] **Step 3: Verify via live Editor — restore-tile path**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Check `get_console_logs` for zero errors, then confirm `RestoreTile` (which
calls the same `PlaceTileView`) works for a stack tile via undo:

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json editor_play
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var gc = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.GameController>(); var f = typeof(GameClient.Presentation.GameController).GetField(\"_currentLevelId\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); f.SetValue(gc, 42); gc.ClearBoard(); gc.BeginLevel();"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var s = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.HUD3D.LevelStartScreen3D>(UnityEngine.FindObjectsInactive.Include); if (s != null) s.gameObject.SetActive(false);"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json get_console_logs
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json editor_stop
```
Expected: zero console errors/exceptions during `BeginLevel` (which internally
calls `BoardView3D.Build`, exercising `PlaceTileView`/`FitCameraToBoard` for
every stack tile in level 42's board, including calling `UpdateSortingOrder`
without throwing). A full undo-specific check (forcing an actual tray collect +
undo cycle on a stack tile) is folded into Task 9's broader manual pass, since
it needs real gameplay interaction, not just `eval` scripting.

- [ ] **Step 4: Run the full EditMode suite as a regression guard**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: same pass count as the end of Task 6 (this task only touches
`BoardView3D`, which isn't covered by `Domain.Tests`).

- [ ] **Step 5: Commit** (hold until the user says go)

```bash
git add unity/GameClient/Assets/Scripts/Presentation/Board3D/BoardView3D.cs
git commit -m "Decouple reserve-stack tiles from pyramid render offset and camera-fit math"
```

---

### Task 8: Stack pile count badges

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Presentation/Board3D/StackPileBadge3D.cs`
- Modify: `unity/GameClient/Assets/Scripts/Presentation/Board3D/BoardView3D.cs`

**Interfaces:**
- Consumes: `TileSlot.Region`/`StackColumnId` (Task 1).
- Produces: `StackPileBadge3D.Create(Transform parent, string name) ->
  StackPileBadge3D`, `StackPileBadge3D.SetCount(int remainingBelowTop)`.
  `BoardView3D` now shows a live `+N` badge per reserve-stack pile, refreshed
  by the same `RefreshFreeStates` call every board mutation already triggers.

- [ ] **Step 1: Implement `StackPileBadge3D`**

Create `unity/GameClient/Assets/Scripts/Presentation/Board3D/StackPileBadge3D.cs`:

```csharp
using TMPro;
using UnityEngine;

namespace GameClient.Presentation.Board3D
{
    // A small floating "+N" label above a reserve-stack pile, reporting how
    // many MORE tiles remain buried beneath the current top tile. Built
    // procedurally at runtime - this project has no hand-authored prefabs for
    // HUD-style elements; GameSceneBuilder3D uses the identical
    // `new GameObject(name, typeof(TextMeshPro))` pattern for every other
    // in-scene label.
    public sealed class StackPileBadge3D : MonoBehaviour
    {
        private TextMeshPro _text;

        public static StackPileBadge3D Create(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            var badge = go.AddComponent<StackPileBadge3D>();
            badge._text = go.GetComponent<TextMeshPro>();
            badge._text.fontSize = 3f;
            badge._text.alignment = TextAlignmentOptions.Center;
            badge._text.color = Color.white;
            return badge;
        }

        // remainingBelowTop: uncleared tiles in this pile beneath its current
        // top tile. 0 hides the badge (nothing left to hint at).
        public void SetCount(int remainingBelowTop)
        {
            gameObject.SetActive(remainingBelowTop > 0);
            if (remainingBelowTop > 0) _text.text = "+" + remainingBelowTop;
        }
    }
}
```

- [ ] **Step 2: Wire badges into `BoardView3D`**

Add the badge dictionary field, alongside `_tileViews`
(`BoardView3D.cs:92`):

```csharp
        private readonly Dictionary<string, TileView3D> _tileViews = new Dictionary<string, TileView3D>();
        private readonly Dictionary<string, StackPileBadge3D> _stackBadges = new Dictionary<string, StackPileBadge3D>();
```

Extend `Clear()` (`BoardView3D.cs:100-105`) to also destroy badges:

```csharp
        public void Clear()
        {
            foreach (var view in _tileViews.Values)
                if (view != null) Destroy(view.gameObject);
            _tileViews.Clear();

            foreach (var badge in _stackBadges.Values)
                if (badge != null) Destroy(badge.gameObject);
            _stackBadges.Clear();
        }
```

Add `RefreshStackBadges` and call it from the end of `RefreshFreeStates`
(`BoardView3D.cs:340-356`):

```csharp
        public void RefreshFreeStates(BoardState board)
        {
            var remaining = new HashSet<string>(
                board.Cells
                    .Where(kv => !kv.Value.Cleared && !board.TrayTileIds.Contains(kv.Key))
                    .Select(kv => kv.Key));

            foreach (var kv in _tileViews)
            {
                bool isFree = FreedomRuleCalculator.IsFree(_slotsById[kv.Key], remaining);
                kv.Value.SetFree(isFree);
            }

            RefreshStackBadges(board);
        }

        // Recomputes every reserve-stack pile's "+N" badge from the board's
        // current cleared state - cheap (bounded by total stack tile count,
        // typically a few hundred at most even at extreme levels) and always
        // correct, since it derives the count fresh each call rather than
        // tracking incremental state.
        private void RefreshStackBadges(BoardState board)
        {
            var byColumn = _slotsById.Values
                .Where(s => s.Region != SlotRegion.Pyramid && s.StackColumnId != null)
                .GroupBy(s => s.StackColumnId);

            foreach (var column in byColumn)
            {
                var uncleared = column
                    .Where(s => board.Cells.TryGetValue(s.Id, out var cell) && !cell.Cleared)
                    .OrderByDescending(s => s.Layer)
                    .ToList();

                if (!_stackBadges.TryGetValue(column.Key, out var badge))
                {
                    badge = StackPileBadge3D.Create(transform, "Badge_" + column.Key);
                    _stackBadges[column.Key] = badge;
                }

                if (uncleared.Count == 0)
                {
                    badge.SetCount(0);
                    continue;
                }

                var topSlot = uncleared[0]; // highest remaining Layer = current top of pile
                badge.transform.localPosition = new Vector3(
                    topSlot.X * _cellWidth, topSlot.Y * _cellHeight + 0.3f, -topSlot.Layer * _layerHeight - 0.1f);
                badge.SetCount(uncleared.Count - 1);
            }
        }
```

- [ ] **Step 3: Verify via live Editor**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Check `get_console_logs` for zero errors, then:

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json editor_play
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var gc = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.GameController>(); var f = typeof(GameClient.Presentation.GameController).GetField(\"_currentLevelId\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); f.SetValue(gc, 46); gc.ClearBoard(); gc.BeginLevel();"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var s = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.HUD3D.LevelStartScreen3D>(UnityEngine.FindObjectsInactive.Include); if (s != null) s.gameObject.SetActive(false); var bv = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.Board3D.BoardView3D>(); var badges = bv.GetComponentsInChildren<GameClient.Presentation.Board3D.StackPileBadge3D>(true); UnityEngine.Debug.Log(\"BADGE_COUNT=\" + badges.Length);"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json get_console_logs
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json editor_stop
```
Expected: `BADGE_COUNT=8` (level 46 → `StackDepthPerColumn = 2`, per Task 4's
test, across 8 columns), zero console errors.

- [ ] **Step 4: Run the full EditMode suite as a regression guard**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: same pass count as the end of Task 7.

- [ ] **Step 5: Commit** (hold until the user says go)

```bash
git add unity/GameClient/Assets/Scripts/Presentation/Board3D/StackPileBadge3D.cs \
        unity/GameClient/Assets/Scripts/Presentation/Board3D/BoardView3D.cs
git commit -m "Add per-pile +N count badges for reserve stacks"
```

---

### Task 9: End-to-end verification

**Files:** none (verification only).

**Interfaces:** none — this task exercises the whole feature together and
closes out the Review Focus items that need a live board (deal-in pacing,
camera-fit stability).

- [ ] **Step 1: Confirm levels 1-38 are visually unchanged**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json editor_play
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var gc = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.GameController>(); var f = typeof(GameClient.Presentation.GameController).GetField(\"_currentLevelId\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); f.SetValue(gc, 5); gc.ClearBoard(); gc.BeginLevel();"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var s = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.HUD3D.LevelStartScreen3D>(UnityEngine.FindObjectsInactive.Include); if (s != null) s.gameObject.SetActive(false); var cam = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Camera>(); UnityEngine.Debug.Log(\"LEVEL5_ORTHOSIZE=\" + cam.orthographicSize);"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json capture_game_view
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json get_console_logs
```
Expected: the captured screenshot matches the pre-feature level 5 layout (pure
54-tile pyramid, no stacks/badges anywhere), and the logged `LEVEL5_ORTHOSIZE`
is recorded for comparison against Step 2.

- [ ] **Step 2: Confirm camera framing stays stable at an extreme stack depth**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var gc = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.GameController>(); var f = typeof(GameClient.Presentation.GameController).GetField(\"_currentLevelId\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); f.SetValue(gc, 1000); gc.ClearBoard(); gc.BeginLevel();"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var cam = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Camera>(); UnityEngine.Debug.Log(\"LEVEL1000_ORTHOSIZE=\" + cam.orthographicSize);"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json capture_game_view
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json get_console_logs
```
Expected: `LEVEL1000_ORTHOSIZE` is close to Step 1's `LEVEL5_ORTHOSIZE` (within
the same fixed-zoom tolerance `_fixedOrthographicSize` already enforces when set
— if the scene uses the legacy dynamic fit instead, the two values should still
be in the same order of magnitude, NOT 10-100x apart, which is what the
pre-fix `maxLayer` bug would have produced for a ~240-tile stack-heavy board).
The screenshot shows the pyramid at its normal size with 8 small piles
(with visible `+N` badges) sandwiching it top and bottom — not a
massively-zoomed-out board with everything shrunk to specks.

- [ ] **Step 3: Confirm deal-in pacing is still reasonable at that depth**

While still on level 1000 from Step 2, observe the deal-in animation timing
via the console (no separate re-trigger needed — `BeginLevel` in Step 2 already
played it):

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "UnityEngine.Debug.Log(\"DEALIN_CHECK_DONE\");"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json get_console_logs
```
Manually review the captured screenshot/timing: the deal-in should complete
within a few seconds, not visibly stall. If it's noticeably slower than a
stack-free level, note this as a follow-up performance item (per the design
spec §8's explicit deferral of stack-tile-view pooling/windowing to a
profiling-driven follow-up) rather than blocking this plan on it — the spec
already scoped that optimization out.

- [ ] **Step 4: Exercise undo on a stack tile through real gameplay**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var gc = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.GameController>(); var f = typeof(GameClient.Presentation.GameController).GetField(\"_currentLevelId\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); f.SetValue(gc, 42); gc.ClearBoard(); gc.BeginLevel();"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var gc = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.GameController>(); var bf = typeof(GameClient.Presentation.GameController).GetField(\"_board\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); var board = (GameDomain.Model.BoardState)bf.GetValue(gc); var sf = typeof(GameClient.Presentation.GameController).GetField(\"_slotsById\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); var slots = (System.Collections.Generic.Dictionary<string, GameDomain.Model.TileSlot>)sf.GetValue(gc); string stackTop = null; foreach (var kv in slots) { if (kv.Value.Region != GameDomain.Model.SlotRegion.Pyramid && !board.Cells[kv.Key].Cleared) { var remaining = new System.Collections.Generic.HashSet<string>(System.Linq.Enumerable.Where(board.Cells.Keys, id => !board.Cells[id].Cleared)); if (GameDomain.Generation.FreedomRuleCalculator.IsFree(kv.Value, remaining)) { stackTop = kv.Key; break; } } } UnityEngine.Debug.Log(\"STACK_TOP_TO_TAP=\" + stackTop); gc.OnTileTapped(stackTop); gc.OnTileTapped(stackTop);"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var gc = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.GameController>(); gc.OnUndoRequested();"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json get_console_logs
```
Expected: no exceptions through the tap-reveal/tap-collect/undo sequence on a
real stack tile — the restored tile reappears via `BoardView3D.RestoreTile` →
`PlaceTileView` at the correct position (Task 7's fix exercised for real,
closing the Review Focus item that no test covers this path directly). If
`OnUndoRequested` isn't the exact public method name, check
`GameController.cs` for whatever the undo button actually calls and use that.

- [ ] **Step 5: Stop the Editor**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json editor_stop
```

- [ ] **Step 6: Final full-suite regression run**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: 100% pass, including every test added in Tasks 1-5, and no change to
any pre-existing test's outcome.

This task has no commit — it's verification-only, closing out the plan.

---

## Spec Coverage Check

- §5.1 (`TileSlot.Region`) → Task 1.
- §5.2 (`StackColumnBuilder`, `BoardShapeBuilder`) → Tasks 2-3.
- §5.4 (`LevelCatalog.StackDepthPerColumn`) → Task 4.
- §5.5 (`GameController.PrepareLevel`) → Task 6.
- §5.3 (`BoardView3D` render/camera-fit decoupling, badges) → Tasks 7-8.
- §6/§7 (data flow guarantees: levels 1-38 unchanged, generation never fails,
  `HiddenTileSelector`/`TileReveal` need no changes) → Tasks 4-6 (unchanged-shape
  assertions) and Task 5 (hidden-tile-through-a-stack integration test).
- §8 open items (visible-depth/pooling tuning, exact constants, branching
  interaction at high stack fractions) → intentionally left as on-device tuning
  per the spec's own framing, called out again in Task 9 Step 3.
