# Hidden (Face-Down) Tiles — Design

**Date:** 2026-09-23
**Status:** Design approved by owner in chat; spec under review
**Scope:** On difficulty-4+ boards, a portion of tiles start face-down. Tapping a
free face-down tile reveals it (peek) instead of collecting it; tapping it again
collects it as normal. Revealing a *different* face-down tile while one is already
peeked flips the previous one back down. Undo reverses a collected hidden tile back
to face-down. Hints may target face-down tiles without revealing them.

---

## 1. Background & Problem

The game is a tray-collection mahjong-solitaire (see `2026-09-10-difficulty-shaping-design.md`
§1 for the full mechanic). Today every free tile shows its value face-up at all times —
tapping it always attempts to collect it via `TrayManager.TryPushToTray`.

**Goal:** add a memory/concealment dimension to the two hardest difficulty tiers
(4-5) so the highest levels are harder in a new way (recall/attention), not just
bigger. A portion of tiles start face-down; the player must reveal a tile's value
before deciding whether to collect it, and can only keep one "peeked" reveal active
at a time — a classic concentration-game constraint layered on top of the existing
tray mechanic.

## 2. Non-Goals (YAGNI)

- Not changing solvability, tray-safety, `TrayManager`, `FreedomRuleCalculator`, or
  `BoardGenerator`/`ValueAssigner`/`BranchingSimulator` at all. Which tiles are
  *geometrically* free is completely untouched; this feature only gates what a tap
  on an already-free tile *does*.
- Not changing which tiles are chosen difficulty-wise beyond a flat random
  selection — no layer bias, no "avoid hiding both tiles of a pair" logic. Simplest
  version first; revisit only if playtesting shows a problem.
- Not applying below difficulty 4. Difficulties 1-3 are untouched.
- Not adding a "peek timer" (auto-hide after N seconds) — only tapping another
  face-down tile forces a re-hide, per the approved design.

## 3. Key Concepts

### 3.1 Hidden vs. Revealed vs. Peeked

Three distinct states, two of which are tracked per-tile and one board-wide:

- **`IsHiddenTile`** (permanent, set once at generation) — this tile belongs to the
  face-down subset. Never changes after generation.
- **`Revealed`** (mutable) — whether the tile is *currently* showing its front face.
  Non-hidden tiles are always `Revealed = true`. Hidden tiles start `false` and flip
  between `true`/`false` as described below.
- **`BoardState.PeekedTileId`** (mutable, board-wide, nullable) — the single hidden
  tile (if any) that is currently `Revealed` but not yet collected. At most one at a
  time; this is what makes revealing a second hidden tile re-hide the first.

### 3.2 The tap rule

For a tap on a free tile:

1. **Not `Revealed`** → this is a *reveal* tap, not a collect tap. If a *different*
   tile is currently `PeekedTileId`, it flips back to face-down. This tile flips to
   face-up and becomes the new `PeekedTileId`. **No tray interaction happens.**
2. **Already `Revealed`** (was never hidden, or is the currently-peeked tile tapped
   again) → today's unchanged flow: `TrayManager.TryPushToTray`. If this was the
   peeked tile, `PeekedTileId` clears once it leaves the board.

Tapping any already-face-up tile (normal or peeked) never disturbs *other*
face-down tiles — only revealing a *new* face-down tile forces the previous peek to
re-hide. (Confirmed with the design owner.)

## 4. Architecture

New pieces, all in `GameDomain` (engine-free, matching the existing `Domain`
assembly split) except the presentation layer:

```
GameController.PrepareLevel
  BoardGenerator.GenerateShaped(...)            (unchanged)
        │
        ▼
  HiddenTileSelector.Apply(board, difficulty, rng)   [NEW] — marks ~30-40% IsHiddenTile
        │                                              on difficulty >= 4, no-op otherwise
        ▼
  BoardState (some cells IsHiddenTile=true, Revealed=false)

GameController.OnTileTapped(slotId)
        │
        ├─ cell.Revealed == false ──► TileReveal.TryReveal(board, slotsById, slotId, out reHiddenId)   [NEW]
        │                               │ true  → play flip animations, return (no tray push)
        │                               │ false → not free, same shake/invalid-tap feedback as today
        │
        └─ cell.Revealed == true  ──► TrayManager.TryPushToTray(...)   (unchanged)
                                         on success, if slotId == PeekedTileId → clear it

GameController.AnimateUndo (TrayUndo.TryUndo path)
        │
        └─ if board.Cells[popped].IsHiddenTile → Revealed = false   [NEW, one line]
```

- **`HiddenTileSelector`** (new, `GameDomain.Gameplay`) — pure, seeded-random
  selection. Same tiny-dedicated-class pattern as `TrayManager`/`TrayUndo`/
  `TrayShuffle`/`TrayHintFinder` already in that namespace.
- **`TileReveal`** (new, `GameDomain.Gameplay`) — owns the reveal/peek state
  machine. Kept separate from `TrayManager` because it's a genuinely different
  concern (visibility gating vs. match/tray logic) and needs to be unit-testable
  independent of tray state — mirroring why `BranchingSimulator` was split out in
  the difficulty-shaping design.
- **No changes to `TrayManager`, `FreedomRuleCalculator`, `TrayHintFinder`,
  `BoardGenerator`, or any generation/solvability code.**

**Why `TileReveal` is a separate Domain function and not inline in
`GameController`:** `GameController` is a `MonoBehaviour` in the implicit
`Assembly-CSharp` (no asmdef), which the `Domain.Tests` assembly cannot reference
(Unity compiles the default assembly last, so nothing can depend on it — confirmed
directly during the tray-matching bug fix earlier this project). Any state-machine
logic embedded directly in `GameController.OnTileTapped` would be permanently
untestable. Putting it in `GameDomain.Gameplay` instead keeps it in the
already-tested, engine-free `Domain` assembly, consistent with every other piece of
gameplay logic in this codebase.

## 5. Component Detail

### 5.1 `TileCell` (Model) — two new fields

```csharp
public sealed class TileCell
{
    public string Value;
    public bool Cleared;
    public bool IsHiddenTile;      // NEW - permanent, set once at generation
    public bool Revealed = true;   // NEW - mutable; false only for IsHiddenTile cells initially
}
```

### 5.2 `BoardState` — one new field

```csharp
public string PeekedTileId;  // NEW - nullable; the one currently-peeked hidden tile, if any
```

### 5.3 `HiddenTileSelector`

```csharp
namespace GameDomain.Gameplay
{
    public static class HiddenTileSelector
    {
        private const int MinDifficulty = 4;
        private const float Fraction = 0.35f; // within the approved 30-40% range

        public static void Apply(BoardState board, int difficulty, Random random)
        {
            if (difficulty < MinDifficulty) return;

            var ids = board.Cells.Keys.ToList();
            int count = (int)Math.Round(ids.Count * Fraction);
            Shuffle(ids, random);
            for (int i = 0; i < count && i < ids.Count; i++)
            {
                board.Cells[ids[i]].IsHiddenTile = true;
                board.Cells[ids[i]].Revealed = false;
            }
        }

        private static void Shuffle(List<string> list, Random random) { /* Fisher-Yates, same pattern as PaletteSelector.Shuffle */ }
    }
}
```

Called from `GameController.PrepareLevel`, immediately after
`_board = BoardGenerator.GenerateShaped(...)`:

```csharp
_board = BoardGenerator.GenerateShaped(level, rng, profile, clusters, modelCount);
HiddenTileSelector.Apply(_board, difficulty, rng);   // NEW
```

Reuses the same `rng` already threaded through generation — no new randomness
source, no effect on board-shape/value reproducibility beyond this one extra draw.

### 5.4 `TileReveal`

```csharp
namespace GameDomain.Gameplay
{
    public static class TileReveal
    {
        // Attempts to reveal a currently-face-down tile. Returns false if it isn't
        // free (caller treats this exactly like an invalid tap). Returns true and
        // sets reHiddenSlotId (nullable) to whichever other tile got auto-re-hidden,
        // if any, so the caller knows which OTHER tile view to flip back down.
        public static bool TryReveal(
            BoardState board, Dictionary<string, TileSlot> slotsById, string slotId,
            out string reHiddenSlotId)
        {
            reHiddenSlotId = null;
            var remaining = new HashSet<string>(
                board.Cells.Where(kv => !kv.Value.Cleared && !board.TrayTileIds.Contains(kv.Key))
                    .Select(kv => kv.Key));
            if (!FreedomRuleCalculator.IsFree(slotsById[slotId], remaining))
                return false;

            if (board.PeekedTileId != null && board.PeekedTileId != slotId)
            {
                reHiddenSlotId = board.PeekedTileId;
                board.Cells[reHiddenSlotId].Revealed = false;
            }

            board.Cells[slotId].Revealed = true;
            board.PeekedTileId = slotId;
            return true;
        }
    }
}
```

Caller contract: only call this when `cell.Revealed == false` (checked by
`GameController.OnTileTapped` before calling, same guard style already used
throughout `GameController`).

### 5.5 `GameController.OnTileTapped` — new branch

```csharp
public void OnTileTapped(string slotId)
{
    if (_paused) return;
    if (IsInputLocked) return;
    if (_board.IsGameOver) return;
    if (_board.Cells.Values.All(c => c.Cleared)) return;

    var cell = _board.Cells[slotId];
    if (!cell.Revealed)
    {
        if (TileReveal.TryReveal(_board, _slotsById, slotId, out string reHiddenId))
        {
            _boardView.GetTileView(slotId)?.PlayFlipToFaceUp(TileVisual.IconFor(_boardView.TileSet, cell.Value));
            if (reHiddenId != null)
                _boardView.GetTileView(reHiddenId)?.PlayFlipToFaceDown(TileVisual.BackIcon(_boardView.TileSet));
            return;
        }
        // not free - identical invalid-tap feedback to the existing branch below
        _boardView.GetTileView(slotId)?.PlayShake();
        /* ...same vibration/sound as today... */
        return;
    }

    // unchanged from here down
    var oldTray = new List<string>(_board.TrayTileIds);
    if (!TrayManager.TryPushToTray(_board, _slotsById, slotId)) { /* unchanged */ return; }
    if (_board.PeekedTileId == slotId) _board.PeekedTileId = null;   // NEW, one line
    /* unchanged: AnimateTapToTray(...) */
}
```

No flip animation plays for tiles that were never hidden (`Revealed` is `true` from
generation) — this branch is only ever reached for `IsHiddenTile` cells, so the
existing flow for every other tile is byte-for-byte unchanged.

### 5.6 Undo integration

`GameController.AnimateUndo` already retrieves `popped` from `TrayUndo.TryUndo`
before restoring the board tile via `_boardView.RestoreTile`. One line added right
after:

```csharp
string popped = TrayUndo.TryUndo(_board);
if (popped == null) { IsInputLocked = false; yield break; }
if (_board.Cells[popped].IsHiddenTile) _board.Cells[popped].Revealed = false;  // NEW
```

`_boardView.RestoreTile` (presentation) needs to pick the back sprite instead of
the front icon when `Revealed` is now `false`, same conditional the initial deal-in
uses (§5.7). `UndoStack` (the other, currently-dead undo path noted in the tray-fix
work earlier this session) is not touched — it isn't live.

### 5.7 Shuffle interaction

`ShuffleService.Shuffle` (via `OnShuffleRequested`) reassigns values across
uncleared on-board cells, which includes a currently-peeked tile — reassigning a
new value underneath an already-flipped-up tile without resetting its state would
silently change what the player is looking at. Shuffle should treat concealment
like a fresh deal for whatever's currently peeked: if `board.PeekedTileId != null`,
reset that cell's `Revealed = false` (only meaningful if `IsHiddenTile`, which it
always is when `PeekedTileId` is set) and clear `board.PeekedTileId = null` before
reassigning values, mirroring how the board already re-deals with
`animateDealIn: true` on shuffle. One small addition to `GameController.
OnShuffleRequested`, no `ShuffleService` domain changes needed.

### 5.8 Hints — no code change

`GameController.OnHintRequested` calls `_boardView.GetTileView(a)?.Highlight()`.
`TileView3D.Highlight()` Lerps `_bodyTint.Color` on whatever sprite `_bodyRenderer`
currently shows — it doesn't care whether that's the front icon or the back sprite.
`TrayHintFinder.FindHint` is free to suggest a face-down tile exactly as it does
today; the glow just plays over the card back. Confirmed with the design owner:
hints should *not* force a reveal. No changes needed to `TrayHintFinder` or
`OnHintRequested`.

### 5.9 Presentation

- **New shared card-back sprite** — one generic asset (not per-value; the point is
  the value is hidden), resolved via a new `TileVisual.BackIcon(TileSetAsset)`
  helper alongside the existing `IconFor`/`AccentColorFor`. Needs one new field on
  `TileSetAsset` (e.g. `public Sprite CardBack;`) and the actual art authored —
  open item, see §8.
- **`TileView3D`** gains `PlayFlipToFaceUp(Sprite frontSprite)` and
  `PlayFlipToFaceDown(Sprite backSprite)`: animate `_bodyRenderer.transform`'s local
  X scale 1→0, swap `_bodyRenderer.sprite` at the midpoint, then 0→1 — same
  coroutine-driven-animation style already used by `PopSettleRoutine`/
  `ShakeRoutine` in this file, just a new routine.
- **`BoardView3D.Build`/`RestoreTile`** need to pass the correct starting sprite
  based on `cell.Revealed` (front icon if `true`, `TileVisual.BackIcon` if
  `false`) instead of always calling `TileVisual.IconFor`.

## 6. Data Flow & Error Handling

1. `PrepareLevel` generates the board (unchanged), then `HiddenTileSelector.Apply`
   marks the hidden subset — a pure post-processing step with no retry/reseed logic
   needed (it can't fail; it's just a random subset pick, always succeeds).
2. `TileReveal.TryReveal` returning `false` is not an error — it's the same "tile
   isn't free" case `TrayManager.TryPushToTray` already handles today, given the
   identical shake/vibrate/sound feedback.
3. No new failure modes for solvability or tray-safety — this feature cannot make a
   board unsolvable or cause an unintended game-over, because it only gates *when*
   a tap reaches `TrayManager`, never *which* tiles are free or *what* values exist.

## 7. Testing Plan

New EditMode tests in `Domain.Tests` (pure, no Unity types needed, following the
existing `Gameplay/` test folder pattern):

1. **`HiddenTileSelectorTests`** — difficulty < 4 → zero `IsHiddenTile` cells;
   difficulty >= 4 → roughly `Fraction` of cells marked (assert a tolerance band,
   deterministic via a seeded `Random`); every `IsHiddenTile` cell starts
   `Revealed = false`; every other cell stays `Revealed = true`.
2. **`TileRevealTests`**:
   - Revealing a free face-down tile sets `Revealed = true` and `PeekedTileId` to
     that slot; returns `true`, `reHiddenSlotId = null` (nothing was peeked
     before).
   - Revealing a *second* free face-down tile while the first is peeked: first
     tile's `Revealed` flips back to `false`, `reHiddenSlotId` equals the first
     tile's id, second tile becomes `PeekedTileId`.
   - Tapping the *same* already-peeked tile again via `TryReveal` is never called
     (caller only calls it when `!Revealed`) — covered by the `OnTileTapped`
     branch logic, not this function; no test needed here for that path since it's
     a caller-side guard.
   - Attempting to reveal a covered (non-free) face-down tile returns `false`,
     no state changes.
3. **Extend existing `TrayManagerPairTests`-style test** (or a small new test) —
   confirm a normal (never-hidden) tile's tap-to-tray flow is completely
   unaffected: `TrayManager.TryPushToTray` behavior is unchanged when
   `HiddenTileSelector.Apply` was never called (difficulty < 4 boards) and
   unchanged for non-hidden cells on difficulty >= 4 boards.
4. **Regression:** `SolvabilityRegressionTests` need no changes and must continue
   passing untouched — this feature doesn't alter generation, confirming the
   non-goal in §2 held.

No PlayMode/presentation tests — consistent with the rest of this project, which
has no PlayMode test infrastructure; the flip animations and back-sprite wiring get
verified visually (Editor screenshot + on-device build), same as every other
presentation change this session.

## 8. Open Items to Resolve During Implementation

- **Card-back art:** needs an actual asset (`TileSetAsset.CardBack`). Simplest
  placeholder: reuse the existing card silhouette/border with a plain tinted
  center (no icon) rather than commissioning new art up front — can be swapped
  later without touching any code, since it's just a sprite reference.
- **Exact hidden fraction (35%):** a concrete starting value within the approved
  30-40% range; tune after playtesting difficulty 4-5 boards on-device.
- **Flip animation timing:** propose ~0.15s each half (matching the existing
  `TapAwayDuration`-scale of other tile animations in `CardAnimator`), tuned
  on-device like every other animation constant in this codebase.

## 9. Summary

A small, fully-isolated addition: two new fields on `TileCell`, one on `BoardState`,
and two new tiny `GameDomain.Gameplay` classes (`HiddenTileSelector`,
`TileReveal`) that own the entire reveal/peek state machine and are unit-testable
in the existing `Domain.Tests` assembly. `GameController.OnTileTapped` gains one
early branch and one line in the undo path; `TrayManager`, `FreedomRuleCalculator`,
generation, and solvability are completely untouched. Hints work with zero code
changes. The only real new work is the presentation flip animation and the
card-back art asset.
