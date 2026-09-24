# Pyramid Reserve Stacks (Unlimited Level Scaling) — Design

**Date:** 2026-09-24
**Status:** Design approved by owner in chat; spec under review
**Scope:** Once a procedural level's difficulty maxes out (today's plateau at
`Difficulty = 5`, ~level 38), further growth in tile count / hardness comes from a
fixed row of face-down-capable reserve stacks above and below the pyramid, instead
of the pyramid itself growing. The pyramid's own footprint never changes again.

---

## 1. Background & Problem

Board shape today comes entirely from `TurtleShapeBuilder.BuildForDifficulty`
(`unity/GameClient/Assets/Scripts/Domain/Generation/TurtleShapeBuilder.cs:136-148`),
which maps `Difficulty` 1-5 to fixed tile counts (12/20/26/30/54) and hard-caps at
`MaxTiles = 54` because a bigger board at the fixed tile size would overlap the
tray or the bottom buttons (`TurtleShapeBuilder.cs:20-26`).

`LevelCatalog.GenerateProcedural` (`unity/GameClient/Assets/Scripts/Domain/
Progression/LevelData.cs:50-68`) ramps `Difficulty` up by 1 every
`RampLevelsPerDifficultyStep = 8` levels and clamps at 5. Once difficulty hits 5
(~level 38), **every subsequent level generates the identical 54-tile board
forever** — tile count and difficulty both permanently plateau, contrary to the
"never dead-ends" intent already stated in that file's own comments.

**Goal:** let levels keep growing in tile count and hardness indefinitely past
that plateau, without the pyramid ever exceeding its current safe on-screen
footprint. Overflow tiles live in a fixed number of vertical reserve stacks
sandwiched above and below the pyramid (mockup reference: a row of small piles,
each showing one face-up top card and a `+N` badge for the hidden tiles beneath
it).

## 2. Non-Goals (YAGNI)

- **Levels 1-38 (authored 1-5 plus procedural up to the difficulty-5 plateau) are
  completely unchanged** — pure pyramid, no stacks, byte-for-byte identical shapes
  to today. Stacks only exist once `Difficulty == 5`.
- Not changing `BoardGenerator`, `ReverseConstructionSolver`,
  `BranchingOrderBuilder`, `FreedomRuleCalculator`, or `DifficultyProfile` — see §4,
  they already operate on an abstract slot graph and need no topology-specific
  code.
- Not making stack *column count* grow — always a fixed 4 top + 4 bottom (8 total,
  per the approved mockup). Only stack *depth* is the growth knob.
- Not building a pooling/object-recycling system up front for stack tile views —
  see §8, this is an explicit profile-then-decide item.
- Not supporting `MatchMode.Triple` stacks — procedural levels are Pair-only today
  (`LevelData.cs:38`, unchanged non-goal from the hidden-tile-reveal design), so
  stack growth only needs to preserve even tile counts, not multiples of three.

## 3. Key Concepts

### 3.1 Region: Pyramid vs. TopStack vs. BottomStack

A board's `TileSlot` list is now a **composition** of three independently-built
pieces sharing one coordinate space:

- **Pyramid** — exactly what `TurtleShapeBuilder.BuildForDifficulty(5)` produces
  today (`BuildWithTileCount(MaxTiles)`, 54 tiles). Frozen forever.
- **TopStack** / **BottomStack** — 4 vertical single-file columns each, placed
  above and below the pyramid's bounding box respectively. Column *count* is
  fixed; column *depth* (tiles per column) is the new growth variable.

### 3.2 A stack is a degenerate covering graph, not a new solver concept

`FreedomRuleCalculator.IsFree` (`FreedomRuleCalculator.cs:8-20`) already reduces to
exactly "LIFO pile" behavior for a slot whose `LeftNeighborId`/`RightNeighborId`
are both `null`:

```csharp
bool leftOpen = slot.LeftNeighborId == null || ...   // null -> true
bool rightOpen = slot.RightNeighborId == null || ...  // null -> true
return leftOpen || rightOpen;   // always true when both are null
```

So a stack tile is free **iff** `CoveredByIds` is empty — i.e., iff it's currently
the top of its pile. Wiring `CoveredByIds` for a stack column to "the tile directly
above me in the same column" (and leaving neighbors `null`) is the entire domain
model. No new solver, no new freedom rule.

### 3.3 Rendered depth is capped independent of true depth

`BoardView3D.PlaceTileView` (`BoardView3D.cs:318-328`) positions every tile using
`LayerRenderOffset(slot.Layer)` — a diagonal X/Y nudge that **grows linearly with
Layer**, tuned for the pyramid's 4-7 layers. Applying that formula to a 60-deep
stack would drift tile 60 sixty units off to the side, silently recreating the
exact overflow problem this feature exists to solve.

Worse, `FitCameraToBoard` (`BoardView3D.cs:206-231`) computes
`maxLayer = slotsById.Values.Max(s => s.Layer)` **globally across every slot** and
uses it to pad the camera's fitted bounding box (`offX`/`offY`,
`BoardView3D.cs:216-220`). An unmodified deep stack would inflate `maxLayer` to 60+,
wildly overshooting the camera zoom for the whole board.

**Resolution:** stack tiles are excluded from both calculations. See §5.3.

## 4. Architecture

```
LevelCatalog.GenerateProcedural(levelId)
  Difficulty ramps 1->5 exactly as today, clamps at 5 (~level 38)   (unchanged)
  StackDepth [NEW] — 0 while Difficulty < 5; once Difficulty == 5, every column
                      (all 8, top and bottom) grows by 1 tile every
                      StackGrowthEveryLevels levels past the plateau, unbounded
        │
        ▼
GameController.PrepareLevel
  BoardShapeBuilder.Compose(                                          [NEW]
      pyramid: TurtleShapeBuilder.BuildForDifficulty(difficulty),   (unchanged call)
      topStacks: StackColumnBuilder.Build(columns: 4, depth, Region.TopStack),
      bottomStacks: StackColumnBuilder.Build(columns: 4, depth, Region.BottomStack))
        │
        ▼
  BoardGenerator.GenerateShaped(...)        (unchanged — operates on the
  HiddenTileSelector.Apply(...)              combined slot graph generically)
        │
        ▼
BoardView3D.Build / FitCameraToBoard / PlaceTileView                  [MODIFIED]
  - stack-region slots skip LayerRenderOffset, get a small fixed pile nudge instead
  - maxLayer for camera-fit offX/offY is computed over Pyramid-region slots only
  - only the top VisibleStackDepth tiles per column get a live TileView3D;
    deeper ones are instantiated lazily as the pile advances (see §8)
  - each stack column gets a "+N" count badge for tiles beyond the visible window
```

- **`StackColumnBuilder`** (new, `GameDomain.Generation`) — sibling to
  `TurtleShapeBuilder`, builds one vertical LIFO column's `TileSlot`s.
- **`BoardShapeBuilder`** (new, `GameDomain.Generation`) — merges pyramid +
  top-stack + bottom-stack slot lists into one, fixing up parity.
- **No changes to `BoardGenerator`, `ReverseConstructionSolver`,
  `BranchingOrderBuilder`, `FreedomRuleCalculator`, `DifficultyProfile`,
  `HiddenTileSelector`, or `TileReveal`** — all already operate generically over
  whatever slot graph they're given (confirmed by reading each; see §4 note below
  and the existing hidden-tile-reveal design's identical finding for
  `HiddenTileSelector`/`TileReveal`).

## 5. Component Detail

### 5.1 `TileSlot` (Model) — one new field

```csharp
public sealed class TileSlot
{
    public string Id;
    public int X;
    public int Y;
    public int Layer;
    public List<string> CoveredByIds = new List<string>();
    public string LeftNeighborId;
    public string RightNeighborId;
    public SlotRegion Region = SlotRegion.Pyramid;   // NEW
}

public enum SlotRegion { Pyramid, TopStack, BottomStack }   // NEW
```

`Region` is read only by the presentation layer (§5.3) and by
`BoardShapeBuilder.Compose` for parity trimming (§5.2). Domain generation/solving
code never inspects it — freedom and covering are still purely graph-driven.

### 5.2 `StackColumnBuilder` / `BoardShapeBuilder`

```csharp
namespace GameDomain.Generation
{
    public static class StackColumnBuilder
    {
        // One vertical LIFO column of `depth` tiles at a fixed (x, y) position.
        // Layer 0 = bottom of the pile (placed first, covered by everything
        // above it); Layer depth-1 = top (free immediately). CoveredByIds points
        // only to the tile directly above in the same column; neighbors stay
        // null so FreedomRuleCalculator's side rule never blocks a pile.
        public static List<TileSlot> Build(int x, int y, int depth, SlotRegion region, string columnId)
        {
            var slots = new List<TileSlot>();
            for (int l = 0; l < depth; l++)
            {
                slots.Add(new TileSlot
                {
                    Id = columnId + "_" + l,
                    X = x, Y = y, Layer = l,
                    Region = region,
                    CoveredByIds = new List<string>()
                });
            }
            for (int l = 0; l < depth - 1; l++)
                slots[l].CoveredByIds.Add(slots[l + 1].Id);   // covered by the one above it
            return slots;
        }
    }

    public static class BoardShapeBuilder
    {
        // Merges pyramid + N top/bottom stack columns into one shape. Stack
        // columns are placed at fixed X positions spanning the pyramid's own
        // width (computed from its slots, not hardcoded) with a fixed Y gap
        // above/below its bounding box - so stacks never collide with the
        // pyramid and the gap never changes regardless of stack depth.
        public static List<TileSlot> Compose(
            List<TileSlot> pyramid, List<List<TileSlot>> topColumns, List<List<TileSlot>> bottomColumns)
        {
            var all = new List<TileSlot>(pyramid);
            foreach (var col in topColumns) all.AddRange(col);
            foreach (var col in bottomColumns) all.AddRange(col);

            // Defensive fallback only: under the uniform-depth growth scheme in
            // §5.4, pyramid (54, even) + 8 columns * equal depth is always even,
            // so this should never actually trigger. Kept as a safety net (same
            // top-down-trim pattern TurtleShapeBuilder.Build already uses for its
            // own parity fix) in case columns are ever made uneven later.
            if (all.Count % 2 == 1)
            {
                var deepest = topColumns.Concat(bottomColumns)
                    .Where(c => c.Count > 0)
                    .OrderByDescending(c => c.Count).First();
                all.Remove(deepest[deepest.Count - 1]);
            }
            return all;
        }
    }
}
```

Column X positions: evenly spaced across
`[pyramid.Min(X), pyramid.Max(X)]`. Column Y positions: a fixed gap constant
above `pyramid.Max(Y)` (top row) / below `pyramid.Min(Y)` (bottom row) — computed
from the pyramid's own bounding box each time, never hardcoded, so it stays
correct if `TurtleShapeBuilder`'s tuning ever changes.

### 5.3 `BoardView3D` — three required changes

1. **`PlaceTileView`** (`BoardView3D.cs:318-328`): for `slot.Region != Pyramid`,
   skip `LayerRenderOffset` entirely and instead apply a small fixed nudge (e.g. a
   couple millimeters per visible layer, capped at `VisibleStackDepth` layers) so
   the pile reads as a physical stack without ever growing past that cap.
2. **`FitCameraToBoard`** (`BoardView3D.cs:206-231`): change
   `int maxLayer = slotsById.Values.Max(s => s.Layer)` to
   `slotsById.Values.Where(s => s.Region == SlotRegion.Pyramid).Max(s => s.Layer)`
   — only pyramid tiles receive the layer-proportional drift that `offX`/`offY`
   compensates for, so only they should influence it. `minX/maxX/minY/maxY`
   already bound correctly with no change, since every tile in a stack column
   shares the same fixed X/Y regardless of depth.
3. **`Build`**: for stack-region slots, only instantiate a live `TileView3D` for
   the top `VisibleStackDepth` (default 3) tiles per column; render a `+N` count
   badge (new small UI prefab) for the rest. As the pile's top tile clears, the
   next tile within the visible window gets instantiated (see §8 for whether this
   needs to be lazy from the start or can be eager initially).

### 5.4 `LevelCatalog.GenerateProcedural` — new `StackDepth` field

```csharp
public class LevelData
{
    // ...existing fields...
    public int StackDepthPerColumn = 0;   // NEW - tiles per reserve-stack column (0 = no stacks)
}

private const int StackColumns = 4;              // per row (4 top + 4 bottom = 8 total)
private const int StackGrowthEveryLevels = 4;     // add 1 pair (2 tiles total) this often
private const int VisibleStackDepth = 3;          // rendered card meshes per pile (presentation)

private static LevelData GenerateProcedural(int levelId, int lastAuthoredId)
{
    int stepsIn = (levelId - lastAuthoredId - 1) / RampLevelsPerDifficultyStep;
    int difficulty = Math.Min(5, 1 + stepsIn);

    int stackDepth = 0;
    if (difficulty == 5)
    {
        // plateauLevelId is the first level where difficulty reaches 5 - derived
        // from the ramp constants, not hardcoded, so it stays correct if those change.
        int plateauLevelId = lastAuthoredId + 1 + RampLevelsPerDifficultyStep * 4;
        int levelsPastPlateau = Math.Max(0, levelId - plateauLevelId);
        // Every column (all 8, top+bottom) grows by 1 tile per step - uniform
        // depth across all columns always, so there's never an imbalance to
        // distribute and the total (8 * stackDepth) is always even for Pair mode.
        stackDepth = levelsPastPlateau / StackGrowthEveryLevels;
    }

    int parAids = Math.Max(1, 4 - difficulty);
    return new LevelData
    {
        LevelId = levelId, Name = "Level " + levelId,
        Difficulty = difficulty, ParAids = parAids,
        StackDepthPerColumn = stackDepth,
        Mode = MatchMode.Pair,
    };
}
```

The exact round-robin bookkeeping (which specific columns get the +1 first when
total growth isn't evenly divisible by 8) is an implementation-time detail with no
design-level consequence — any deterministic, seed-independent distribution that
keeps columns within ±1 tile of each other works.

### 5.5 `GameController.PrepareLevel` — one changed call

```csharp
var levelData = LevelCatalog.Get(_currentLevelId) ?? LevelCatalog.Levels[0];
int difficulty = levelData.Difficulty;

var pyramid = TurtleShapeBuilder.BuildForDifficulty(difficulty);   // unchanged
if (levelData.StackDepthPerColumn > 0)                              // NEW branch
{
    var topCols = BuildStackColumns(pyramid, StackRegion.TopStack, levelData.StackDepthPerColumn);
    var bottomCols = BuildStackColumns(pyramid, StackRegion.BottomStack, levelData.StackDepthPerColumn);
    _shape = BoardShapeBuilder.Compose(pyramid, topCols, bottomCols);
}
else
{
    _shape = pyramid;   // levels 1-38: byte-for-byte identical to today
}
```

Everything after this line (`BoardGenerator.GenerateShaped`,
`HiddenTileSelector.Apply`, tray/hint/undo wiring) is **completely unchanged** —
they already consume `_shape`/`_board` generically.

## 6. Data Flow & Error Handling

1. `StackDepthPerColumn == 0` for every level up to the difficulty-5 plateau —
   `_shape` is exactly `TurtleShapeBuilder.BuildForDifficulty(difficulty)`, so
   levels 1-38 cannot regress; this is directly testable (§7).
2. `BoardShapeBuilder.Compose` cannot fail — it's pure list concatenation plus an
   optional single-tile trim, always produces a valid even-count shape.
3. `BoardGenerator.GenerateShaped`'s existing `maxRestarts` retry/fallback logic
   (`BoardGenerator.cs:90-124`) is untouched and applies to the composed shape
   exactly as it does to the pyramid today — no new failure mode, though very deep
   stacks are a performance question (see §8, not a correctness one: a stack
   column's freedom is trivial and can never cause `BranchingOrderBuilder`/
   `ReverseConstructionSolver` to return null).
4. `HiddenTileSelector.Apply` and `TileReveal.TryReveal` need **zero code
   changes** — both already operate over `board.Cells`/slot freedom generically
   (confirmed by reading both; §3.2 of the hidden-tile-reveal design already
   established this same genericity for the pyramid case, and nothing about a
   stack tile is special to either function).

## 7. Testing Plan

New EditMode tests in `Domain.Tests`, following the existing `Generation`/
`Progression` test-folder patterns:

1. **`StackColumnBuilderTests`** — a depth-N column produces N slots; only the
   top slot (`Layer = N-1`) has empty `CoveredByIds`; every other slot's
   `CoveredByIds` contains exactly the slot above it; `LeftNeighborId`/
   `RightNeighborId` are `null` on every slot.
2. **`BoardShapeBuilderTests`** — composing a pyramid with 0-depth stacks returns
   the pyramid unchanged; composing with non-zero stacks returns pyramid ∪ all
   stack tiles with no ID collisions; odd totals get trimmed to even by removing
   exactly one tile from the deepest column, never from the pyramid.
3. **`ProgressionTests`** (extend existing file) — for every level 1 through the
   difficulty-5 plateau level, `LevelCatalog.Get(id).StackDepthPerColumn == 0` and
   the resulting `_shape` matches `TurtleShapeBuilder.BuildForDifficulty(difficulty)`
   exactly (regression guard: levels 1-38 must never change).
4. **`SolvabilityRegressionTests`** (extend existing file) — add a case at a large
   synthetic `StackDepthPerColumn` (e.g. 60) confirming `BoardGenerator.GenerateShaped`
   still produces a solvable board within `maxRestarts` — proves the generic solver
   genuinely handles deep stacks, not just small ones.
5. **Freedom-rule interaction** — a hand-built tiny board (pyramid + one 3-deep
   stack) confirms only the stack's top tile is ever free until it's cleared, then
   the next one becomes free, matching `FreedomRuleCalculator.IsFree`'s existing
   behavior with no special-casing.

No PlayMode/presentation tests, consistent with the rest of this project — the
camera-fit fix (§5.3) and pile-badge rendering get verified visually (Editor
screenshot + on-device build) at implementation time.

## 8. Open Items to Resolve During Implementation

- **Stack tile view instantiation strategy:** `BoardView3D.Build` today
  instantiates a `TileView3D` for every uncleared cell unconditionally
  (`BoardView3D.cs:116-192`). §5.3 proposes capping *live* views per stack at
  `VisibleStackDepth` and lazily instantiating deeper ones as the pile advances.
  Whether this laziness is needed from day one, or whether eagerly instantiating
  every stack tile (all occluded behind the visible top few, since they share X/Y
  and differ only in Z) is cheap enough on target devices, should be settled by
  profiling at a deep test level (e.g. `StackDepthPerColumn = 60`) rather than
  guessed up front.
- **`VisibleStackDepth` (3) and `StackGrowthEveryLevels` (4) are starting
  defaults** — tune after playtesting difficulty-5-plus levels on-device, same as
  every other tuning constant in this project (`Fraction = 0.35f` in
  `HiddenTileSelector`, `RampLevelsPerDifficultyStep = 8` in `LevelCatalog`, etc.).
- **Pile count badge art/prefab** — needs a small TMP-based badge UI, placeholder
  first (plain text over the top card), styled later — same open-item treatment
  the hidden-tile-reveal design gave its card-back art.
- **Branching/difficulty-shaping interaction at very high stack fractions:**
  `BranchingOrderBuilder`'s opening-branching profile (`DifficultyProfile.cs`)
  was tuned against pure-pyramid boards. A stack column can only ever expose one
  newly-free tile at a time (its next-down tile), unlike pyramid tiles which can
  expose several at once. At extreme levels where stacks form the bulk of the
  tile count, the realized branching curve will trend lower than
  `OpeningBranchingMin` targets even when `DifficultyProfile.Accepts` still passes
  via the existing fallback path (`BoardGenerator.cs:106-124`). This is expected —
  more piles, less simultaneous choice, is itself a harder-feeling board — but
  should be reviewed against actual playtesting rather than assumed correct.

## 9. Summary

The pyramid is frozen forever at its current 54-tile maximum. A fixed 4-top +
4-bottom row of reserve stacks absorbs all further growth via depth alone, using
the exact same abstract covering-graph model (`CoveredByIds` + null neighbors)
that already makes `FreedomRuleCalculator`, `BoardGenerator`, and every solver
component work — none of that code changes. The real work is: a new
`StackColumnBuilder`/`BoardShapeBuilder` pair (domain), a new `StackDepthPerColumn`
progression knob (`LevelCatalog`), and three targeted `BoardView3D` changes to
keep the visual/camera footprint genuinely constant regardless of true stack
depth (§5.3) — without that last piece, stacks would silently recreate the exact
screen-overflow bug they're meant to fix.
