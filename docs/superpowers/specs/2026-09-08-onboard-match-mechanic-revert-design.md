# Sub-project #1 — Core Mechanic Revert: On-Board Pair Matching

## Status
Design approved in chat 2026-09-08. First sub-project of the "Premium Mahjong
Solitaire (feature/tech parity, original art)" master plan, which supersedes the
prior tray-collection direction.

## Context

The master plan targets **classic mahjong solitaire**: tap one free tile to
select it, tap a second matching free tile to clear the pair directly on the
board. The current build instead uses a **tray-collection** mechanic (tap a free
tile → it flies into a 4-slot tray → two identical tiles in the tray clear).
This sub-project reverts the live gameplay path back to on-board pair matching.

Crucially, the **domain layer is already pair-match-ready** — it was built for
on-board matching before the tray pivot and was never deleted, only orphaned:

- `MatchValidator.TryMatch(board, slotsById, slotIdA, slotIdB)` — validates two
  slots are distinct, uncleared, equal-valued, and both free; clears them and
  records a `Move`. Untouched, correct.
- `FreedomRuleCalculator.IsFree` — covered-above + one-side-open rule.
- `HintFinder.FindFreePair` — returns a free matching pair, or `null` when stuck.
- `UndoStack.TryUndo` — reverts the last `Move` (un-clears both slots), consumes
  an undo.
- `ShuffleService.Shuffle` — reshuffles remaining tiles into a still-solvable
  layout, consumes a shuffle, reserves values that could return via Undo.
- `ComboScorer.RegisterMatch` — combo-window scoring against `BoardState.Score`
  / `ComboCount`.
- `BoardGenerator.Generate` — solvable pair board (each value exactly twice).

So this is a **`GameController` / presentation rewire plus small additive
domain fields**, not a domain rewrite. The existing pair-oriented EditMode tests
(`MatchValidatorTests`, `HintFinderTests`, `UndoStackTests`,
`ShuffleServiceTests`, `BoardGeneratorTests`) should pass unchanged — that is the
regression signal confirming this is a rewire.

## Scope

In scope:
1. Rewire `GameController` from tray-push to select-then-match.
2. Un-stub hint / undo / shuffle in `GameController`.
3. Add a limited-moves budget with a real lose condition.
4. Add the two `BoardView3D` view-sync methods that Undo/Shuffle need.
5. Stop building the tray HUD row in `GameSceneBuilder3D`.

Out of scope (later sub-projects):
- Visual re-theme to jade/gold (#2).
- The combo *meter* UI — a fill bar, distinct from the old tile tray (#3).
- Level select, star rating, save/load, currency, settings (#4).
- Daily challenge (#5), monetization (#6), real level authoring (#7),
  performance/QA pass (#8).

## Decisions (confirmed with user)

- **Tray code fate:** keep `TrayManager`, `TrayView3D`, `TraySlotView3D` in the
  codebase but **unreferenced/dormant** (same treatment the earlier triple-match
  code got). Do not delete. The live match path must not call any of them.
- **Moves budget:** build the **full limited-moves lose condition now**, not just
  a display. Add `MovesBudget` to `LevelDefinition` and `MovesRemaining` to
  `BoardState`.

## Design

### Domain additions (additive, non-breaking)

`LevelDefinition`:
```
public int MovesBudget = -1; // <= 0 means unlimited
```

`BoardState`:
```
public int MovesRemaining; // set from LevelDefinition.MovesBudget at generation;
                           // -1/0 semantics: <= 0 at generation => unlimited (not enforced)
```

- `BoardGenerator.Generate` sets `board.MovesRemaining = level.MovesBudget`.
- A move is consumed on a **successful match only** (mismatched second-tap
  reselect does not cost a move — standard solitaire convention).
- `MovesRemaining` is decremented in the domain-adjacent match flow (in
  `GameController` right after a successful `MatchValidator.TryMatch`, so the
  domain match function stays a pure pair operation and existing tests that call
  `TryMatch` directly are unaffected).

### GameController rewire

State: replace tray bookkeeping with a single nullable selection:
```
private string _selectedSlotId; // null = nothing selected
```

`OnTileTapped(slotId)`:
1. Guard: `IsInputLocked` / `IsGameOver` / all-cleared → return.
2. Resolve `TileView3D`. If the tapped tile is **not free**
   (`FreedomRuleCalculator.IsFree` against uncleared remaining) → `PlayShake()`,
   return. (Blocked tiles never select.)
3. If nothing selected → select it (`SetSelected(true)`, store id), return.
4. If the tapped tile **is** the selected one → deselect, return.
5. Otherwise a different free tile is tapped while one is selected → attempt
   `MatchValidator.TryMatch(_board, _slotsById, _selectedSlotId, slotId)`:
   - **Match:** clear selection; `ComboScorer.RegisterMatch`;
     `MatchCelebrationController.PlayMatchCelebration` at the match position;
     `PlayClearAndDestroy()` on both tiles; `MovesRemaining--`;
     `RefreshFreeStates`; fire `ScoreChanged`; `EvaluateEndState`.
   - **No match** (values differ; both are free): deselect the old tile, select
     the newly tapped one (carry selection forward — not an error).

Input locking: keep the existing `IsInputLocked` discipline around the deal-in
and the clear animation so a second tap can't land mid-animation.

`OnHintRequested`:
- Gate on `HintsRemaining > 0`. Call `HintFinder.FindFreePair`; if a pair is
  returned, `Highlight()` both tiles and decrement `HintsRemaining` via the
  existing counter, then `NotifyUsesChanged()`. If `null` (stuck), do nothing
  (or a soft feedback) and do not consume a hint.

`OnUndoRequested`:
- Gate on `UndosRemaining > 0` and non-empty `MoveHistory`. Read the last `Move`
  before calling `UndoStack.TryUndo`; on success call
  `BoardView3D.RestoreTiles([SlotIdA, SlotIdB], board)` to re-materialize the two
  tiles, clear any selection, `RefreshFreeStates`, fire `ScoreChanged` /
  `NotifyUsesChanged`. (Undo does **not** refund a spent move — or does; pick one
  and state it: **decision: Undo restores the pair but does not refund the
  move**, so moves-budget pressure is preserved. Revisit if playtesting says
  otherwise.)

`OnShuffleRequested`:
- Gate on `ShufflesRemaining > 0`. Call `ShuffleService.Shuffle(_board, _shape,
  random)`; on success call `BoardView3D.RefreshTileValues(remainingIds, board)`
  to update tile faces to the new values, clear selection, `RefreshFreeStates`,
  `NotifyUsesChanged`.

### BoardView3D additions

Both reuse the tile-instantiation and material/face logic already in `Build()`:

- `RestoreTiles(IEnumerable<string> slotIds, BoardState board)` — re-instantiate
  views for slots that Undo un-cleared, placing them at their original layer /
  position and applying their (possibly value-changed) face.
- `RefreshTileValues(IEnumerable<string> slotIds, BoardState board)` — update the
  face/food-model of existing tile views to match `board.Cells[id].Value` after a
  Shuffle, without destroying/re-creating the GameObjects where avoidable.

### End states (`EvaluateEndState`)

- **Win:** every cell `Cleared` → `GameOverPopup3D.ShowWin(this, Score)`.
- **Lose:** either
  - `MovesRemaining` is a real budget (`> 0` at generation) and has reached `0`
    with tiles still on the board; **or**
  - `HintFinder.FindFreePair` returns `null` (no legal move) **and**
    `ShufflesRemaining == 0` (can't shuffle out of it).
  → `GameOverPopup3D.ShowLose(this)`.

  If stuck but shuffles remain, the player is **not** lost — they must spend a
  shuffle. This matches the master-plan wording "no valid moves remain and no
  moves/hints/shuffles left."

### GameSceneBuilder3D

Stop building the 4-slot tray HUD row. The vacated screen band is left empty
until the visual re-theme (#2) claims it. No other HUD element moves in this
sub-project (spacing/re-theme is #2's job).

## Testing

New EditMode tests:
- `MovesBudget` reaches 0 with tiles remaining → lose flagged; unlimited budget
  (`<= 0`) never triggers the moves lose.
- Stuck board (`FindFreePair == null`) with `ShufflesRemaining > 0` → **not**
  lost; with `ShufflesRemaining == 0` → lost.
- A successful match decrements `MovesRemaining`; a mismatched reselect does not.

Regression (must pass unchanged): `MatchValidatorTests`, `HintFinderTests`,
`UndoStackTests`, `ShuffleServiceTests`, `BoardGeneratorTests`,
`FreedomRuleCalculatorTests`, `SolvabilityRegressionTests`.

Manual on-device (Samsung RZ8M70HFTCM, `com.gameclient.mahjong`, via
`AndroidBuilder.RegenerateAndBuild` in a fresh batchmode process):
- Tap-select highlights a free tile; tap-blocked shakes, never selects.
- Tap-match clears a valid pair with celebration; mismatch carries selection.
- Hint highlights a real pair and consumes a charge; undo restores the last pair;
  shuffle re-lays a still-solvable board.
- Win on full clear; lose on moves exhausted; lose on stuck-with-no-shuffles.

## Boundaries

Original art/code only. This sub-project touches no reference-app assets. It
reverts a mechanic and wires already-present domain logic; it does not restyle
anything (that is sub-project #2).
