# Core Gameplay Foundation — Design Spec

Date: 2026-08-25
Status: Approved for planning

## Context

This project is a senior-friendly, mahjong-style tile-matching puzzle game
(in the spirit of Vita Mahjong): classic layered "turtle" board, hint/undo/
shuffle power-ups, daily challenges, ad + IAP monetization, accessibility-
first design for an older-adult audience.

The full product (engine setup through live-ops) is too large for a single
spec. It has been decomposed into sequential sub-projects:

1. **Core Gameplay Foundation** (this spec)
2. Meta & Progression — level map, save/load, daily challenge, currency
3. Content Pipeline — tile sets, audio, first level batch
4. Monetization — ad mediation, IAP
5. Backend Services — analytics, crash reporting, remote config
6. Polish / QA / Store Launch
7. Post-launch Live-Ops (ongoing)

Sub-project 1 is the foundation every later piece depends on: it proves the
board generator is reliably solvable, the core interaction loop works, and
the accessibility constraints are baked in from the start rather than
retrofitted.

## Decisions

- **Engine:** Unity (C#).
- **Board layout:** classic layered "turtle" Mahjong Solitaire (tiles
  stacked across z-layers; a tile is free only if nothing sits above it and
  at least one horizontal side is open). Flat-grid ("Onet"-style) layout is
  out of scope for this sub-project.
- **Architecture:** the gameplay/algorithm code is a plain C# domain layer
  with zero `UnityEngine` references, isolated in its own assembly
  definition. Unity MonoBehaviours form a thin presentation layer that
  renders `BoardState` and forwards input — they never contain gameplay
  rules.
- **Platform:** Android only for this sub-project (matches the target
  audience's tablet-heavy usage). iOS build setup is deferred.
- **UI polish:** accessibility-first tokens (tap targets, contrast, type
  scale, icon+color coding) are built now, not deferred to a later polish
  pass — this constraint shapes every UI decision from the start.

## Scope

**In scope:**
- Unity project setup targeting Android.
- Data structures for layered board shapes (`TileSlot`, `LevelDefinition`,
  `BoardState`, `Move`).
- Reverse-construction solvable board generator + freedom-rule calculator
  for the layered turtle rule.
- Match validation, tile state machine (idle → selected →
  matched-clearing → cleared), clear animation.
- Hint, Undo, Shuffle power-ups (shuffle re-runs generation over the
  currently-remaining cells only, preserving tile positions).
- Combo/score system (in-memory only; no persistence in this sub-project).
- Accessibility-first tile/HUD styling: ≥88dp tap targets, WCAG AA
  (4.5:1) contrast, 20pt+ body text, icon+color tile coding (never
  color-alone).
- 3–5 hand-authored test layout shapes (small / medium / large tile
  counts) to exercise the generator across different topologies.
- Automated EditMode tests, including a solvability regression test that
  generates N boards per shape and verifies each via an independent
  backtracking solver.
- One playable Android device/emulator build exercising the full loop.

**Out of scope (deferred to later sub-projects):**
- Save/load and persistence of any kind.
- Level map / progression, daily challenge, currency/economy.
- Ads, IAP, analytics, crash reporting, remote config.
- Final art and audio content (placeholder tile icons/colors only).
- Localization.
- iOS build.
- Flat-grid ("Onet"-style) board layout.

## Project Structure

```
/Assets
  /Scripts
    /Domain              -- plain C# only, ZERO UnityEngine references
      /Model              -- TileSlot, LevelDefinition, BoardState, Move
      /Generation          -- BoardGenerator (reverse-construction), FreedomRuleCalculator
      /Gameplay           -- MatchValidator, HintFinder, UndoStack, ShuffleService, ComboScorer
    /Presentation          -- MonoBehaviours, references UnityEngine + Domain (one-way)
      /Board              -- BoardView, TileView, TileInputController
      /HUD                -- ScoreDisplay, HintButton, UndoButton, ShuffleButton
      /Effects            -- ClearAnimation, ComboEffect
    /Data                 -- ScriptableObjects: LevelShapeAsset, TileSetAsset, AccessibilityTokens
  /Scenes
    Game.scene
  /Tests
    /EditMode
      BoardGeneratorTests.cs
      FreedomRuleTests.cs
      MatchValidatorTests.cs
      SolvabilityRegressionTests.cs
      HintUndoShuffleTests.cs
  /Prefabs
    Tile.prefab, ComboEffectFX.prefab
```

`Domain` is its own `.asmdef` with no Unity engine reference, so EditMode
tests run fast and the algorithm stays portable. `Presentation` depends on
`Domain`; `Domain` never depends on `Presentation`.

## Data Model

```csharp
// Domain/Model
public sealed class TileSlot {
    public string Id;
    public int X, Y, Layer;
    public List<string> CoveredByIds;   // slots stacked directly above this one
    public string LeftNeighborId, RightNeighborId;
}

public sealed class LevelDefinition {
    public int LevelId;
    public IReadOnlyList<TileSlot> Shape;
    public string TileSetId;
}

public sealed class TileCell {
    public string Value;    // null if this slot holds no tile
    public bool Cleared;
}

public sealed class Move {
    public string SlotIdA, SlotIdB;
    public string ValueA, ValueB;
}

public sealed class BoardState {
    public int LevelId;
    public Dictionary<string, TileCell> Cells;   // slotId -> cell
    public List<Move> MoveHistory;                // for undo
    public int Score;
    public int ComboCount;
}
```

## Freedom Rule (layered turtle)

A slot is **free** iff:
1. None of its `CoveredByIds` are still uncleared, AND
2. `LeftNeighborId` is null/cleared, OR `RightNeighborId` is null/cleared.

`FreedomRuleCalculator` evaluates this against a given set of
still-uncleared slot ids — it never assumes the full board is present, which
is what makes it reusable for both generation-time simulation and
mid-game shuffle.

## Board Generator (reverse construction)

1. Simulate a full valid removal order over the shape's topology alone (no
   tile values yet), repeatedly computing free slots via
   `FreedomRuleCalculator` against a shrinking `remainingSlots` set and
   removing a random free pair each step.
2. If at any step fewer than 2 free slots remain, restart generation for
   that shape. Log restarts — frequent restarts indicate an unbalanced
   shape and is a QA signal, not just a runtime retry mechanism.
3. Once a full removal order exists, assign paired tile values by walking
   the removal order **in reverse**, popping values from a shuffled pool
   (each value appears in groups of 4, matching physical tile sets).
4. Replaying the removal order forward against the resulting board is by
   construction always a legal solve path.

**Shuffle power-up:** treat the currently-uncleared cells as a fresh shape
(same topology, cleared slots excluded), rerun steps 1–3 against just that
subset, keep tile *positions* fixed, and reassign values only.

## Gameplay Services (Domain/Gameplay)

- `MatchValidator.TryMatch(BoardState, slotIdA, slotIdB)` — checks value
  equality and that both slots are currently free.
- `HintFinder.FindFreePair(BoardState)` — returns the first valid free
  matching pair, or null (should not occur on a generator-produced board;
  the check stays defensive rather than assumed).
- `UndoStack` — pushes/pops `Move` records and replays them back onto
  `BoardState`.
- `ShuffleService` — performs the reduced re-generation described above.
- `ComboScorer` — awards base points per match; consecutive matches within
  an injected elapsed-time window multiply score. Combo resets on idle
  timeout, not on a mismatched tap (mismatches never penalize the player,
  consistent with the low-pressure design goal).

## Presentation Layer

- `BoardView` owns a `BoardState`, spawns one `TileView` per uncleared
  cell from `Tile.prefab`, and re-evaluates each tile's free/blocked
  visual state after every mutation.
- `TileInputController` captures taps and forwards slot-id pairs to a thin
  `GameController` MonoBehaviour, which calls `MatchValidator` and also
  drives the Hint/Undo/Shuffle buttons and clear/combo animations.
- Mismatched taps trigger a gentle shake tween — no score penalty, no
  combo break.

## Accessibility UI

- `AccessibilityTokens` ScriptableObject centralizes: minimum tap target
  (88dp default), body text size (20pt minimum), pre-checked contrast
  pairs (≥4.5:1), and the tile icon set.
- `TileView` always renders a tile's value as **icon + color**, never
  color alone.
- HUD buttons (Hint/Undo/Shuffle) pull size and contrast from the same
  token asset rather than being hardcoded per screen, so later screens
  stay consistent by construction.

## Testing Strategy

- `Tests/EditMode` covers: board generator, freedom rule, match validator,
  hint/undo/shuffle, and combo scoring — all pure C#, runnable without
  opening a scene or a device.
- `SolvabilityRegressionTests` generates 200 boards per test layout shape
  and verifies each with an independent backtracking solver
  (operating on tile values, not the generator's own construction
  guarantee) — a second, independent check against the algorithm.
- A single manual pass on an Android device/emulator validates input
  responsiveness, animation feel, and tap-target sizing — this is
  exploratory, not automated.

## Open Items for Later Sub-Projects

- Flat-grid board layout (if ever added) will need its own freedom-rule
  implementation behind the same generator interface.
- Persistence, level map, and daily-challenge seeding are intentionally
  absent here and belong to Sub-project 2.
