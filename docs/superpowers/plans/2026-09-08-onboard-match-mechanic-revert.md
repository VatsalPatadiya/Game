# On-Board Pair-Match Mechanic Revert Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Revert the live gameplay path from tray-collection back to classic on-board pair matching (tap a free tile, tap a matching free tile to clear the pair), with working hint/undo/shuffle and a limited-moves lose condition.

**Architecture:** The domain layer (`MatchValidator`, `FreedomRuleCalculator`, `HintFinder`, `UndoStack`, `ShuffleService`, `ComboScorer`, `BoardGenerator`) is already pair-match-shaped and untouched — this is a `GameController`/presentation rewire plus small additive domain fields. Win/lose/moves logic goes into a new pure `EndStateEvaluator` domain class so it is EditMode-testable (GameController is a MonoBehaviour and cannot be unit-tested). The tray classes stay in the tree, dormant and unreferenced.

**Tech Stack:** Unity 6000.5.9f1, C#, NUnit EditMode tests (`Domain.Tests` asmdef). Build/deploy via `AndroidBuilder.RegenerateAndBuild` in a fresh batchmode process.

**Spec:** `docs/superpowers/specs/2026-09-08-onboard-match-mechanic-revert-design.md`

## Global Constraints

- Original art/code only; touch no reference-app assets.
- Do NOT delete or edit `TrayManager`, `TrayView3D`, `TraySlotView3D` — leave them dormant/unreferenced. The live match path must call none of them.
- Existing pair-oriented EditMode tests (`MatchValidatorTests`, `HintFinderTests`, `UndoStackTests`, `ShuffleServiceTests`, `BoardGeneratorTests`, `FreedomRuleCalculatorTests`, `SolvabilityRegressionTests`) MUST pass unchanged — that is the regression signal that this is a rewire, not a rewrite.
- A move is consumed on a **successful match only**; a mismatched reselect costs no move. Undo restores the pair but does **not** refund the spent move.
- Domain namespaces: `GameDomain.Model`, `GameDomain.Gameplay`, `GameDomain.Generation`. Test namespace: `GameDomain.Tests.*`, fixtures in `GameDomain.Tests.Fixtures` (`TestLayoutShapes`).
- Moves-budget semantics: `LevelDefinition.MovesBudget <= 0` means unlimited (never triggers a moves loss).

---

### Task 1: Add moves-budget domain fields

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Domain/Model/LevelDefinition.cs`
- Modify: `unity/GameClient/Assets/Scripts/Domain/Model/BoardState.cs`
- Modify: `unity/GameClient/Assets/Scripts/Domain/Generation/BoardGenerator.cs` (in `Generate`, the pair variant only)
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/BoardGeneratorTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `LevelDefinition.MovesBudget` (int, default `-1`); `BoardState.MovesRemaining` (int); `BoardGenerator.Generate` sets `board.MovesRemaining = level.MovesBudget`.

- [ ] **Step 1: Write the failing test**

Add to `BoardGeneratorTests.cs`:
```csharp
[Test]
public void Generate_CopiesMovesBudgetOntoBoardMovesRemaining()
{
    var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
    var level = new LevelDefinition { LevelId = 1, Shape = shape, TileSetId = "default", MovesBudget = 42 };

    var board = BoardGenerator.Generate(level, new System.Random(1));

    Assert.That(board.MovesRemaining, Is.EqualTo(42));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run EditMode tests (fresh batchmode):
```bash
/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity -runTests -batchmode -projectPath unity/GameClient -testPlatform EditMode -testFilter "Generate_CopiesMovesBudgetOntoBoardMovesRemaining" -logFile - 2>&1 | tail -30
```
Expected: FAIL — `MovesBudget`/`MovesRemaining` do not exist (compile error), or assertion fails.

- [ ] **Step 3: Write minimal implementation**

In `LevelDefinition.cs`, add field:
```csharp
public int MovesBudget = -1; // <= 0 means unlimited
```
In `BoardState.cs`, add field:
```csharp
public int MovesRemaining; // set from LevelDefinition.MovesBudget at generation
```
In `BoardGenerator.Generate`, in the block that builds `var board = new BoardState { ... }`, add after `LevelId = level.LevelId`:
```csharp
MovesRemaining = level.MovesBudget,
```

- [ ] **Step 4: Run test to verify it passes**

Run the same command as Step 2. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Domain/Model/LevelDefinition.cs \
        unity/GameClient/Assets/Scripts/Domain/Model/BoardState.cs \
        unity/GameClient/Assets/Scripts/Domain/Generation/BoardGenerator.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Generation/BoardGeneratorTests.cs
git commit -m "feat: add MovesBudget/MovesRemaining domain fields"
```

---

### Task 2: EndStateEvaluator — win / lose / stuck logic (pure domain)

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Domain/Gameplay/EndStateEvaluator.cs`
- Create: `unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/EndStateEvaluatorTests.cs`

**Interfaces:**
- Consumes: `BoardState`, `TileSlot`, `HintFinder.FindFreePair`, `FreedomRuleCalculator` (via HintFinder).
- Produces:
  - `enum EndState { InProgress, Won, Lost }` in namespace `GameDomain.Gameplay`.
  - `static EndState EndStateEvaluator.Evaluate(BoardState board, Dictionary<string, TileSlot> slotsById)`.

Rules (from spec):
- `Won` when every cell is `Cleared`.
- `Lost` when tiles remain AND either: (a) a real budget (`MovesRemaining` came from a `MovesBudget > 0`) has hit `0`; or (b) no legal move exists (`HintFinder.FindFreePair == null`) AND `ShufflesRemaining == 0`.
- Otherwise `InProgress`. Note: stuck-but-shuffles-remain is `InProgress`.

Budget detection: `Evaluate` cannot see `MovesBudget`, only `MovesRemaining`. Treat `MovesRemaining == 0` as a real exhausted budget and `MovesRemaining < 0` as "unlimited / not tracked". (Generation copies `MovesBudget` verbatim, so an unlimited level carries `MovesRemaining = -1` and never equals 0; a real budget counts down toward 0.)

- [ ] **Step 1: Write the failing tests**

Create `EndStateEvaluatorTests.cs`:
```csharp
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Gameplay;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Gameplay
{
    public class EndStateEvaluatorTests
    {
        private static (BoardState, Dictionary<string, TileSlot>) MakeBoard(
            Dictionary<string, string> values, int movesRemaining, int shufflesRemaining)
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var slotsById = shape.ToDictionary(s => s.Id);
            var board = new BoardState
            {
                MovesRemaining = movesRemaining,
                ShufflesRemaining = shufflesRemaining,
                Cells = values.ToDictionary(kv => kv.Key, kv => new TileCell { Value = kv.Value })
            };
            return (board, slotsById);
        }

        [Test]
        public void Evaluate_AllCleared_ReturnsWon()
        {
            var (board, slots) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "b", ["L0_3"] = "a" },
                movesRemaining: -1, shufflesRemaining: 3);
            foreach (var c in board.Cells.Values) c.Cleared = true;

            Assert.That(EndStateEvaluator.Evaluate(board, slots), Is.EqualTo(EndState.Won));
        }

        [Test]
        public void Evaluate_MovesExhaustedWithTilesLeft_ReturnsLost()
        {
            var (board, slots) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "b", ["L0_3"] = "a" },
                movesRemaining: 0, shufflesRemaining: 3);

            Assert.That(EndStateEvaluator.Evaluate(board, slots), Is.EqualTo(EndState.Lost));
        }

        [Test]
        public void Evaluate_UnlimitedMovesWithPlayableBoard_ReturnsInProgress()
        {
            var (board, slots) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "b", ["L0_3"] = "a" },
                movesRemaining: -1, shufflesRemaining: 3);

            Assert.That(EndStateEvaluator.Evaluate(board, slots), Is.EqualTo(EndState.InProgress));
        }

        [Test]
        public void Evaluate_StuckButShufflesRemain_ReturnsInProgress()
        {
            // No two free tiles share a value => FindFreePair is null.
            var (board, slots) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "c", ["L0_3"] = "d" },
                movesRemaining: -1, shufflesRemaining: 1);

            Assert.That(EndStateEvaluator.Evaluate(board, slots), Is.EqualTo(EndState.InProgress));
        }

        [Test]
        public void Evaluate_StuckAndNoShuffles_ReturnsLost()
        {
            var (board, slots) = MakeBoard(
                new Dictionary<string, string> { ["L0_0"] = "a", ["L0_1"] = "b", ["L0_2"] = "c", ["L0_3"] = "d" },
                movesRemaining: -1, shufflesRemaining: 0);

            Assert.That(EndStateEvaluator.Evaluate(board, slots), Is.EqualTo(EndState.Lost));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity -runTests -batchmode -projectPath unity/GameClient -testPlatform EditMode -testFilter "EndStateEvaluatorTests" -logFile - 2>&1 | tail -30
```
Expected: FAIL — `EndStateEvaluator`/`EndState` do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `EndStateEvaluator.cs`:
```csharp
using System.Collections.Generic;
using System.Linq;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    public enum EndState { InProgress, Won, Lost }

    public static class EndStateEvaluator
    {
        public static EndState Evaluate(BoardState board, Dictionary<string, TileSlot> slotsById)
        {
            if (board.Cells.Values.All(c => c.Cleared))
                return EndState.Won;

            // MovesRemaining == 0 is a real exhausted budget; < 0 means unlimited/untracked.
            if (board.MovesRemaining == 0)
                return EndState.Lost;

            bool anyLegalMove = HintFinder.FindFreePair(board, slotsById) != null;
            if (!anyLegalMove && board.ShufflesRemaining <= 0)
                return EndState.Lost;

            return EndState.InProgress;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run the same command as Step 2. Expected: PASS (all 5).

- [ ] **Step 5: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Domain/Gameplay/EndStateEvaluator.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/EndStateEvaluatorTests.cs
git commit -m "feat: add EndStateEvaluator win/lose/stuck domain logic"
```

---

### Task 3: BoardView3D restore/refresh methods for undo & shuffle

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/Board3D/BoardView3D.cs`
- Test: none (MonoBehaviour view code; verified on-device in Task 5). Compile-only check.

**Interfaces:**
- Consumes: existing private tile-placement logic in `Build()` (localPosition formula, `JitterFor`, `LayerRenderOffset`, `TileVisual.FoodModelFor`, `_tileViews`, `_slotsById`).
- Produces:
  - `void BoardView3D.RestoreTiles(IEnumerable<string> slotIds, BoardState board)` — re-instantiates tile views for undone (un-cleared) slots at their original layer/position with a fade-in, re-registers them in `_tileViews`, then `RefreshFreeStates(board)`.
  - `void BoardView3D.RefreshTileValues(IEnumerable<string> slotIds, BoardState board)` — rebuilds the food model of existing tile views to match `board.Cells[id].Value`, then `RefreshFreeStates(board)`.

Design note: to avoid duplicating the localPosition math, extract a private helper from `Build()`.

- [ ] **Step 1: Extract placement helper (refactor, no behavior change)**

In `BoardView3D.cs`, add a private helper and call it from `Build()`'s instantiation loop (replace the inline `view.transform.localPosition = ...; view.transform.localRotation = ...;` block):
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
}
```
In `Build()`, replace the two lines that set `view.transform.localPosition`/`localRotation` with `PlaceTileView(view, slot);`.

- [ ] **Step 2: Add RestoreTiles and RefreshTileValues**

Append to `BoardView3D`:
```csharp
public void RestoreTiles(IEnumerable<string> slotIds, BoardState board)
{
    foreach (var id in slotIds)
    {
        if (_tileViews.ContainsKey(id)) continue;
        if (!_slotsById.TryGetValue(id, out var slot)) continue;
        if (!board.Cells.TryGetValue(id, out var cell)) continue;

        var view = Instantiate(_tilePrefab, transform);
        PlaceTileView(view, slot);
        view.Initialize(slot.Id, slot.Layer, TileVisual.FoodModelFor(_tileSet, cell.Value));
        view.PlayFadeInOnly();
        _tileViews[id] = view;
    }
    RefreshFreeStates(board);
}

public void RefreshTileValues(IEnumerable<string> slotIds, BoardState board)
{
    foreach (var id in slotIds)
    {
        if (!_tileViews.TryGetValue(id, out var view)) continue;
        if (!_slotsById.TryGetValue(id, out var slot)) continue;
        if (!board.Cells.TryGetValue(id, out var cell)) continue;
        view.Initialize(slot.Id, slot.Layer, TileVisual.FoodModelFor(_tileSet, cell.Value));
    }
    RefreshFreeStates(board);
}
```
(`Initialize` already clears and rebuilds the food anchor's children, so it doubles as a value-swap.)

- [ ] **Step 3: Compile check**

```bash
/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity -quit -batchmode -projectPath unity/GameClient -executeMethod GameClient.Editor.NoOpCompileCheck -logFile - 2>&1 | tail -20 || \
/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity -runTests -batchmode -projectPath unity/GameClient -testPlatform EditMode -testFilter "BoardGeneratorTests" -logFile - 2>&1 | tail -20
```
Expected: compiles with no errors (the test-run fallback also forces a full compile). If `NoOpCompileCheck` does not exist, rely on the EditMode run compiling all assemblies.

- [ ] **Step 4: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Presentation/Board3D/BoardView3D.cs
git commit -m "feat: BoardView3D RestoreTiles/RefreshTileValues for undo & shuffle"
```

---

### Task 4: GameController rewire — select-then-match + aids + end states

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/GameController.cs`
- Test: none (MonoBehaviour; logic under test lives in `EndStateEvaluator`, Task 2). Compile + on-device (Task 5).

**Interfaces:**
- Consumes: `MatchValidator.TryMatch`, `HintFinder.FindFreePair`, `UndoStack.TryUndo`, `ShuffleService.Shuffle`, `ComboScorer.RegisterMatch`, `EndStateEvaluator.Evaluate`/`EndState`, `BoardView3D.{GetTileView,RefreshFreeStates,RemoveTiles,RestoreTiles,RefreshTileValues}`, `TileView3D.{SetSelected,Highlight,PlayShake,PlayClearAndDestroy}`, `FreedomRuleCalculator.IsFree`, `MatchCelebrationController.PlayMatchCelebration`, `GameOverPopup3D.{ShowWin,ShowLose}`.
- Produces: rewired `OnTileTapped`, `OnHintRequested`, `OnUndoRequested`, `OnShuffleRequested`; new `EvaluateEndState()` using `EndStateEvaluator`.

- [ ] **Step 1: Replace tray state with selection state**

In `GameController`, remove the tray-specific presentation combo timer block if it references the tray, and add:
```csharp
private string _selectedSlotId; // null = nothing selected
private ComboScorer _comboScorer;
private readonly System.Random _random = new System.Random();
```
Keep `_trayView`/`_matchCelebration`/`_gameOverPopup` serialized fields (the tray field may stay wired but the code must not push to it). In `LoadLevel`, keep board generation but pass a moves budget: build the `LevelDefinition` with `MovesBudget = 60` (placeholder budget so the lose path is exercised before real level data), instantiate `_comboScorer = new ComboScorer();`, and stop calling `_trayView.Initialize(...)`. Leave `_board = BoardGenerator.Generate(level, _random);`.

- [ ] **Step 2: Rewrite OnTileTapped as select-then-match**

Replace `OnTileTapped` and `AnimateTapToTray` with:
```csharp
public void OnTileTapped(string slotId)
{
    if (IsInputLocked) return;
    if (_board.IsGameOver) return;
    if (_board.Cells.Values.All(c => c.Cleared)) return;

    var remaining = new HashSet<string>(
        _board.Cells.Where(kv => !kv.Value.Cleared).Select(kv => kv.Key));
    bool isFree = FreedomRuleCalculator.IsFree(_slotsById[slotId], remaining);
    if (!isFree)
    {
        _boardView.GetTileView(slotId)?.PlayShake();
        return;
    }

    if (_selectedSlotId == null)
    {
        Select(slotId);
        return;
    }

    if (_selectedSlotId == slotId)
    {
        Deselect();
        return;
    }

    string a = _selectedSlotId;
    if (MatchValidator.TryMatch(_board, _slotsById, a, slotId))
    {
        Deselect();
        _board.MovesRemaining = _board.MovesRemaining < 0 ? _board.MovesRemaining : _board.MovesRemaining - 1;
        int points = _comboScorer.RegisterMatch(_board, System.DateTime.UtcNow);
        var pos = _boardView.GetTileView(slotId)?.transform.position ?? Vector3.zero;
        _matchCelebration?.PlayMatchCelebration(pos, _board.ComboCount > 1);
        _boardView.RemoveTiles(new[] { a, slotId });
        _boardView.RefreshFreeStates(_board);
        ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
        EvaluateEndState();
    }
    else
    {
        // Different free tile, values differ: carry selection forward.
        Deselect();
        Select(slotId);
    }
}

private void Select(string slotId)
{
    _selectedSlotId = slotId;
    _boardView.GetTileView(slotId)?.SetSelected(true);
}

private void Deselect()
{
    if (_selectedSlotId != null)
        _boardView.GetTileView(_selectedSlotId)?.SetSelected(false);
    _selectedSlotId = null;
}
```

- [ ] **Step 3: Rewrite EvaluateEndState via EndStateEvaluator**

Replace `EvaluateEndState`:
```csharp
private void EvaluateEndState()
{
    switch (EndStateEvaluator.Evaluate(_board, _slotsById))
    {
        case EndState.Won:
            _gameOverPopup?.ShowWin(this, _board.Score);
            break;
        case EndState.Lost:
            _board.IsGameOver = true;
            _gameOverPopup?.ShowLose(this);
            break;
    }
}
```

- [ ] **Step 4: Un-stub the aids**

Replace the three stubbed methods:
```csharp
public void OnHintRequested()
{
    if (IsInputLocked || _board.IsGameOver) return;
    if (_board.HintsRemaining <= 0) return;
    var hint = HintFinder.FindFreePair(_board, _slotsById);
    if (hint == null) return;
    _board.HintsRemaining -= 1;
    _boardView.GetTileView(hint.Value.slotIdA)?.Highlight();
    _boardView.GetTileView(hint.Value.slotIdB)?.Highlight();
    NotifyUsesChanged();
}

public void OnUndoRequested()
{
    if (IsInputLocked || _board.IsGameOver) return;
    if (_board.UndosRemaining <= 0 || _board.MoveHistory.Count == 0) return;
    var last = _board.MoveHistory[_board.MoveHistory.Count - 1];
    if (!UndoStack.TryUndo(_board)) return;
    Deselect();
    _boardView.RestoreTiles(new[] { last.SlotIdA, last.SlotIdB }, _board);
    ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
    NotifyUsesChanged();
}

public void OnShuffleRequested()
{
    if (IsInputLocked || _board.IsGameOver) return;
    if (_board.ShufflesRemaining <= 0) return;
    var remainingIds = _board.Cells.Where(kv => !kv.Value.Cleared).Select(kv => kv.Key).ToList();
    if (!ShuffleService.Shuffle(_board, _shape, _random)) return;
    Deselect();
    _boardView.RefreshTileValues(remainingIds, _board);
    NotifyUsesChanged();
}
```
Confirm `_shape` is still a field (it is, set in `LoadLevel`). Ensure `using System.Collections.Generic;` and `using System.Linq;` remain.

- [ ] **Step 5: Compile via a full EditMode run (also runs regression suite)**

```bash
/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity -runTests -batchmode -projectPath unity/GameClient -testPlatform EditMode -logFile - 2>&1 | tail -40
```
Expected: project compiles; ALL EditMode tests pass, including the untouched pair-match regression suite and the new Task 1/2 tests.

- [ ] **Step 6: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Presentation/GameController.cs
git commit -m "feat: rewire GameController to on-board pair matching + aids + end states"
```

---

### Task 5: Remove tray HUD row from the scene + on-device verification

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder3D.cs` (stop building the tray HUD row; leave tray classes intact)
- No test file; verified on-device.

**Interfaces:**
- Consumes: existing `GameSceneBuilder3D` HUD-building code.
- Produces: a scene with no tray slot row; all other HUD elements unchanged.

- [ ] **Step 1: Locate and disable tray-row construction**

In `GameSceneBuilder3D.cs`, find where the tray slot row is built (search for `traySlotCount`, `TrayView3D`, `BuildTraySlotPrefab`, or `Tray` anchor placement). Comment out / remove only the calls that instantiate the tray row and wire `_trayView`, leaving the leaf/background/topbar/progress/control-button construction intact. Do NOT delete `TrayView3D`/`TraySlotView3D` classes. If `GameController._trayView` is a required serialized reference, leave the field but assign it null / skip wiring (guard: GameController no longer dereferences it on the match path).

Run:
```bash
grep -n "tray\|Tray" unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder3D.cs
```
to enumerate every tray touchpoint before editing.

- [ ] **Step 2: Regenerate + build in a fresh batchmode process**

Kill any live editor first (per project gotcha: eval against a long-lived editor runs stale code), then:
```bash
pkill -f "Unity.app/Contents/MacOS/Unity" 2>/dev/null; sleep 2
/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity -quit -batchmode -projectPath unity/GameClient -executeMethod AndroidBuilder.RegenerateAndBuild -logFile - 2>&1 | tail -40
```
Expected: `REGENERATE_ALL_DONE` and `ANDROID_BUILD_RESULT:Succeeded`, zero compile errors. Then confirm the scene actually changed, e.g.:
```bash
grep -c "field of view" unity/GameClient/Assets/Scenes/Game.unity   # sanity that scene regenerated
```

- [ ] **Step 3: Deploy and verify on device**

```bash
adb -s RZ8M70HFTCM install -r unity/GameClient/Builds/GameClient.apk
adb -s RZ8M70HFTCM shell am start -n com.gameclient.mahjong/com.unity3d.player.UnityPlayerGameActivity
```
Then verify by screenshot/interaction (poll ~10s for content to render):
- Level-start screen shows; tap Play.
- Tap a free tile → it highlights + lifts (SetSelected). Tap a blocked tile → shake, no select.
- Tap a matching free tile → pair clears with celebration; score increases.
- Tap two mismatched free tiles → selection carries to the second, first deselects.
- Hint highlights a real pair and its badge decrements; Undo restores the last cleared pair; Shuffle re-lays a still-solvable board.
- Play out to a win → win popup. (Optional) force `MovesBudget` low temporarily to confirm the moves-exhausted lose popup, then restore to 60.
- Confirm no tray row is visible.

- [ ] **Step 4: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder3D.cs \
        unity/GameClient/Assets/Scenes/Game.unity
git commit -m "feat: remove tray HUD row; on-board match verified on device"
```

---

## Self-Review

**Spec coverage:**
- Select-then-match, mismatch-reselect, blocked-tile shake → Task 4.
- Win / moves-exhausted lose / stuck-no-shuffle lose → Task 2 (logic) + Task 4 (wiring).
- MovesBudget/MovesRemaining, move consumed on match only, no refund on undo → Task 1 + Task 4.
- Hint/undo/shuffle un-stub + BoardView sync → Task 3 + Task 4.
- Tray classes kept dormant, tray HUD row removed → Global Constraints + Task 5.
- Regression suite unchanged & passing → Task 4 Step 5.

**Placeholder scan:** No TBD/TODO; all steps contain concrete code or exact commands.

**Type consistency:** `EndState`/`EndStateEvaluator.Evaluate` (Task 2) used verbatim in Task 4. `RestoreTiles(IEnumerable<string>, BoardState)` / `RefreshTileValues(IEnumerable<string>, BoardState)` (Task 3) match Task 4 call sites. `MovesRemaining`/`MovesBudget` (Task 1) match Task 2/4 usage. `HintFinder.FindFreePair` returns `(string slotIdA, string slotIdB)?` — accessed as `hint.Value.slotIdA/.slotIdB` in both Task 2 test and Task 4, consistent with `HintFinderTests`.

**Open risk to watch during execution:** the exact `MatchCelebrationController.PlayMatchCelebration` signature and whether `GameController._trayView` is `[SerializeField]`-required — the executor must read `GameController.cs` and `MatchCelebrationController.cs` as they exist before editing (both are already partially referenced in the current file).
