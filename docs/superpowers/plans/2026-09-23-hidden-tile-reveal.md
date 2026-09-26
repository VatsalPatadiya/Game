# Hidden (Face-Down) Tiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** On difficulty-4+ boards, ~35% of tiles start face-down; tapping a free
face-down tile reveals it instead of collecting it, tapping a *different*
face-down tile re-hides the previous reveal, and undo/shuffle reset concealment
state correctly.

**Architecture:** Two new pure, engine-free classes in `GameDomain.Gameplay`
(`HiddenTileSelector`, `TileReveal`) own generation-time selection and the
reveal/peek state machine, unit-tested in the existing `Domain.Tests` assembly.
`GameController` gains one early branch in `OnTileTapped` plus one-line hooks in
`PrepareLevel`/`AnimateUndo`/`OnShuffleRequested`. Presentation gets a shared
card-back sprite (baked via an Editor script reusing the existing
`TileFaceTexture.Build`) and a flip animation on `TileView3D`. `TrayManager`,
`FreedomRuleCalculator`, and all board-generation/solvability code are untouched.

**Tech Stack:** Unity 6000.5.9f1, C#, NUnit EditMode tests (`Domain.Tests`
assembly), URP.

**Spec:** `docs/superpowers/specs/2026-09-23-hidden-tile-reveal-design.md`

## Global Constraints

- Feature only activates at `Difficulty >= 4` (spec §3, §5.3) — difficulties 1-3
  are completely unaffected.
- Hidden fraction is `0.35f` of a board's cells (spec §5.3), a concrete value
  within the approved 30-40% range.
- No changes to `TrayManager`, `FreedomRuleCalculator`, `BoardGenerator`,
  `PaletteSelector`, `DifficultyProfile`, or any solvability/branching code (spec
  §2 Non-Goals).
- `TileReveal`/`HiddenTileSelector` must live in `GameDomain.Gameplay` (the
  `Domain` assembly), never as logic embedded in `GameController` — that
  assembly cannot be referenced by `Domain.Tests` (confirmed earlier this
  project: Unity compiles the implicit `Assembly-CSharp` last, so no custom
  asmdef can depend on it), so any state-machine logic placed there would be
  permanently untestable.
- Every domain-layer task must follow TDD: write the failing test, watch it
  fail for the right reason, then implement.
- Do not commit until told to — this project's established working convention
  this session. Each task below still ends with a "Step: Commit" for tracking,
  but hold actual `git commit` execution until the user says go, exactly as
  every other task this session has.

---

## File Structure

**Create:**
- `unity/GameClient/Assets/Scripts/Domain/Gameplay/HiddenTileSelector.cs` — generation-time random selection of hidden tiles.
- `unity/GameClient/Assets/Scripts/Domain/Gameplay/TileReveal.cs` — the reveal/peek state machine.
- `unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/HiddenTileSelectorTests.cs`
- `unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/TileRevealTests.cs`

**Modify:**
- `unity/GameClient/Assets/Scripts/Domain/Model/TileCell.cs` — add `IsHiddenTile`, `Revealed`.
- `unity/GameClient/Assets/Scripts/Domain/Model/BoardState.cs` — add `PeekedTileId`.
- `unity/GameClient/Assets/Scripts/Tests/EditMode/Model/BoardStateTests.cs` — extend with default-state assertions.
- `unity/GameClient/Assets/Scripts/Data/TileSetAsset.cs` — add `CardBack` sprite field.
- `unity/GameClient/Assets/Scripts/Presentation/Board/TileVisual.cs` — add `BackIcon`.
- `unity/GameClient/Assets/Scripts/Editor/TileMaterialGenerator.cs` — add a card-back texture bake, reusing the existing `TileFaceTexture.Build`.
- `unity/GameClient/Assets/Scripts/Presentation/Board3D/TileView3D.cs` — flip animations + a shared fit-scale helper.
- `unity/GameClient/Assets/Scripts/Presentation/Board3D/BoardView3D.cs` — `Build`/`RestoreTile` pick the back sprite for unrevealed cells.
- `unity/GameClient/Assets/Scripts/Presentation/GameController.cs` — `PrepareLevel`, `OnTileTapped`, `AnimateUndo`, `OnShuffleRequested`.

---

### Task 1: Domain model fields

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Domain/Model/TileCell.cs`
- Modify: `unity/GameClient/Assets/Scripts/Domain/Model/BoardState.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Model/BoardStateTests.cs`

**Interfaces:**
- Produces: `TileCell.IsHiddenTile` (bool, default `false`), `TileCell.Revealed`
  (bool, default `true`), `BoardState.PeekedTileId` (string, nullable, default
  `null`) — every later task reads/writes these exact names.

- [ ] **Step 1: Write the failing tests**

Add to `unity/GameClient/Assets/Scripts/Tests/EditMode/Model/BoardStateTests.cs`,
inside the existing `BoardStateTests` class, after `TileSlot_DefaultsToEmptyCoveredByList`:

```csharp
        [Test]
        public void NewBoardState_PeekedTileIdStartsNull()
        {
            var board = new BoardState();
            Assert.That(board.PeekedTileId, Is.Null);
        }

        [Test]
        public void NewTileCell_DefaultsToRevealedAndNotHidden()
        {
            var cell = new TileCell();
            Assert.That(cell.Revealed, Is.True);
            Assert.That(cell.IsHiddenTile, Is.False);
        }
```

- [ ] **Step 2: Verify the tests fail to compile**

Via the connected Unity Editor:
```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Then check `get_console_logs` for `CS0117` errors on `PeekedTileId`/`Revealed`/`IsHiddenTile` not existing on `BoardState`/`TileCell`. (If a live Editor isn't connected for this session, `unity test /path/to/GameClient --editor-version 6000.5.9f1 --mode EditMode` is the headless fallback — see `unity-cli`/`unity-test` skills.)

- [ ] **Step 3: Implement the fields**

`TileCell.cs` — full new content:

```csharp
namespace GameDomain.Model
{
    public sealed class TileCell
    {
        public string Value;
        public bool Cleared;
        // Permanent - set once at generation by HiddenTileSelector, never
        // changes. Distinguishes "was originally a hidden tile" from "is
        // currently showing its front" (Revealed), which Undo needs to tell
        // apart (see TileReveal / GameController.AnimateUndo).
        public bool IsHiddenTile;
        // Mutable. True for every normal tile from generation on. False only
        // for IsHiddenTile cells until the player reveals them (TileReveal);
        // can flip back to false if a different hidden tile gets revealed
        // first (see BoardState.PeekedTileId).
        public bool Revealed = true;
    }
}
```

In `BoardState.cs`, add the field alongside the other mutable board-wide state
(after `public bool IsGameOver = false;`):

```csharp
        // The one hidden tile (if any) currently showing its front face but
        // not yet collected. Null when nothing is peeked. See TileReveal.
        public string PeekedTileId;
```

- [ ] **Step 4: Run tests, verify pass**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
# wait for compile, then:
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: all EditMode tests pass, including the 2 new ones (total count goes up by 2 from whatever the current baseline is).

- [ ] **Step 5: Commit** (hold until the user says go)

```bash
git add unity/GameClient/Assets/Scripts/Domain/Model/TileCell.cs \
        unity/GameClient/Assets/Scripts/Domain/Model/BoardState.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Model/BoardStateTests.cs
git commit -m "Add IsHiddenTile/Revealed to TileCell, PeekedTileId to BoardState"
```

---

### Task 2: `HiddenTileSelector`

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Domain/Gameplay/HiddenTileSelector.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/HiddenTileSelectorTests.cs`

**Interfaces:**
- Consumes: `BoardState.Cells` (`Dictionary<string, TileCell>`, from Task 1),
  `TileCell.IsHiddenTile`/`Revealed` (Task 1).
- Produces: `HiddenTileSelector.Apply(BoardState board, int difficulty, System.Random random)` — later called from `GameController.PrepareLevel` (Task 4).

- [ ] **Step 1: Write the failing tests**

Create `unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/HiddenTileSelectorTests.cs`:

```csharp
using System;
using System.Linq;
using NUnit.Framework;
using GameDomain.Gameplay;
using GameDomain.Model;

namespace GameDomain.Tests.Gameplay
{
    public class HiddenTileSelectorTests
    {
        private static BoardState BoardWithCells(int count)
        {
            var board = new BoardState();
            for (int i = 0; i < count; i++)
                board.Cells["s" + i] = new TileCell { Value = "0" };
            return board;
        }

        [Test]
        public void Apply_DifficultyBelow4_MarksNoCellsHidden()
        {
            var board = BoardWithCells(20);
            HiddenTileSelector.Apply(board, difficulty: 3, new Random(1));

            Assert.That(board.Cells.Values.Any(c => c.IsHiddenTile), Is.False);
            Assert.That(board.Cells.Values.All(c => c.Revealed), Is.True);
        }

        [Test]
        public void Apply_Difficulty4_MarksExactlyThirtyFivePercentHidden()
        {
            var board = BoardWithCells(100);
            HiddenTileSelector.Apply(board, difficulty: 4, new Random(1));

            int hiddenCount = board.Cells.Values.Count(c => c.IsHiddenTile);
            Assert.That(hiddenCount, Is.EqualTo(35));
        }

        [Test]
        public void Apply_HiddenCells_StartNotRevealed_OthersStayRevealed()
        {
            var board = BoardWithCells(20);
            HiddenTileSelector.Apply(board, difficulty: 5, new Random(2));

            foreach (var cell in board.Cells.Values)
                Assert.That(cell.Revealed, Is.EqualTo(!cell.IsHiddenTile));
        }

        [Test]
        public void Apply_Difficulty5_AlsoMarksCellsHidden()
        {
            var board = BoardWithCells(20);
            HiddenTileSelector.Apply(board, difficulty: 5, new Random(3));

            Assert.That(board.Cells.Values.Any(c => c.IsHiddenTile), Is.True);
        }
    }
}
```

- [ ] **Step 2: Verify the tests fail to compile**

Same refresh + `get_console_logs` check as Task 1 Step 2 — expect
`CS0103: The name 'HiddenTileSelector' does not exist`.

- [ ] **Step 3: Implement**

Create `unity/GameClient/Assets/Scripts/Domain/Gameplay/HiddenTileSelector.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    // Marks a random subset of a generated board's tiles as face-down, on the
    // two hardest difficulty tiers only. See docs/superpowers/specs/
    // 2026-09-23-hidden-tile-reveal-design.md for the full design.
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

        // Fisher-Yates, same pattern as PaletteSelector.Shuffle.
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

- [ ] **Step 4: Run tests, verify pass**

Same as Task 1 Step 4. Expect the 4 new tests passing, total count up by 4.

- [ ] **Step 5: Commit** (hold until told to)

```bash
git add unity/GameClient/Assets/Scripts/Domain/Gameplay/HiddenTileSelector.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/HiddenTileSelectorTests.cs
git commit -m "Add HiddenTileSelector: random face-down tile selection for difficulty 4+"
```

---

### Task 3: `TileReveal`

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Domain/Gameplay/TileReveal.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/TileRevealTests.cs`

**Interfaces:**
- Consumes: `BoardState.PeekedTileId`, `TileCell.Revealed` (Task 1);
  `FreedomRuleCalculator.IsFree(TileSlot, HashSet<string>)` (existing, in
  `GameDomain.Generation`); `TestLayoutShapes.BuildLayeredRowShape` (existing
  test fixture, `GameDomain.Tests.Fixtures`).
- Produces: `TileReveal.TryReveal(BoardState board, Dictionary<string, TileSlot> slotsById, string slotId, out string reHiddenSlotId) : bool` — called from `GameController.OnTileTapped` (Task 9).

- [ ] **Step 1: Write the failing tests**

Create `unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/TileRevealTests.cs`:

```csharp
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Gameplay;
using GameDomain.Model;
using GameDomain.Tests.Fixtures;

namespace GameDomain.Tests.Gameplay
{
    public class TileRevealTests
    {
        // A flat row of 4: L0_0/L0_3 are free (row ends), L0_1/L0_2 are not
        // (same fixture/positions MatchValidatorTests already uses).
        private static (BoardState board, Dictionary<string, TileSlot> slotsById) FourTileRow()
        {
            var shape = TestLayoutShapes.BuildLayeredRowShape(new[] { 4 });
            var slotsById = shape.ToDictionary(s => s.Id);
            var board = new BoardState
            {
                Cells = new Dictionary<string, TileCell>
                {
                    ["L0_0"] = new TileCell { Value = "a", Revealed = false, IsHiddenTile = true },
                    ["L0_1"] = new TileCell { Value = "b", Revealed = false, IsHiddenTile = true },
                    ["L0_2"] = new TileCell { Value = "b", Revealed = false, IsHiddenTile = true },
                    ["L0_3"] = new TileCell { Value = "a", Revealed = false, IsHiddenTile = true }
                }
            };
            return (board, slotsById);
        }

        [Test]
        public void TryReveal_FreeFaceDownTile_RevealsItAndBecomesPeeked()
        {
            var (board, slotsById) = FourTileRow();

            bool result = TileReveal.TryReveal(board, slotsById, "L0_0", out string reHidden);

            Assert.That(result, Is.True);
            Assert.That(reHidden, Is.Null);
            Assert.That(board.Cells["L0_0"].Revealed, Is.True);
            Assert.That(board.PeekedTileId, Is.EqualTo("L0_0"));
        }

        [Test]
        public void TryReveal_SecondDifferentFaceDownTile_ReHidesThePreviousPeek()
        {
            var (board, slotsById) = FourTileRow();
            TileReveal.TryReveal(board, slotsById, "L0_0", out _);

            bool result = TileReveal.TryReveal(board, slotsById, "L0_3", out string reHidden);

            Assert.That(result, Is.True);
            Assert.That(reHidden, Is.EqualTo("L0_0"));
            Assert.That(board.Cells["L0_0"].Revealed, Is.False);
            Assert.That(board.Cells["L0_3"].Revealed, Is.True);
            Assert.That(board.PeekedTileId, Is.EqualTo("L0_3"));
        }

        [Test]
        public void TryReveal_NotFreeTile_ReturnsFalseAndChangesNothing()
        {
            var (board, slotsById) = FourTileRow();

            bool result = TileReveal.TryReveal(board, slotsById, "L0_1", out string reHidden);

            Assert.That(result, Is.False);
            Assert.That(reHidden, Is.Null);
            Assert.That(board.Cells["L0_1"].Revealed, Is.False);
            Assert.That(board.PeekedTileId, Is.Null);
        }

        [Test]
        public void TryReveal_SameTileCalledTwice_StaysPeekedWithNoReHide()
        {
            var (board, slotsById) = FourTileRow();
            TileReveal.TryReveal(board, slotsById, "L0_0", out _);

            bool result = TileReveal.TryReveal(board, slotsById, "L0_0", out string reHidden);

            Assert.That(result, Is.True);
            Assert.That(reHidden, Is.Null);
            Assert.That(board.PeekedTileId, Is.EqualTo("L0_0"));
        }
    }
}
```

- [ ] **Step 2: Verify the tests fail to compile**

Same pattern as before — expect `CS0103: The name 'TileReveal' does not exist`.

- [ ] **Step 3: Implement**

Create `unity/GameClient/Assets/Scripts/Domain/Gameplay/TileReveal.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    // Owns the reveal/peek state machine for hidden (face-down) tiles. Caller
    // contract: only call TryReveal when the cell's Revealed is currently
    // false (GameController.OnTileTapped enforces this before calling in).
    // See docs/superpowers/specs/2026-09-23-hidden-tile-reveal-design.md.
    public static class TileReveal
    {
        // Attempts to reveal a currently-face-down tile. Returns false if it
        // isn't free right now (caller treats this exactly like today's
        // invalid-tap case - same shake/vibrate/sound). On success, sets
        // reHiddenSlotId to whichever OTHER tile got auto-re-hidden as a side
        // effect (null if none), so the caller knows which other tile view to
        // flip back down.
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

- [ ] **Step 4: Run tests, verify pass**

Same as before. Expect the 4 new tests passing.

- [ ] **Step 5: Commit** (hold until told to)

```bash
git add unity/GameClient/Assets/Scripts/Domain/Gameplay/TileReveal.cs \
        unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/TileRevealTests.cs
git commit -m "Add TileReveal: reveal/peek state machine for hidden tiles"
```

---

### Task 4: Wire `HiddenTileSelector` into `PrepareLevel`

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/GameController.cs:195` (inside `PrepareLevel`)

**Interfaces:**
- Consumes: `HiddenTileSelector.Apply` (Task 2).

- [ ] **Step 1: Make the change**

In `PrepareLevel`, immediately after the line that generates `_board`:

```csharp
            _board = BoardGenerator.GenerateShaped(level, rng, profile, clusters, modelCount);
```

add:

```csharp
            HiddenTileSelector.Apply(_board, difficulty, rng);
```

(`GameDomain.Gameplay` is already imported in this file — `TrayManager`/
`TrayUndo`/`TrayHintFinder` are already used from that namespace — no new
`using` needed.)

- [ ] **Step 2: Verify compile**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Check `get_console_logs` — expect zero errors.

- [ ] **Step 3: Manual verification via the connected Editor**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json editor_play
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var gc = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.GameController>(); var f = typeof(GameClient.Presentation.GameController).GetField(\"_currentLevelId\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); f.SetValue(gc, 5); gc.ClearBoard(); gc.PrepareLevel(); var bf = typeof(GameClient.Presentation.GameController).GetField(\"_board\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); var board = (GameDomain.Model.BoardState)bf.GetValue(gc); int hidden = 0; foreach (var c in board.Cells.Values) if (c.IsHiddenTile) hidden++; UnityEngine.Debug.Log(\"HIDDEN_COUNT=\" + hidden + \" TOTAL=\" + board.Cells.Count);"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json get_console_logs
```

Expected: a `HIDDEN_COUNT=` log with a positive count roughly 35% of `TOTAL`
(level 5 is difficulty 5 → the selector should apply). Then stop play mode:
```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json editor_stop
```

- [ ] **Step 4: Commit** (hold until told to)

```bash
git add unity/GameClient/Assets/Scripts/Presentation/GameController.cs
git commit -m "Call HiddenTileSelector.Apply after board generation"
```

---

### Task 5: `TileVisual.BackIcon` + `TileSetAsset.CardBack`

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Data/TileSetAsset.cs`
- Modify: `unity/GameClient/Assets/Scripts/Presentation/Board/TileVisual.cs`

**Interfaces:**
- Produces: `TileSetAsset.CardBack` (`Sprite`), `TileVisual.BackIcon(TileSetAsset tileSet) : Sprite` — consumed by `BoardView3D` (Task 8) and `GameController` (Task 9).

No domain logic here (pure Unity `Sprite` plumbing, not unit-testable and not
worth a test) — verified visually once Task 6 provides the actual asset and
Task 8/9 wire it in.

- [ ] **Step 1: Add the field to `TileSetAsset`**

In `unity/GameClient/Assets/Scripts/Data/TileSetAsset.cs`, add after
`SimilarityClusterId`:

```csharp
        // Shared "face-down" card sprite for hidden tiles (see
        // HiddenTileSelector / TileReveal) - one generic sprite, not
        // per-value, since the value is exactly what's concealed. Generated
        // via Tools/Mahjong/Generate Tile Back (TileMaterialGenerator).
        public Sprite CardBack;
```

- [ ] **Step 2: Add `BackIcon` to `TileVisual`**

In `unity/GameClient/Assets/Scripts/Presentation/Board/TileVisual.cs`, add
alongside `IconFor`:

```csharp
        // The shared face-down sprite for hidden tiles - not value-indexed,
        // since there is exactly one "back" regardless of what's underneath.
        public static Sprite BackIcon(TileSetAsset tileSet) => tileSet.CardBack;
```

- [ ] **Step 3: Verify compile**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Check `get_console_logs` for zero errors.

- [ ] **Step 4: Commit** (hold until told to)

```bash
git add unity/GameClient/Assets/Scripts/Data/TileSetAsset.cs \
        unity/GameClient/Assets/Scripts/Presentation/Board/TileVisual.cs
git commit -m "Add TileSetAsset.CardBack and TileVisual.BackIcon"
```

---

### Task 6: Generate the card-back texture and wire it into `DefaultTileSet`

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Editor/TileMaterialGenerator.cs`

**Interfaces:**
- Consumes: `TileFaceTexture.Build` (existing, unchanged), `TileSetAsset.CardBack` (Task 5).
- Produces: `Assets/Sprites/Tiles/TileBack.png` (new asset), `DefaultTileSet.asset.CardBack` populated.

- [ ] **Step 1: Add the generator method**

In `unity/GameClient/Assets/Scripts/Editor/TileMaterialGenerator.cs`, add
`using GameClient.Data;` to the top usings, then add this method alongside the
existing `Generate()`:

```csharp
        [MenuItem("Tools/Mahjong/Generate Tile Back")]
        public static void GenerateCardBack()
        {
            Directory.CreateDirectory("Assets/Sprites/Tiles");

            const int texW = 512;
            int texH = Mathf.RoundToInt(texW / CardStyle.CardAspectRatio);
            // Inverted contrast from the front face (solid jade fill, thin
            // ivory frame, no icon) - reads as clearly "not a value card" at a
            // glance without needing bespoke art.
            var tex = TileFaceTexture.Build(texW, texH, Jade, Jade, IvoryTop,
                framePadding: 0.028f, frameThickness: 0.011f, cornerRadius: 0.15f,
                bevelStrength: 0.45f, sheenStrength: 0.05f);
            File.WriteAllBytes("Assets/Sprites/Tiles/TileBack.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset("Assets/Sprites/Tiles/TileBack.png");

            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Sprites/Tiles/TileBack.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsToUnits = 100;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();

            var tileSetAsset = AssetDatabase.LoadAssetAtPath<TileSetAsset>("Assets/Data/DefaultTileSet.asset");
            if (tileSetAsset != null)
            {
                tileSetAsset.CardBack = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Tiles/TileBack.png");
                EditorUtility.SetDirty(tileSetAsset);
                AssetDatabase.SaveAssets();
            }

            Debug.Log("TILE_BACK_GENERATOR_DONE");
        }
```

- [ ] **Step 2: Run it via the connected Editor**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
# wait for compile, then:
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json clear_console
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --detach eval -- "TileMaterialGenerator.GenerateCardBack();"
# poll get_console_logs for "TILE_BACK_GENERATOR_DONE"
```

- [ ] **Step 3: Verify the asset and wiring**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var ts = UnityEditor.AssetDatabase.LoadAssetAtPath<GameClient.Data.TileSetAsset>(\"Assets/Data/DefaultTileSet.asset\"); UnityEngine.Debug.Log(\"CARD_BACK_SET=\" + (ts.CardBack != null));"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json get_console_logs
```
Expected: `CARD_BACK_SET=True`.

- [ ] **Step 4: Commit** (hold until told to — note this stages a new binary asset)

```bash
git add unity/GameClient/Assets/Scripts/Editor/TileMaterialGenerator.cs \
        unity/GameClient/Assets/Sprites/Tiles/TileBack.png \
        unity/GameClient/Assets/Sprites/Tiles/TileBack.png.meta \
        unity/GameClient/Assets/Data/DefaultTileSet.asset
git commit -m "Bake a card-back texture and wire it into DefaultTileSet"
```

---

### Task 7: `TileView3D` flip animations

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/Board3D/TileView3D.cs`

**Interfaces:**
- Produces: `TileView3D.PlayFlipToFaceUp(Sprite frontSprite)`,
  `TileView3D.PlayFlipToFaceDown(Sprite backSprite)` — called from
  `GameController.OnTileTapped` (Task 9).

No unit test (pure `MonoBehaviour`/coroutine presentation code, same category
as every other `TileView3D` animation in this file) — verified visually via the
connected Editor in Task 12.

- [ ] **Step 1: Extract the existing fit-scale logic into a shared helper**

In `Initialize`, replace:

```csharp
            if (tileSprite != null)
            {
                // Dynamically scale the sprite so its world width is precisely 0.626f.
                // This perfectly matches the _cellWidth logic in BoardView3D and prevents 
                // the horizontal overlapping seen when sprites are naturally too wide.
                float targetWidth = 0.626f;
                float spriteWidthUnits = tileSprite.bounds.size.x;
                float scale = spriteWidthUnits > 0 ? (targetWidth / spriteWidthUnits) : 1f;
                _bodyRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            }
```

with:

```csharp
            ApplyFitScale(tileSprite);
```

Add the helper (and its backing constant) near the top of the class, replacing
nothing — new code only:

```csharp
        // Dynamically scale a sprite so its world width is precisely 0.626f -
        // matches the _cellWidth logic in BoardView3D and prevents horizontal
        // overlap. Shared by Initialize and the flip routines below so
        // swapping between the front icon and the back sprite (which may not
        // share exactly the same source aspect) always re-fits correctly
        // instead of carrying over a stale scale from whichever sprite was
        // showing before.
        private const float TargetWidth = 0.626f;

        private void ApplyFitScale(Sprite sprite)
        {
            if (sprite == null) return;
            float spriteWidthUnits = sprite.bounds.size.x;
            float scale = spriteWidthUnits > 0 ? (TargetWidth / spriteWidthUnits) : 1f;
            _bodyRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }
```

- [ ] **Step 2: Add the flip coroutine field and public methods**

Add `private Coroutine _flipCoroutine;` alongside the other coroutine fields
(`_shakeCoroutine`, `_clearCoroutine`, etc.).

Add near `PlayShake`/`ShakeRoutine`:

```csharp
        private const float FlipHalfDuration = 0.15f;

        public void PlayFlipToFaceUp(Sprite frontSprite)
        {
            if (_flipCoroutine != null) StopCoroutine(_flipCoroutine);
            _flipCoroutine = StartCoroutine(FlipRoutine(frontSprite));
        }

        public void PlayFlipToFaceDown(Sprite backSprite)
        {
            if (_flipCoroutine != null) StopCoroutine(_flipCoroutine);
            _flipCoroutine = StartCoroutine(FlipRoutine(backSprite));
        }

        // Classic card-flip: scale X to zero (edge-on), swap the sprite at
        // the midpoint, then scale back out. ApplyFitScale is recomputed for
        // the NEW sprite so the front/back don't need identical source aspect.
        private IEnumerator FlipRoutine(Sprite newSprite)
        {
            var t = _bodyRenderer.transform;
            float startX = t.localScale.x;
            float elapsed = 0f;
            while (elapsed < FlipHalfDuration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / FlipHalfDuration);
                t.localScale = new Vector3(Mathf.Lerp(startX, 0f, p), t.localScale.y, t.localScale.z);
                yield return null;
            }

            _bodyRenderer.sprite = newSprite;
            ApplyFitScale(newSprite);
            float targetX = t.localScale.x;
            t.localScale = new Vector3(0f, t.localScale.y, t.localScale.z);

            elapsed = 0f;
            while (elapsed < FlipHalfDuration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / FlipHalfDuration);
                t.localScale = new Vector3(Mathf.Lerp(0f, targetX, p), t.localScale.y, t.localScale.z);
                yield return null;
            }
            t.localScale = new Vector3(targetX, t.localScale.y, t.localScale.z);
            _flipCoroutine = null;
        }
```

- [ ] **Step 3: Verify compile**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Check `get_console_logs` for zero errors.

- [ ] **Step 4: Run the full EditMode suite to confirm no regression**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: same pass count as before this task (this change touches only
`TileView3D`, which has no EditMode tests — this run just confirms nothing
else broke).

- [ ] **Step 5: Commit** (hold until told to)

```bash
git add unity/GameClient/Assets/Scripts/Presentation/Board3D/TileView3D.cs
git commit -m "Add flip-to-face-up/down animations to TileView3D"
```

---

### Task 8: `BoardView3D` — show the back sprite for unrevealed cells

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/Board3D/BoardView3D.cs:168` (in `Build`)
- Modify: `unity/GameClient/Assets/Scripts/Presentation/Board3D/BoardView3D.cs:384` (in `RestoreTile`)

**Interfaces:**
- Consumes: `TileVisual.BackIcon` (Task 5), `TileCell.Revealed` (Task 1).

- [ ] **Step 1: Update `Build`**

Replace:

```csharp
                view.Initialize(slot.Id, slot.Layer, TileVisual.IconFor(_tileSet, kv.Value.Value));
```

with:

```csharp
                var cell = kv.Value;
                Sprite startSprite = cell.Revealed
                    ? TileVisual.IconFor(_tileSet, cell.Value)
                    : TileVisual.BackIcon(_tileSet);
                view.Initialize(slot.Id, slot.Layer, startSprite);
```

- [ ] **Step 2: Update `RestoreTile`**

Replace:

```csharp
            view.Initialize(slot.Id, slot.Layer, TileVisual.IconFor(_tileSet, cell.Value));
```

with:

```csharp
            Sprite startSprite = cell.Revealed
                ? TileVisual.IconFor(_tileSet, cell.Value)
                : TileVisual.BackIcon(_tileSet);
            view.Initialize(slot.Id, slot.Layer, startSprite);
```

(`RestoreTile` already has `cell` in scope from
`board.Cells.TryGetValue(slotId, out var cell)` a few lines above.)

- [ ] **Step 3: Verify compile**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Check `get_console_logs` for zero errors.

- [ ] **Step 4: Visual verification**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json editor_play
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var gc = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.GameController>(); var f = typeof(GameClient.Presentation.GameController).GetField(\"_currentLevelId\", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); f.SetValue(gc, 5); gc.ClearBoard(); gc.BeginLevel();"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var s = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.HUD3D.LevelStartScreen3D>(UnityEngine.FindObjectsInactive.Include); if (s != null) s.gameObject.SetActive(false);"
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json capture_game_view
```
Decode the returned base64 `result.data.base64` to a PNG (same approach used
throughout this session) and look at it: some tiles on the difficulty-5 board
should show the jade card-back instead of an icon. Then:
```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json editor_stop
```

- [ ] **Step 5: Commit** (hold until told to)

```bash
git add unity/GameClient/Assets/Scripts/Presentation/Board3D/BoardView3D.cs
git commit -m "BoardView3D shows the card back for unrevealed hidden tiles"
```

---

### Task 9: `GameController.OnTileTapped` — the reveal branch

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/GameController.cs:236-263` (`OnTileTapped`)

**Interfaces:**
- Consumes: `TileReveal.TryReveal` (Task 3), `TileView3D.PlayFlipToFaceUp`/`PlayFlipToFaceDown` (Task 7), `TileVisual.IconFor`/`BackIcon` (Task 5).

- [ ] **Step 1: Make the change**

Replace the full body of `OnTileTapped`:

```csharp
        public void OnTileTapped(string slotId)
        {
            if (_paused) return;
            if (IsInputLocked) return;

            if (_board.IsGameOver) return;
            if (_board.Cells.Values.All(c => c.Cleared)) return;

            var oldTray = new List<string>(_board.TrayTileIds);

            // TrayManager runs the freedom check itself (excluding tray tiles) and
            // rejects covered tiles, a full tray, or an already-collected tile.
            if (!TrayManager.TryPushToTray(_board, _slotsById, slotId))
            {
                _boardView.GetTileView(slotId)?.PlayShake();
#if UNITY_ANDROID || UNITY_IOS
                if (SaveSystem.LoadSettings().VibrationOn) Handheld.Vibrate();
#endif
                if (_invalidTapClip != null && _audioSource != null)
                    _audioSource.PlayOneShot(_invalidTapClip);
                return;
            }



            var newTray = new List<string>(_board.TrayTileIds);
            StartCoroutine(AnimateTapToTray(slotId, oldTray, newTray));
        }
```

with:

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
                _boardView.GetTileView(slotId)?.PlayShake();
#if UNITY_ANDROID || UNITY_IOS
                if (SaveSystem.LoadSettings().VibrationOn) Handheld.Vibrate();
#endif
                if (_invalidTapClip != null && _audioSource != null)
                    _audioSource.PlayOneShot(_invalidTapClip);
                return;
            }

            var oldTray = new List<string>(_board.TrayTileIds);

            // TrayManager runs the freedom check itself (excluding tray tiles) and
            // rejects covered tiles, a full tray, or an already-collected tile.
            if (!TrayManager.TryPushToTray(_board, _slotsById, slotId))
            {
                _boardView.GetTileView(slotId)?.PlayShake();
#if UNITY_ANDROID || UNITY_IOS
                if (SaveSystem.LoadSettings().VibrationOn) Handheld.Vibrate();
#endif
                if (_invalidTapClip != null && _audioSource != null)
                    _audioSource.PlayOneShot(_invalidTapClip);
                return;
            }

            if (_board.PeekedTileId == slotId) _board.PeekedTileId = null;

            var newTray = new List<string>(_board.TrayTileIds);
            StartCoroutine(AnimateTapToTray(slotId, oldTray, newTray));
        }
```

- [ ] **Step 2: Verify compile**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Check `get_console_logs` for zero errors.

- [ ] **Step 3: Run the full EditMode suite**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: same pass count as the end of Task 3 (this task only touches
`GameController`, which has no direct EditMode tests) — confirms no
regression elsewhere.

- [ ] **Step 4: Manual end-to-end verification**

In the connected Editor: enter play mode, force a difficulty-5 level (same
reflection trick as Task 4/8), begin the level, then tap a free tile that is
currently a card-back (identify one from the `capture_game_view` screenshot),
confirm via `get_console_logs`/a follow-up screenshot that it flips face-up and
the tray is untouched; tap a *different* free card-back tile and confirm the
first one flips back down; tap the newly-peeked tile again and confirm it
flies to the tray as normal. Stop play mode when done.

- [ ] **Step 5: Commit** (hold until told to)

```bash
git add unity/GameClient/Assets/Scripts/Presentation/GameController.cs
git commit -m "OnTileTapped: reveal face-down tiles before allowing tray collection"
```

---

### Task 10: Undo restores concealment

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/GameController.cs` (`AnimateUndo`)

**Interfaces:**
- Consumes: `TileCell.IsHiddenTile`/`Revealed` (Task 1), existing `TrayUndo.TryUndo`/`_boardView.RestoreTile` (already wired; `RestoreTile` now respects `Revealed` per Task 8).

- [ ] **Step 1: Make the change**

In `AnimateUndo`, replace:

```csharp
            int trayIndex = _board.TrayTileIds.Count - 1;
            string popped = TrayUndo.TryUndo(_board);
            if (popped == null)
            {
                IsInputLocked = false;
                yield break;
            }

            _aidsUsed++;
```

with:

```csharp
            int trayIndex = _board.TrayTileIds.Count - 1;
            string popped = TrayUndo.TryUndo(_board);
            if (popped == null)
            {
                IsInputLocked = false;
                yield break;
            }

            // Undoing a hidden tile fully reverses the reveal too, not just
            // the collection - it goes back to face-down.
            if (_board.Cells[popped].IsHiddenTile) _board.Cells[popped].Revealed = false;

            _aidsUsed++;
```

This runs before the later `_boardView.RestoreTile(popped, _board)` call
already in this method, so `RestoreTile` (Task 8) picks the back sprite
correctly.

- [ ] **Step 2: Verify compile**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Check `get_console_logs` for zero errors.

- [ ] **Step 3: Manual verification**

In the connected Editor: force a difficulty-5 level, tap a hidden tile to
reveal it, tap it again to collect it (confirm it lands in the tray), then
call `OnUndoRequested` and confirm via a screenshot that the tile returns to
the board showing the **card back**, not its revealed front.

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var gc = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.GameController>(); gc.OnUndoRequested();"
```

- [ ] **Step 4: Commit** (hold until told to)

```bash
git add unity/GameClient/Assets/Scripts/Presentation/GameController.cs
git commit -m "Undo restores a hidden tile back to face-down"
```

---

### Task 11: Shuffle resets concealment state

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/GameController.cs` (`OnShuffleRequested`)

**Interfaces:**
- Consumes: `BoardState.PeekedTileId`, `TileCell.Revealed` (Task 1).

- [ ] **Step 1: Make the change**

In `OnShuffleRequested`, replace:

```csharp
    // Attempt shuffle; if no shuffles left, abort.
    bool shuffled = ShuffleService.Shuffle(_board, _shape, _random);
    if (!shuffled) return;

    // Record shuffle usage as an aid.
    _aidsUsed++;
```

with:

```csharp
    // Attempt shuffle; if no shuffles left, abort.
    bool shuffled = ShuffleService.Shuffle(_board, _shape, _random);
    if (!shuffled) return;

    // A shuffle reassigns values across every uncleared on-board cell,
    // including under a currently-peeked hidden tile - reset concealment so
    // it doesn't keep showing a reveal of a value that no longer belongs to
    // that slot.
    if (_board.PeekedTileId != null)
    {
        _board.Cells[_board.PeekedTileId].Revealed = false;
        _board.PeekedTileId = null;
    }

    // Record shuffle usage as an aid.
    _aidsUsed++;
```

The subsequent `_boardView.Build(_board, _slotsById, animateDealIn: true, ...)`
call already in this method fully rebuilds every tile view from `_board.Cells`
(Task 8's conditional sprite selection), so the reset above is all that's
needed on the domain side.

- [ ] **Step 2: Verify compile**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json menu -- "Assets/Refresh"
```
Check `get_console_logs` for zero errors.

- [ ] **Step 3: Manual verification**

In the connected Editor: force a difficulty-5 level, reveal a hidden tile,
then call shuffle and confirm (via `get_console_logs` inspecting
`_board.PeekedTileId` through eval, or a screenshot) that no tile is left
stuck showing a stale reveal.

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json eval -- "var gc = UnityEngine.Object.FindFirstObjectByType<GameClient.Presentation.GameController>(); gc.OnShuffleRequested();"
```

- [ ] **Step 4: Commit** (hold until told to)

```bash
git add unity/GameClient/Assets/Scripts/Presentation/GameController.cs
git commit -m "Shuffle resets any currently-peeked hidden tile"
```

---

### Task 12: Full regression pass + on-device verification

**Files:** none (verification only).

- [ ] **Step 1: Full EditMode suite**

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --timeout 180 run_tests --
```
Expected: 100% pass, including every test added in Tasks 1-3, and no
regressions in `SolvabilityRegressionTests` or any other existing suite (per
spec §2/§6 — this feature cannot affect solvability by construction, this run
confirms it).

- [ ] **Step 2: Build and install on the connected device**

Same build/install/launch flow used throughout this session:

```bash
unity command --project-path /Users/vatsalpatadiya/Desktop/Game/unity/GameClient --format json --detach eval -- "AndroidBuilder.Build();"
# poll Builds/GameClient.apk mtime until it updates (IL2CPP build takes several minutes)
adb devices
adb -s <device-id> install -r /Users/vatsalpatadiya/Desktop/Game/unity/GameClient/Builds/GameClient.apk
adb -s <device-id> shell monkey -p com.gameclient.mahjong -c android.intent.category.LAUNCHER 1
adb -s <device-id> logcat -d -t 60 | grep -iE "fatal|AndroidRuntime"
```
Expected: clean launch, no fatal errors.

- [ ] **Step 3: Manual on-device playthrough**

Play up to a difficulty-4+ level (or use the level-select/debug path already
established this session to jump ahead) and confirm on the real device:
face-down tiles are visually distinct (jade card back), first tap reveals,
second tap collects, revealing a different hidden tile re-hides the previous
one, hint highlights a face-down tile without revealing it, undo restores
concealment, shuffle doesn't leave a stale reveal.

- [ ] **Step 4: Report results to the user**

Summarize pass/fail for each item in Step 3, and hold all commits from Tasks
1-11 until the user explicitly approves committing (per this session's
standing convention).
