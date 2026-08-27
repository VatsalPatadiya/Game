# Tile, HUD & Control Bar Visual Styling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle tiles, the score HUD, and the Hint/Undo/Shuffle control bar so tiles read as icon+color cards with depth, the HUD reads as a calm status bar, and the three control buttons show equal-weight circular icons with a live remaining-uses badge — without adding any leveling/meta-progression system.

**Architecture:** This is a Unity 6 project driven entirely by headless Editor scripts (`Assets/Scripts/Editor/*.cs`, run via `Unity -batchmode -executeMethod X -quit`) that (re)generate prefabs, ScriptableObject data assets, and the scene from code — there is no interactive Editor session in this workflow. All new art (7 tile icons, 3 HUD glyphs) is generated procedurally as signed-distance-field silhouettes rasterized to PNG at Editor time, since no external art pipeline exists. Icon shape + accent color are combined combinatorially (7 icons × 4 colors = 28 combos) so every one of the up to 26 distinct pair-values a board can generate renders as a unique icon+color pair — this also fixes a latent bug where `TileView` truncated two-digit values to their first character.

**Tech Stack:** Unity 6000.5.9f1, C# (Domain assembly has `noEngineReferences: true` — no `UnityEngine` types allowed in `Assets/Scripts/Domain/**`), Unity Test Framework (NUnit) for EditMode tests, legacy uGUI (`UnityEngine.UI`) for the Canvas HUD, `SpriteRenderer`/2D physics for the world-space board.

**Spec:** the "UI Visual Design Spec — Tile, HUD & Control Bar Styling" provided in conversation (visual/structural styling only — no leveling, no meta-progression, no level-lock behavior).

## Global Constraints

- No leveling, meta-progression, or level-lock UI of any kind (explicit spec exclusion).
- Every tile must render icon **and** color — never color alone (existing accessibility rule, `unity/README.md` "Known placeholder-art gaps").
- All interactive elements keep a minimum 72–88dp effective tap target; visual card/button art may be smaller than the tap target, never larger.
- WCAG AA contrast (≥4.5:1) between text/icons and their background — verified below with exact RGB values, not eyeballed.
- No artwork/characters/branded iconography copied from any reference screenshot — only original procedurally-generated shapes (flower, leaf, star, diamond, ring, dot-cluster, cross).
- Background moves from flat gray-blue to deep muted green `#2A3D30` (42, 61, 48) — user-selected direction.
- Hint/Undo/Shuffle each get a real per-level resource cap of **3 uses**, reset on level load/restart — user-selected approach; this is a simple per-board counter, not a meta-progression system.
- Verified palette (all values are `sRGB 0–255`, contrast computed via the WCAG relative-luminance formula):
  - Board background: `#2A3D30` (42, 61, 48)
  - Card face / HUD pill / button face (off-white): `#F7F4EB` (247, 244, 235) — contrast vs. board: **10.56:1**
  - Accent/icon colors (all measured against the card face `#F7F4EB`): terracotta `#B04228` (176,66,40) **5.22:1**, dark amber `#965C00` (150,92,0) **4.99:1**, teal `#126161` (18,97,97) **6.57:1**, plum `#5E3385` (94,51,133) **8.35:1**
  - Selection glow (unchanged from existing `AccessibilityTokens.HighlightColor`): `#FFD933` (255,217,51) — contrast vs. board: **8.42:1**
  - HUD/button dark text-icon color: `#282E24` (40,46,36) — contrast vs. card face: **12.67:1**
  - Badge circle: terracotta `#B04228` with white text — contrast **5.74:1**

---

## Task 1: Domain layer — per-level remaining-uses counters

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Domain/Model/BoardState.cs`
- Modify: `unity/GameClient/Assets/Scripts/Domain/Gameplay/UndoStack.cs`
- Modify: `unity/GameClient/Assets/Scripts/Domain/Gameplay/ShuffleService.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Model/BoardStateTests.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/UndoStackTests.cs`
- Test: `unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/ShuffleServiceTests.cs`

**Interfaces:**
- Produces: `BoardState.HintsRemaining`, `BoardState.UndosRemaining`, `BoardState.ShufflesRemaining` (all `int`, default `3`) — consumed by Task 2 (`GameController`).
- Produces: `UndoStack.TryUndo(BoardState)` now returns `false` (no mutation) when `UndosRemaining <= 0`, otherwise decrements it on success — same signature as before.
- Produces: `ShuffleService.Shuffle(BoardState, List<TileSlot>, Random, int maxRestarts = 50)` changes return type from `void` to `bool` (`false` when `ShufflesRemaining <= 0`, `true` and decremented on success, still throws `BoardGenerationException` if no solvable arrangement is found) — consumed by Task 2.

- [ ] **Step 1: Add the three counter fields to `BoardState`**

Edit `unity/GameClient/Assets/Scripts/Domain/Model/BoardState.cs`:

```csharp
using System.Collections.Generic;

namespace GameDomain.Model
{
    public sealed class BoardState
    {
        public int LevelId;
        public Dictionary<string, TileCell> Cells = new Dictionary<string, TileCell>();
        public List<Move> MoveHistory = new List<Move>();

        public List<string> TrayTileIds = new List<string>();
        public int MaxTraySize = 4;
        public bool IsGameOver = false;

        public int Score;
        public int ComboCount;

        public int HintsRemaining = 3;
        public int UndosRemaining = 3;
        public int ShufflesRemaining = 3;
    }
}
```

- [ ] **Step 2: Write the failing tests for the new defaults**

Edit `unity/GameClient/Assets/Scripts/Tests/EditMode/Model/BoardStateTests.cs`, add a new test method inside the class:

```csharp
        [Test]
        public void NewBoardState_DefaultsToThreeUsesOfEachControl()
        {
            var board = new BoardState();

            Assert.That(board.HintsRemaining, Is.EqualTo(3));
            Assert.That(board.UndosRemaining, Is.EqualTo(3));
            Assert.That(board.ShufflesRemaining, Is.EqualTo(3));
        }
```

- [ ] **Step 3: Update `UndoStack` to gate and decrement**

Edit `unity/GameClient/Assets/Scripts/Domain/Gameplay/UndoStack.cs`:

```csharp
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    public static class UndoStack
    {
        public static bool TryUndo(BoardState board)
        {
            if (board.UndosRemaining <= 0)
                return false;

            if (board.MoveHistory.Count == 0)
                return false;

            var lastMove = board.MoveHistory[board.MoveHistory.Count - 1];
            board.MoveHistory.RemoveAt(board.MoveHistory.Count - 1);

            board.Cells[lastMove.SlotIdA].Cleared = false;
            board.Cells[lastMove.SlotIdB].Cleared = false;

            board.UndosRemaining -= 1;

            return true;
        }
    }
}
```

- [ ] **Step 4: Add the failing tests for `UndoStack`'s gate**

Edit `unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/UndoStackTests.cs`, add inside the class:

```csharp
        [Test]
        public void TryUndo_NoUndosRemaining_ReturnsFalseAndDoesNotRestore()
        {
            var board = new BoardState
            {
                UndosRemaining = 0,
                Cells = new Dictionary<string, TileCell>
                {
                    ["L0_0"] = new TileCell { Value = "a", Cleared = true },
                    ["L0_3"] = new TileCell { Value = "a", Cleared = true }
                },
                MoveHistory = new List<Move>
                {
                    new Move { SlotIdA = "L0_0", SlotIdB = "L0_3", ValueA = "a", ValueB = "a" }
                }
            };

            bool result = UndoStack.TryUndo(board);

            Assert.That(result, Is.False);
            Assert.That(board.Cells["L0_0"].Cleared, Is.True);
            Assert.That(board.MoveHistory.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryUndo_WithPriorMove_DecrementsUndosRemaining()
        {
            var board = new BoardState
            {
                Cells = new Dictionary<string, TileCell>
                {
                    ["L0_0"] = new TileCell { Value = "a", Cleared = true },
                    ["L0_3"] = new TileCell { Value = "a", Cleared = true }
                },
                MoveHistory = new List<Move>
                {
                    new Move { SlotIdA = "L0_0", SlotIdB = "L0_3", ValueA = "a", ValueB = "a" }
                }
            };

            UndoStack.TryUndo(board);

            Assert.That(board.UndosRemaining, Is.EqualTo(2));
        }
```

- [ ] **Step 5: Update `ShuffleService` to gate, decrement, and return `bool`**

Edit `unity/GameClient/Assets/Scripts/Domain/Gameplay/ShuffleService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Gameplay
{
    public static class ShuffleService
    {
        public static bool Shuffle(BoardState board, List<TileSlot> shape, Random random, int maxRestarts = 50)
        {
            if (board.ShufflesRemaining <= 0)
                return false;

            var slotsById = shape.ToDictionary(s => s.Id);
            var remainingIds = new HashSet<string>(
                board.Cells.Where(kv => !kv.Value.Cleared).Select(kv => kv.Key));

            for (int attempt = 0; attempt < maxRestarts; attempt++)
            {
                var removalOrder = ReverseConstructionSolver.TryBuildRemovalOrder(slotsById, remainingIds, random);
                if (removalOrder == null)
                    continue;

                var values = ReverseConstructionSolver.AssignValuesFromRemovalOrder(removalOrder, random);

                foreach (var id in remainingIds)
                {
                    board.Cells[id].Value = values[id];
                }

                board.ShufflesRemaining -= 1;
                return true;
            }

            throw new BoardGenerationException(
                "Could not find a solvable shuffle for level " + board.LevelId + " after " + maxRestarts + " attempts.");
        }
    }
}
```

Note: existing call sites in `ShuffleServiceTests.cs` invoke `ShuffleService.Shuffle(...)` as a bare statement — a `bool`-returning method called as a statement compiles unchanged, so no existing test needs edits for that alone.

- [ ] **Step 6: Add the failing tests for `ShuffleService`'s gate**

Edit `unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/ShuffleServiceTests.cs`, add inside the class:

```csharp
        [Test]
        public void Shuffle_Succeeds_DecrementsShufflesRemaining()
        {
            var shape = TestLayoutShapes.MediumShape();
            var level = new LevelDefinition { LevelId = 1, Shape = shape, TileSetId = "test" };
            var board = BoardGenerator.Generate(level, new Random(11));

            bool result = ShuffleService.Shuffle(board, shape, new Random(99));

            Assert.That(result, Is.True);
            Assert.That(board.ShufflesRemaining, Is.EqualTo(2));
        }

        [Test]
        public void Shuffle_NoShufflesRemaining_ReturnsFalseAndLeavesValuesUnchanged()
        {
            var shape = TestLayoutShapes.MediumShape();
            var level = new LevelDefinition { LevelId = 1, Shape = shape, TileSetId = "test" };
            var board = BoardGenerator.Generate(level, new Random(11));
            board.ShufflesRemaining = 0;
            var valuesBefore = board.Cells.ToDictionary(kv => kv.Key, kv => kv.Value.Value);

            bool result = ShuffleService.Shuffle(board, shape, new Random(99));

            Assert.That(result, Is.False);
            var valuesAfter = board.Cells.ToDictionary(kv => kv.Key, kv => kv.Value.Value);
            Assert.That(valuesAfter, Is.EqualTo(valuesBefore));
        }
```

- [ ] **Step 7: Run the EditMode tests and verify everything passes**

Run:
```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -runTests -testPlatform EditMode -testResults /tmp/results-task1.xml -quit
grep -E "test-run|failures=\"[^0]\"" /tmp/results-task1.xml | head -5
```
Expected: the summary `test-run` line shows `failures="0"`.

- [ ] **Step 8: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Domain/Model/BoardState.cs \
  unity/GameClient/Assets/Scripts/Domain/Gameplay/UndoStack.cs \
  unity/GameClient/Assets/Scripts/Domain/Gameplay/ShuffleService.cs \
  unity/GameClient/Assets/Scripts/Tests/EditMode/Model/BoardStateTests.cs \
  unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/UndoStackTests.cs \
  unity/GameClient/Assets/Scripts/Tests/EditMode/Gameplay/ShuffleServiceTests.cs
git commit -m "feat: add 3-use-per-level cap to hint/undo/shuffle"
```

---

## Task 2: `GameController` — wire remaining-uses gating and broadcast event

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/GameController.cs`

**Interfaces:**
- Consumes: `BoardState.HintsRemaining/UndosRemaining/ShufflesRemaining` (Task 1), `UndoStack.TryUndo(BoardState) : bool` (Task 1), `ShuffleService.Shuffle(...) : bool` (Task 1).
- Produces: `event Action<int, int, int> UsesChanged` (params: hintsRemaining, undosRemaining, shufflesRemaining) — consumed by Task 7 (`HintButton`/`UndoButton`/`ShuffleButton`).

- [ ] **Step 1: Add the event and gating logic**

Edit `unity/GameClient/Assets/Scripts/Presentation/GameController.cs` in full:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using GameClient.Data;
using GameClient.Presentation.Board;
using GameDomain.Gameplay;
using GameDomain.Generation;
using GameDomain.Model;
using UnityEngine;

namespace GameClient.Presentation
{
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField] private BoardView _boardView;
        [SerializeField] private TrayView _trayView;
        [SerializeField] private GameOverPopup _gameOverPopup;

        private BoardState _board;
        private List<TileSlot> _shape;
        private Dictionary<string, TileSlot> _slotsById;
        private readonly ComboScorer _comboScorer = new ComboScorer();

        public event Action<int, int> ScoreChanged;
        public event Action<int, int, int> UsesChanged;

        private void Start()
        {
            LoadLevel();
        }

        public void RestartLevel()
        {
            if (_gameOverPopup != null)
                _gameOverPopup.Hide();
            LoadLevel();
        }

        private void LoadLevel()
        {
            // Build a random pyramid with 20 tiles
            _shape = PyramidShapeBuilder.BuildRandom(20, new System.Random());
            _slotsById = _shape.ToDictionary(s => s.Id);

            var level = new LevelDefinition
            {
                LevelId = 999, // Use an int ID for the randomized level
                Shape = _shape,
                TileSetId = "default"
            };

            _board = BoardGenerator.Generate(level, new System.Random());

            _boardView.Build(_board, _slotsById);
            if (_trayView != null)
                _trayView.Initialize(4); // 4 slots

            ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
            NotifyUsesChanged();
        }

        public void OnTileTapped(string slotId)
        {
            if (_board.IsGameOver) return;

            // Compute remaining tiles (not cleared and not in tray)
            var remaining = new HashSet<string>(
                _board.Cells.Where(kv => !kv.Value.Cleared && !_board.TrayTileIds.Contains(kv.Key)).Select(kv => kv.Key));

            if (!FreedomRuleCalculator.IsFree(_slotsById[slotId], remaining))
            {
                _boardView.GetTileView(slotId)?.PlayShake();
                return;
            }

            if (TrayManager.TryPushToTray(_board, _slotsById, slotId))
            {
                // Remove tile from BoardView entirely, or just hide it
                // We'll update BoardView to hide it, and update TrayView to show it
                _boardView.GetTileView(slotId)?.gameObject.SetActive(false);

                if (_trayView != null)
                    _trayView.UpdateTray(_board, _slotsById);

                _boardView.RefreshFreeStates(_board);
                ScoreChanged?.Invoke(_board.Score, _board.ComboCount);

                if (_board.IsGameOver)
                {
                    if (_gameOverPopup != null)
                        _gameOverPopup.Show(this);
                }
            }
        }

        public void OnHintRequested()
        {
            if (_board.HintsRemaining <= 0) return;

            var hint = HintFinder.FindFreePair(_board, _slotsById);
            if (!hint.HasValue) return;

            _board.HintsRemaining -= 1;

            _boardView.GetTileView(hint.Value.slotIdA)?.Highlight();
            _boardView.GetTileView(hint.Value.slotIdB)?.Highlight();
            NotifyUsesChanged();
        }

        public void OnUndoRequested()
        {
            if (UndoStack.TryUndo(_board))
            {
                _boardView.Build(_board, _slotsById);
                ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
                NotifyUsesChanged();
            }
        }

        public void OnShuffleRequested()
        {
            try
            {
                if (ShuffleService.Shuffle(_board, _shape, new System.Random()))
                {
                    _boardView.Build(_board, _slotsById);
                    NotifyUsesChanged();
                }
            }
            catch (BoardGenerationException ex)
            {
                Debug.LogWarning("Shuffle could not find a solvable arrangement, board left unchanged: " + ex.Message);
            }
        }

        private void NotifyUsesChanged()
        {
            UsesChanged?.Invoke(_board.HintsRemaining, _board.UndosRemaining, _board.ShufflesRemaining);
        }
    }
}
```

- [ ] **Step 2: Compile-check via a quick batch build**

There are no unit tests for `GameController` (it's a `MonoBehaviour`, not currently covered by EditMode tests, consistent with the rest of the Presentation layer). Verify it compiles cleanly:

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -executeMethod UnityEditor.SyncVS.SyncSolution -logFile /tmp/compile-task2.log -quit
grep -iE "error CS" /tmp/compile-task2.log
```
Expected: no `error CS` lines.

- [ ] **Step 3: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Presentation/GameController.cs
git commit -m "feat: gate hint/undo/shuffle on remaining uses and broadcast UsesChanged"
```

---

## Task 3: Procedural sprite generator + 7 tile icons

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Editor/ProceduralSpriteGenerator.cs`
- Create: `unity/GameClient/Assets/Scripts/Editor/TileIconGenerator.cs`

**Interfaces:**
- Produces: `ProceduralSpriteGenerator.Generate(string directory, int size, float aaWidthPixels, (string Name, Func<float,float,float> Sdf)[] shapes)` — a reusable rasterizer, consumed by Task 4 (`HudIconGenerator`) and this task's `TileIconGenerator`.
- Produces: `ProceduralSpriteGenerator.CircleSdf(u, v, cx, cy, r) : float`, `ProceduralSpriteGenerator.BoxSdf(u, v, halfW, halfH) : float` — shared shape primitives.
- Produces: PNG sprite assets at `Assets/Textures/Icons/icon_dots.png`, `icon_flower.png`, `icon_star.png`, `icon_diamond.png`, `icon_ring.png`, `icon_cross.png`, `icon_leaf.png` — consumed by Task 5 (`DataAssetGenerator`).

- [ ] **Step 1: Write the shared rasterizer**

Create `unity/GameClient/Assets/Scripts/Editor/ProceduralSpriteGenerator.cs`:

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ProceduralSpriteGenerator
{
    public static void Generate(
        string directory, int size, float aaWidthPixels, (string Name, Func<float, float, float> Sdf)[] shapes)
    {
        Directory.CreateDirectory(directory);
        float aaWidth = aaWidthPixels / size * 2f;

        foreach (var shape in shapes)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float dist = shape.Sdf(u, v);
                    float alpha = Mathf.Clamp01(0.5f - dist / aaWidth);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();

            string path = directory + "/" + shape.Name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = size;
            importer.SaveAndReimport();
        }

        AssetDatabase.SaveAssets();
    }

    public static float CircleSdf(float u, float v, float cx, float cy, float r)
    {
        float dx = u - cx, dy = v - cy;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }

    public static float BoxSdf(float u, float v, float halfW, float halfH)
    {
        return Mathf.Max(Mathf.Abs(u) - halfW, Mathf.Abs(v) - halfH);
    }
}
```

- [ ] **Step 2: Write the 7 tile icon shapes**

Create `unity/GameClient/Assets/Scripts/Editor/TileIconGenerator.cs`:

```csharp
using UnityEngine;
using static ProceduralSpriteGenerator;

public static class TileIconGenerator
{
    private const string Directory = "Assets/Textures/Icons";
    private const int Size = 128;

    public static void Generate()
    {
        ProceduralSpriteGenerator.Generate(Directory, Size, 3f, new (string, System.Func<float, float, float>)[]
        {
            ("icon_dots", DotClusterSdf),
            ("icon_flower", FlowerSdf),
            ("icon_star", StarSdf),
            ("icon_diamond", DiamondSdf),
            ("icon_ring", RingSdf),
            ("icon_cross", CrossSdf),
            ("icon_leaf", LeafSdf),
        });

        Debug.Log("TILE_ICON_GENERATOR_DONE");
    }

    private static float DotClusterSdf(float u, float v)
    {
        float best = float.MaxValue;
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI * 2f / 6f;
            float cx = Mathf.Cos(angle) * 0.52f;
            float cy = Mathf.Sin(angle) * 0.52f;
            best = Mathf.Min(best, CircleSdf(u, v, cx, cy, 0.24f));
        }
        return best;
    }

    private static float FlowerSdf(float u, float v)
    {
        float best = CircleSdf(u, v, 0f, 0f, 0.2f);
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.PI * 2f / 5f - Mathf.PI / 2f;
            float cx = Mathf.Cos(angle) * 0.4f;
            float cy = Mathf.Sin(angle) * 0.4f;
            best = Mathf.Min(best, CircleSdf(u, v, cx, cy, 0.32f));
        }
        return best;
    }

    private static float StarSdf(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float theta = Mathf.Atan2(v, u);
        const float outerR = 0.82f;
        const float innerR = 0.36f;
        float lobe = 0.5f + 0.5f * Mathf.Cos(5f * theta);
        float boundary = innerR + (outerR - innerR) * Mathf.Pow(lobe, 2.2f);
        return r - boundary;
    }

    private static float DiamondSdf(float u, float v)
    {
        return Mathf.Abs(u) + Mathf.Abs(v) - 0.78f;
    }

    private static float RingSdf(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        return Mathf.Abs(r - 0.55f) - 0.17f;
    }

    private static float CrossSdf(float u, float v)
    {
        float vertical = BoxSdf(u, v, 0.22f, 0.75f);
        float horizontal = BoxSdf(u, v, 0.75f, 0.22f);
        return Mathf.Min(vertical, horizontal);
    }

    private static float LeafSdf(float u, float v)
    {
        float top = CircleSdf(u, v, 0f, 0.34f, 0.62f);
        float bottom = CircleSdf(u, v, 0f, -0.34f, 0.62f);
        return Mathf.Max(top, bottom);
    }
}
```

- [ ] **Step 3: Run it and verify the 7 PNGs exist**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -executeMethod TileIconGenerator.Generate -logFile /tmp/tile-icons.log -quit
grep -E "TILE_ICON_GENERATOR_DONE|error CS" /tmp/tile-icons.log
ls unity/GameClient/Assets/Textures/Icons/*.png | wc -l
```
Expected: log contains `TILE_ICON_GENERATOR_DONE`, no `error CS` lines, and the file count is `7`.

- [ ] **Step 4: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Editor/ProceduralSpriteGenerator.cs \
  unity/GameClient/Assets/Scripts/Editor/TileIconGenerator.cs \
  unity/GameClient/Assets/Textures/Icons
git commit -m "feat: generate 7 procedural tile icon sprites (flower, leaf, star, diamond, ring, dots, cross)"
```

---

## Task 4: HUD control-bar icons (hint, undo, shuffle)

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Editor/HudIconGenerator.cs`

**Interfaces:**
- Consumes: `ProceduralSpriteGenerator.Generate/CircleSdf/BoxSdf` (Task 3).
- Produces: PNG sprite assets at `Assets/Textures/HudIcons/icon_hint.png`, `icon_undo.png`, `icon_shuffle.png` — consumed by Task 8 (`GameSceneBuilder`).

- [ ] **Step 1: Write the 3 HUD glyph shapes**

Create `unity/GameClient/Assets/Scripts/Editor/HudIconGenerator.cs`:

```csharp
using UnityEngine;
using static ProceduralSpriteGenerator;

public static class HudIconGenerator
{
    private const string Directory = "Assets/Textures/HudIcons";
    private const int Size = 128;

    public static void Generate()
    {
        ProceduralSpriteGenerator.Generate(Directory, Size, 3f, new (string, System.Func<float, float, float>)[]
        {
            ("icon_hint", LightbulbSdf),
            ("icon_undo", UndoSdf),
            ("icon_shuffle", ShuffleSdf),
        });

        Debug.Log("HUD_ICON_GENERATOR_DONE");
    }

    private static float LightbulbSdf(float u, float v)
    {
        float head = CircleSdf(u, v, 0f, 0.14f, 0.46f);
        float baseBox = BoxSdf(u, v + 0.52f, 0.17f, 0.2f);
        return Mathf.Min(head, baseBox);
    }

    private static float UndoSdf(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float theta = Mathf.Atan2(v, u) * Mathf.Rad2Deg;
        if (theta < 0f) theta += 360f;

        float ring = Mathf.Abs(r - 0.5f) - 0.13f;
        float arc = ring + AngleGapPenalty(theta, 40f, 320f);

        float capAngleRad = 40f * Mathf.Deg2Rad;
        float capX = Mathf.Cos(capAngleRad) * 0.5f;
        float capY = Mathf.Sin(capAngleRad) * 0.5f;
        float cap = CircleSdf(u, v, capX, capY, 0.18f);

        return Mathf.Min(arc, cap);
    }

    // Shape is visible for theta in [rangeStart, rangeEnd]; the gap is the short
    // arc on the other side (rangeEnd -> 360/0 -> rangeStart), which is where the
    // undo icon's "opening" reads as a directional break in the ring.
    private static float AngleGapPenalty(float theta, float rangeStart, float rangeEnd)
    {
        if (theta >= rangeStart && theta <= rangeEnd)
            return 0f;
        float distToStart = Mathf.Abs(Mathf.DeltaAngle(theta, rangeStart));
        float distToEnd = Mathf.Abs(Mathf.DeltaAngle(theta, rangeEnd));
        return Mathf.Min(distToStart, distToEnd) * 0.012f;
    }

    private static float ShuffleSdf(float u, float v)
    {
        float diag1 = DiagonalBarSdf(u, v, 1f);
        float diag2 = DiagonalBarSdf(u, v, -1f);
        float dots = Mathf.Min(
            Mathf.Min(CircleSdf(u, v, 0.55f, 0.55f, 0.14f), CircleSdf(u, v, -0.55f, -0.55f, 0.14f)),
            Mathf.Min(CircleSdf(u, v, 0.55f, -0.55f, 0.14f), CircleSdf(u, v, -0.55f, 0.55f, 0.14f)));
        return Mathf.Min(Mathf.Min(diag1, diag2), dots);
    }

    private static float DiagonalBarSdf(float u, float v, float sign)
    {
        float angle = sign * Mathf.PI / 4f;
        float cos = Mathf.Cos(-angle), sin = Mathf.Sin(-angle);
        float ru = u * cos - v * sin;
        float rv = u * sin + v * cos;
        return BoxSdf(ru, rv, 0.72f, 0.09f);
    }
}
```

- [ ] **Step 2: Run it and verify the 3 PNGs exist**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -executeMethod HudIconGenerator.Generate -logFile /tmp/hud-icons.log -quit
grep -E "HUD_ICON_GENERATOR_DONE|error CS" /tmp/hud-icons.log
ls unity/GameClient/Assets/Textures/HudIcons/*.png | wc -l
```
Expected: log contains `HUD_ICON_GENERATOR_DONE`, no `error CS` lines, file count is `3`.

- [ ] **Step 3: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Editor/HudIconGenerator.cs \
  unity/GameClient/Assets/Textures/HudIcons
git commit -m "feat: generate procedural hint/undo/shuffle HUD glyph sprites"
```

---

## Task 5: `TileSetAsset` icon+color palette and `DataAssetGenerator`

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Data/TileSetAsset.cs`
- Modify: `unity/GameClient/Assets/Scripts/Editor/DataAssetGenerator.cs`

**Interfaces:**
- Consumes: `Assets/Textures/Icons/icon_*.png` (Task 3).
- Produces: `TileSetAsset.Icons : Sprite[]` (length 7), `TileSetAsset.AccentColors : Color[]` (length 4), populated on `Assets/Data/DefaultTileSet.asset` — consumed by Task 8 (`BoardView`).

- [ ] **Step 1: Add the fields to `TileSetAsset`**

Edit `unity/GameClient/Assets/Scripts/Data/TileSetAsset.cs`:

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
    }
}
```

- [ ] **Step 2: Populate it in `DataAssetGenerator`**

Edit `unity/GameClient/Assets/Scripts/Editor/DataAssetGenerator.cs`:

```csharp
using System.IO;
using GameClient.Data;
using UnityEditor;
using UnityEngine;

public static class DataAssetGenerator
{
    private static readonly string[] IconNames =
    {
        "icon_dots", "icon_flower", "icon_star", "icon_diamond", "icon_ring", "icon_cross", "icon_leaf"
    };

    private static readonly Color[] AccentColors =
    {
        new Color(176f / 255f, 66f / 255f, 40f / 255f, 1f),  // terracotta
        new Color(150f / 255f, 92f / 255f, 0f / 255f, 1f),   // dark amber
        new Color(18f / 255f, 97f / 255f, 97f / 255f, 1f),   // teal
        new Color(94f / 255f, 51f / 255f, 133f / 255f, 1f),  // plum
    };

    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Data");

        var tokens = ScriptableObject.CreateInstance<AccessibilityTokens>();
        AssetDatabase.CreateAsset(tokens, "Assets/Data/DefaultAccessibilityTokens.asset");

        var tileSet = ScriptableObject.CreateInstance<TileSetAsset>();
        tileSet.TileSetId = "default";
        tileSet.Icons = new Sprite[IconNames.Length];
        for (int i = 0; i < IconNames.Length; i++)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Icons/" + IconNames[i] + ".png");
            if (sprite == null)
                throw new System.Exception(
                    "DATA_ASSET_GENERATOR_MISSING_ICON: " + IconNames[i] + " - run TileIconGenerator.Generate() first");
            tileSet.Icons[i] = sprite;
        }
        tileSet.AccentColors = AccentColors;
        AssetDatabase.CreateAsset(tileSet, "Assets/Data/DefaultTileSet.asset");

        var level = ScriptableObject.CreateInstance<LevelShapeAsset>();
        level.LevelId = 1;
        level.RowLengthsByLayer = new[] { 8 };
        level.TileSetId = "default";
        AssetDatabase.CreateAsset(level, "Assets/Data/SmallTestLevel.asset");

        AssetDatabase.SaveAssets();
        Debug.Log("DATA_ASSET_GENERATOR_DONE");
    }
}
```

- [ ] **Step 3: Run it and verify the asset is populated**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -executeMethod DataAssetGenerator.Generate -logFile /tmp/data-assets.log -quit
grep -E "DATA_ASSET_GENERATOR_DONE|error CS|MISSING_ICON" /tmp/data-assets.log
grep -c "guid:" unity/GameClient/Assets/Data/DefaultTileSet.asset
```
Expected: log contains `DATA_ASSET_GENERATOR_DONE`, no `error CS`/`MISSING_ICON` lines, and the asset YAML references multiple `guid:`s (the 7 icon sprites).

- [ ] **Step 4: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Data/TileSetAsset.cs \
  unity/GameClient/Assets/Scripts/Editor/DataAssetGenerator.cs \
  unity/GameClient/Assets/Data/DefaultTileSet.asset
git commit -m "feat: populate default tile set with 7 icons x 4 accent colors"
```

---

## Task 6: Tile rendering pipeline — `TileView`, `TilePrefabGenerator`, `BoardView`

These three files are mutually dependent for compilation (`TileView.Initialize`'s
signature is called by `BoardView` and its serialized fields are wired by
`TilePrefabGenerator`), and Unity batch mode always compiles the **entire**
project before running any `-executeMethod`, including EditMode test runs. Editing
them across separate tasks would leave the project in a non-compiling state that
no batch-mode command could get past, so they're one task with one verification
pass at the end.

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/Board/TileView.cs`
- Modify: `unity/GameClient/Assets/Scripts/Editor/TilePrefabGenerator.cs`
- Modify: `unity/GameClient/Assets/Scripts/Presentation/Board/BoardView.cs`

**Interfaces:**
- Consumes: `TileSetAsset.Icons/AccentColors` (Task 5).
- Produces: `TileView.Initialize(string slotId, int layer, Sprite icon, Color accentColor)`, `SetFree(bool)`, `Highlight()`, `PlayClearAndDestroy()`, `PlayShake()`; `Assets/Prefabs/Tile.prefab` wiring the serialized fields `_shadowRenderer`, `_selectionGlowRenderer`, `_accentRenderer`, `_cardRenderer`, `_iconRenderer`; `BoardView._tileSet` serialized field — all consumed by Task 8 (`GameSceneBuilder`, which wires `_tileSet` to `Assets/Data/DefaultTileSet.asset` and references `Assets/Prefabs/Tile.prefab` at its existing unchanged path).

This task deliberately removes the on-tile letter (`TextMesh`) — the target card structure per the spec is background + accent + icon only, and icon+color is now the accessible coding scheme (the letter was a pre-icon placeholder, and its `pair_`/substring parsing was already broken for two-digit values).

- [ ] **Step 1: Rewrite `TileView.cs`**

```csharp
using System.Collections;
using UnityEngine;

namespace GameClient.Presentation.Board
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class TileView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _shadowRenderer;
        [SerializeField] private SpriteRenderer _selectionGlowRenderer;
        [SerializeField] private SpriteRenderer _accentRenderer;
        [SerializeField] private SpriteRenderer _cardRenderer;
        [SerializeField] private SpriteRenderer _iconRenderer;
        [SerializeField] private Color _freeCardColor = new Color(0.969f, 0.957f, 0.922f, 1f);
        [SerializeField] private Color _blockedCardColor = new Color(0.62f, 0.63f, 0.58f, 1f);
        [SerializeField] private Color _selectionGlowColor = new Color(1f, 0.85f, 0.2f, 1f);

        private Vector3 _originalLocalPos;
        private Coroutine _shakeCoroutine;
        private Coroutine _clearCoroutine;

        public string SlotId { get; private set; }
        public int Layer { get; private set; }

        public void Initialize(string slotId, int layer, Sprite icon, Color accentColor)
        {
            SlotId = slotId;
            Layer = layer;

            // Apply a slight isometric offset based on layer to give a 3D stacked feel
            _originalLocalPos = transform.localPosition + new Vector3(layer * -0.04f, layer * 0.08f, 0f);
            transform.localPosition = _originalLocalPos;
            transform.localScale = Vector3.one;

            if (_accentRenderer != null)
            {
                var c = accentColor;
                c.a = 1f;
                _accentRenderer.color = c;
            }

            if (_iconRenderer != null)
            {
                _iconRenderer.sprite = icon;
                var c = accentColor;
                c.a = 1f;
                _iconRenderer.color = c;
            }

            if (_shadowRenderer != null)
            {
                float offset = 0.05f + layer * 0.025f;
                _shadowRenderer.transform.localPosition = new Vector3(offset, -offset, 0.06f);
                _shadowRenderer.color = new Color(0f, 0f, 0f, Mathf.Clamp01(0.3f + layer * 0.08f));
            }

            if (_selectionGlowRenderer != null)
            {
                var c = _selectionGlowColor;
                c.a = 0f;
                _selectionGlowRenderer.color = c;
            }

            RefreshCardColor(true, layer);
        }

        public void SetFree(bool isFree)
        {
            RefreshCardColor(isFree, Layer);
        }

        private void RefreshCardColor(bool isFree, int layer)
        {
            if (_cardRenderer == null) return;

            float layerBrightness = Mathf.Clamp01(1f - (2 - layer) * 0.1f);
            Color baseColor = isFree ? _freeCardColor : _blockedCardColor;
            _cardRenderer.color = new Color(
                baseColor.r * layerBrightness,
                baseColor.g * layerBrightness,
                baseColor.b * layerBrightness,
                baseColor.a);
        }

        public void Highlight()
        {
            if (_selectionGlowRenderer == null) return;
            var c = _selectionGlowColor;
            c.a = 1f;
            _selectionGlowRenderer.color = c;
        }

        public void PlayClearAndDestroy()
        {
            if (_clearCoroutine != null) StopCoroutine(_clearCoroutine);
            _clearCoroutine = StartCoroutine(ClearRoutine());
        }

        private IEnumerator ClearRoutine()
        {
            const float duration = 0.2f;
            float elapsed = 0f;
            var startScale = transform.localScale;
            var endScale = startScale * 1.15f;

            var renderers = new[] { _shadowRenderer, _selectionGlowRenderer, _accentRenderer, _cardRenderer, _iconRenderer };
            var startAlphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                startAlphas[i] = renderers[i] != null ? renderers[i].color.a : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, endScale, t);

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    var c = renderers[i].color;
                    c.a = startAlphas[i] * (1f - t);
                    renderers[i].color = c;
                }

                yield return null;
            }

            Destroy(gameObject);
        }

        public void PlayShake()
        {
            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            const float duration = 0.2f;
            float elapsed = 0f;

            if (_cardRenderer != null) _cardRenderer.color = Color.red;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float xOffset = Mathf.Sin(elapsed * 40f) * 0.1f;
                transform.localPosition = _originalLocalPos + new Vector3(xOffset, 0, 0);
                yield return null;
            }

            transform.localPosition = _originalLocalPos;
            RefreshCardColor(false, Layer);
        }
    }
}
```

- [ ] **Step 2: Rewrite `TilePrefabGenerator.cs`**

```csharp
using System.IO;
using GameClient.Presentation.Board;
using UnityEditor;
using UnityEngine;

public static class TilePrefabGenerator
{
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Prefabs");

        var root = new GameObject("Tile");

        var shadow = CreateSlicedChild(root.transform, "Shadow", new Vector2(0.82f, 0.82f), -2,
            new Vector3(0.05f, -0.05f, 0.06f), new Color(0f, 0f, 0f, 0.3f));

        var selectionGlow = CreateSlicedChild(root.transform, "SelectionGlow", new Vector2(1f, 1f), -1,
            Vector3.zero, new Color(1f, 0.85f, 0.2f, 0f));

        var accent = CreateSlicedChild(root.transform, "AccentBorder", new Vector2(0.86f, 0.86f), 0,
            Vector3.zero, new Color(0.69f, 0.26f, 0.16f, 1f));

        var card = CreateSlicedChild(root.transform, "Card", new Vector2(0.78f, 0.78f), 1,
            Vector3.zero, new Color(0.969f, 0.957f, 0.922f, 1f));

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(root.transform, false);
        iconGO.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        iconGO.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        var iconRenderer = iconGO.AddComponent<SpriteRenderer>();
        iconRenderer.drawMode = SpriteDrawMode.Simple;
        iconRenderer.sortingOrder = 2;

        var collider = root.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        var tileView = root.AddComponent<TileView>();
        var serialized = new SerializedObject(tileView);
        serialized.FindProperty("_shadowRenderer").objectReferenceValue = shadow;
        serialized.FindProperty("_selectionGlowRenderer").objectReferenceValue = selectionGlow;
        serialized.FindProperty("_accentRenderer").objectReferenceValue = accent;
        serialized.FindProperty("_cardRenderer").objectReferenceValue = card;
        serialized.FindProperty("_iconRenderer").objectReferenceValue = iconRenderer;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Tile.prefab");
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log("TILE_PREFAB_GENERATOR_DONE");
    }

    private static SpriteRenderer CreateSlicedChild(
        Transform parent, string name, Vector2 size, int sortingOrder, Vector3 localPos, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = GetOrCreateSquareSprite();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.sortingOrder = sortingOrder;
        renderer.color = color;
        return renderer;
    }

    private static Sprite GetOrCreateSquareSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }
}
```

- [ ] **Step 3: Rewrite `BoardView.cs`**

```csharp
using System.Collections.Generic;
using System.Linq;
using GameClient.Data;
using GameDomain.Generation;
using GameDomain.Model;
using UnityEngine;

namespace GameClient.Presentation.Board
{
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private TileView _tilePrefab;
        [SerializeField] private TileSetAsset _tileSet;
        [SerializeField] private float _cellSize = 0.9f;

        private readonly Dictionary<string, TileView> _tileViews = new Dictionary<string, TileView>();
        private Dictionary<string, TileSlot> _slotsById;

        public void Build(BoardState board, Dictionary<string, TileSlot> slotsById)
        {
            _slotsById = slotsById;

            foreach (var view in _tileViews.Values)
                if (view != null) Destroy(view.gameObject);
            _tileViews.Clear();

            foreach (var kv in board.Cells)
            {
                if (kv.Value.Cleared) continue;

                var slot = slotsById[kv.Key];
                var view = Instantiate(_tilePrefab, transform);
                view.transform.localPosition = new Vector3(
                    slot.X * _cellSize,
                    slot.Y * _cellSize,
                    -slot.Layer * 0.1f);
                view.Initialize(slot.Id, slot.Layer, IconForValue(kv.Value.Value), AccentColorForValue(kv.Value.Value));
                _tileViews[kv.Key] = view;
            }

            RefreshFreeStates(board);
        }

        public void RefreshFreeStates(BoardState board)
        {
            var remaining = new HashSet<string>(
                board.Cells.Where(kv => !kv.Value.Cleared).Select(kv => kv.Key));

            foreach (var kv in _tileViews)
            {
                bool isFree = FreedomRuleCalculator.IsFree(_slotsById[kv.Key], remaining);
                kv.Value.SetFree(isFree);
            }
        }

        public void RemoveTiles(IEnumerable<string> slotIds)
        {
            foreach (var id in slotIds)
            {
                if (!_tileViews.TryGetValue(id, out var view)) continue;
                view.PlayClearAndDestroy();
                _tileViews.Remove(id);
            }
        }

        public TileView GetTileView(string slotId) =>
            _tileViews.TryGetValue(slotId, out var view) ? view : null;

        // Icons.Length (7) * AccentColors.Length (4) = 28 unique combinations, which
        // covers the full 0-25 value range the domain layer can assign (values are
        // capped at mod 26 by ReverseConstructionSolver), so no two distinct pair
        // values ever render as the same icon+color combo.
        private Sprite IconForValue(string value)
        {
            int index = int.Parse(value);
            return _tileSet.Icons[index % _tileSet.Icons.Length];
        }

        private Color AccentColorForValue(string value)
        {
            int index = int.Parse(value);
            int colorIndex = (index / _tileSet.Icons.Length) % _tileSet.AccentColors.Length;
            return _tileSet.AccentColors[colorIndex];
        }
    }
}
```

- [ ] **Step 4: Regenerate the prefab and run the EditMode tests against the now-consistent project**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -executeMethod TilePrefabGenerator.Generate -logFile /tmp/tile-prefab.log -quit
grep -E "TILE_PREFAB_GENERATOR_DONE|error CS" /tmp/tile-prefab.log
grep -c "_shadowRenderer:\|_selectionGlowRenderer:\|_accentRenderer:\|_cardRenderer:\|_iconRenderer:" \
  unity/GameClient/Assets/Prefabs/Tile.prefab

"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -runTests -testPlatform EditMode -testResults /tmp/results-task6.xml -logFile /tmp/compile-task6.log -quit
grep -iE "error CS" /tmp/compile-task6.log
grep -E "test-run" /tmp/results-task6.xml | head -2
```
Expected: first log contains `TILE_PREFAB_GENERATOR_DONE` with no `error CS`, the grep finds all 5 field names in the regenerated prefab YAML, the second compile log has no `error CS` (confirms `TileView`, `TilePrefabGenerator`, and `BoardView` all compile together), and `test-run` shows `failures="0"`.

- [ ] **Step 5: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Presentation/Board/TileView.cs \
  unity/GameClient/Assets/Scripts/Editor/TilePrefabGenerator.cs \
  unity/GameClient/Assets/Scripts/Presentation/Board/BoardView.cs \
  unity/GameClient/Assets/Prefabs/Tile.prefab
git commit -m "feat: rebuild tile rendering as icon+color cards with layer-driven shadow and glow"
```

---

## Task 7: Control-button badge component and Hint/Undo/Shuffle wiring

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Presentation/HUD/ControlButtonUsesDisplay.cs`
- Modify: `unity/GameClient/Assets/Scripts/Presentation/HUD/HintButton.cs`
- Modify: `unity/GameClient/Assets/Scripts/Presentation/HUD/UndoButton.cs`
- Modify: `unity/GameClient/Assets/Scripts/Presentation/HUD/ShuffleButton.cs`

**Interfaces:**
- Consumes: `GameController.UsesChanged : Action<int,int,int>` (Task 2).
- Produces: `ControlButtonUsesDisplay.SetRemaining(int remaining)` and its serialized fields `_button`, `_faceImage`, `_iconImage`, `_badgeBackground`, `_badgeText` — consumed by Task 8 (`GameSceneBuilder`).
- Produces: `HintButton`/`UndoButton`/`ShuffleButton` serialized field `_usesDisplay` — consumed by Task 8.

- [ ] **Step 1: Create `ControlButtonUsesDisplay`**

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class ControlButtonUsesDisplay : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _faceImage;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _badgeBackground;
        [SerializeField] private Text _badgeText;
        [SerializeField] private float _disabledAlpha = 0.4f;

        public void SetRemaining(int remaining)
        {
            if (_badgeText != null)
                _badgeText.text = remaining.ToString();

            bool available = remaining > 0;
            if (_button != null)
                _button.interactable = available;

            float alpha = available ? 1f : _disabledAlpha;
            SetAlpha(_faceImage, alpha);
            SetAlpha(_iconImage, alpha);
            SetAlpha(_badgeBackground, alpha);
            SetAlpha(_badgeText, alpha);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
```

- [ ] **Step 2: Wire `HintButton`**

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class HintButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private ControlButtonUsesDisplay _usesDisplay;
        [SerializeField] private GameController _gameController;

        private void Start()
        {
            if (_button != null)
                _button.onClick.AddListener(() => _gameController.OnHintRequested());
        }

        private void OnEnable()
        {
            if (_gameController != null)
                _gameController.UsesChanged += HandleUsesChanged;
        }

        private void OnDisable()
        {
            if (_gameController != null)
                _gameController.UsesChanged -= HandleUsesChanged;
        }

        private void HandleUsesChanged(int hintsRemaining, int undosRemaining, int shufflesRemaining)
        {
            _usesDisplay?.SetRemaining(hintsRemaining);
        }
    }
}
```

- [ ] **Step 3: Wire `UndoButton`**

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class UndoButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private ControlButtonUsesDisplay _usesDisplay;
        [SerializeField] private GameController _gameController;

        private void Start()
        {
            if (_button != null)
                _button.onClick.AddListener(() => _gameController.OnUndoRequested());
        }

        private void OnEnable()
        {
            if (_gameController != null)
                _gameController.UsesChanged += HandleUsesChanged;
        }

        private void OnDisable()
        {
            if (_gameController != null)
                _gameController.UsesChanged -= HandleUsesChanged;
        }

        private void HandleUsesChanged(int hintsRemaining, int undosRemaining, int shufflesRemaining)
        {
            _usesDisplay?.SetRemaining(undosRemaining);
        }
    }
}
```

- [ ] **Step 4: Wire `ShuffleButton`**

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class ShuffleButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private ControlButtonUsesDisplay _usesDisplay;
        [SerializeField] private GameController _gameController;

        private void Start()
        {
            if (_button != null)
                _button.onClick.AddListener(() => _gameController.OnShuffleRequested());
        }

        private void OnEnable()
        {
            if (_gameController != null)
                _gameController.UsesChanged += HandleUsesChanged;
        }

        private void OnDisable()
        {
            if (_gameController != null)
                _gameController.UsesChanged -= HandleUsesChanged;
        }

        private void HandleUsesChanged(int hintsRemaining, int undosRemaining, int shufflesRemaining)
        {
            _usesDisplay?.SetRemaining(shufflesRemaining);
        }
    }
}
```

- [ ] **Step 5: Commit**

These four files reference each other and `GameController` but aren't wired into a scene yet (Task 8 does that) and have no unit tests (consistent with the rest of `Presentation/HUD`). Stage and commit now; full-project compile is checked at the end of Task 8.

```bash
git add unity/GameClient/Assets/Scripts/Presentation/HUD/ControlButtonUsesDisplay.cs \
  unity/GameClient/Assets/Scripts/Presentation/HUD/HintButton.cs \
  unity/GameClient/Assets/Scripts/Presentation/HUD/UndoButton.cs \
  unity/GameClient/Assets/Scripts/Presentation/HUD/ShuffleButton.cs
git commit -m "feat: add remaining-uses badge display to hint/undo/shuffle buttons"
```

---

## Task 8: `GameSceneBuilder` — green background, score pill, circular badged buttons, `RegenerateAll`

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder.cs`
- Create: `unity/GameClient/Assets/Scripts/Editor/RegenerateAll.cs`

**Interfaces:**
- Consumes: `Assets/Textures/HudIcons/icon_hint.png`/`icon_undo.png`/`icon_shuffle.png` (Task 4), `Assets/Data/DefaultTileSet.asset` (Task 5), `BoardView._tileSet` (Task 6), `ControlButtonUsesDisplay` + `HintButton`/`UndoButton`/`ShuffleButton._usesDisplay` (Task 7), `TileIconGenerator.Generate`/`HudIconGenerator.Generate`/`DataAssetGenerator.Generate`/`TilePrefabGenerator.Generate`/`GameSceneBuilder.Build` (Tasks 3-6).
- Produces: `Assets/Scenes/Game.unity` — the full playable scene; `RegenerateAll.Run()` — single entry point that reruns every generator in dependency order, used for verification in Task 9.

- [ ] **Step 1: Rewrite `GameSceneBuilder.cs`**

```csharp
using System.IO;
using GameClient.Data;
using GameClient.Presentation;
using GameClient.Presentation.Board;
using GameClient.Presentation.HUD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GameSceneBuilder
{
    private static readonly Color BoardGreen = new Color(42f / 255f, 61f / 255f, 48f / 255f, 1f);
    private static readonly Color CardOffWhite = new Color(0.969f, 0.957f, 0.922f, 1f);
    private static readonly Color DarkHudText = new Color(40f / 255f, 46f / 255f, 36f / 255f, 1f);
    private static readonly Color BadgeTerracotta = new Color(0.69f, 0.26f, 0.16f, 1f);

    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGO = new GameObject("Main Camera", typeof(Camera));
        var camera = cameraGO.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f; // Fit 6x4 pyramid
        camera.transform.position = new Vector3(2.5f, -1.5f, -10f); // Center on 6x4 grid
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BoardGreen;
        cameraGO.tag = "MainCamera";

        // Add EventSystem for UI clicks
        var eventSystemGO = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

        var boardGO = new GameObject("Board", typeof(BoardView));
        var boardView = boardGO.GetComponent<BoardView>();
        var tilePrefab = AssetDatabase.LoadAssetAtPath<TileView>("Assets/Prefabs/Tile.prefab");
        RequireNotNull(tilePrefab, "Assets/Prefabs/Tile.prefab as TileView");
        SetField(boardView, "_tilePrefab", tilePrefab);

        var tileSet = AssetDatabase.LoadAssetAtPath<TileSetAsset>("Assets/Data/DefaultTileSet.asset");
        RequireNotNull(tileSet, "Assets/Data/DefaultTileSet.asset as TileSetAsset");
        SetField(boardView, "_tileSet", tileSet);

        var inputGO = new GameObject("TileInputController", typeof(TileInputController));
        var inputController = inputGO.GetComponent<TileInputController>();

        var gameControllerGO = new GameObject("GameController", typeof(GameController));
        var gameController = gameControllerGO.GetComponent<GameController>();
        SetField(gameController, "_boardView", boardView);

        SetField(inputController, "_targetCamera", camera);
        SetField(inputController, "_gameController", gameController);

        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // ------------------
        // Score pill
        // ------------------
        var scorePillGO = new GameObject("ScorePill", typeof(Image));
        scorePillGO.transform.SetParent(canvasGO.transform, false);
        var scorePillImage = scorePillGO.GetComponent<Image>();
        scorePillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        scorePillImage.type = Image.Type.Sliced;
        scorePillImage.color = CardOffWhite;
        var scorePillRect = scorePillGO.GetComponent<RectTransform>();
        scorePillRect.anchorMin = new Vector2(0f, 1f);
        scorePillRect.anchorMax = new Vector2(0f, 1f);
        scorePillRect.pivot = new Vector2(0f, 1f);
        scorePillRect.anchoredPosition = new Vector2(20f, -20f);
        scorePillRect.sizeDelta = new Vector2(220f, 56f);

        var scoreGO = new GameObject("ScoreText", typeof(Text), typeof(ScoreDisplay));
        scoreGO.transform.SetParent(scorePillGO.transform, false);
        var scoreText = scoreGO.GetComponent<Text>();
        scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        scoreText.fontSize = 28;
        scoreText.color = DarkHudText;
        scoreText.alignment = TextAnchor.MiddleCenter;
        scoreText.text = "Score: 0";
        var scoreRect = scoreGO.GetComponent<RectTransform>();
        scoreRect.anchorMin = Vector2.zero;
        scoreRect.anchorMax = Vector2.one;
        scoreRect.offsetMin = Vector2.zero;
        scoreRect.offsetMax = Vector2.zero;
        var scoreDisplay = scoreGO.GetComponent<ScoreDisplay>();
        SetField(scoreDisplay, "_scoreText", scoreText);
        SetField(scoreDisplay, "_gameController", gameController);

        // ------------------
        // Control bar
        // ------------------
        var hintIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_hint.png");
        var undoIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_undo.png");
        var shuffleIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/HudIcons/icon_shuffle.png");
        RequireNotNull(hintIcon, "Assets/Textures/HudIcons/icon_hint.png as Sprite");
        RequireNotNull(undoIcon, "Assets/Textures/HudIcons/icon_undo.png as Sprite");
        RequireNotNull(shuffleIcon, "Assets/Textures/HudIcons/icon_shuffle.png as Sprite");

        CreateHudButton(canvasGO.transform, "ShuffleButton", new Vector2(-120f, 60f), gameController,
            typeof(ShuffleButton), shuffleIcon);
        CreateHudButton(canvasGO.transform, "HintButton", new Vector2(0f, 60f), gameController,
            typeof(HintButton), hintIcon);
        CreateHudButton(canvasGO.transform, "UndoButton", new Vector2(120f, 60f), gameController,
            typeof(UndoButton), undoIcon);

        // ------------------
        // Build Tray UI
        // ------------------
        var trayGO = new GameObject("TrayPanel", typeof(Image), typeof(HorizontalLayoutGroup), typeof(TrayView));
        trayGO.transform.SetParent(canvasGO.transform, false);
        var trayImage = trayGO.GetComponent<Image>();
        trayImage.color = new Color(0, 0, 0, 0.5f);
        var trayRect = trayGO.GetComponent<RectTransform>();
        trayRect.anchorMin = new Vector2(0.5f, 1f);
        trayRect.anchorMax = new Vector2(0.5f, 1f);
        trayRect.pivot = new Vector2(0.5f, 1f);
        trayRect.anchoredPosition = new Vector2(0, -80f);
        trayRect.sizeDelta = new Vector2(400f, 100f);

        var trayLayout = trayGO.GetComponent<HorizontalLayoutGroup>();
        trayLayout.childAlignment = TextAnchor.MiddleCenter;
        trayLayout.childControlWidth = false;
        trayLayout.childControlHeight = false;
        trayLayout.spacing = 10f;

        var trayView = trayGO.GetComponent<TrayView>();
        var traySlotPrefab = new GameObject("TraySlot", typeof(RectTransform));
        var slotRect = traySlotPrefab.GetComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(80f, 80f);

        var shadowGO = new GameObject("Shadow", typeof(Image));
        shadowGO.transform.SetParent(traySlotPrefab.transform, false);
        var shadowImage = shadowGO.GetComponent<Image>();
        shadowImage.color = new Color(0, 0, 0, 0.4f);
        var shadowRect = shadowGO.GetComponent<RectTransform>();
        shadowRect.anchorMin = Vector2.zero;
        shadowRect.anchorMax = Vector2.one;
        shadowRect.anchoredPosition = new Vector2(5f, -5f);

        var bgGO = new GameObject("Background", typeof(Image));
        bgGO.transform.SetParent(traySlotPrefab.transform, false);
        var bgImage = bgGO.GetComponent<Image>();
        var bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        var iconGO = new GameObject("Icon", typeof(Image));
        iconGO.transform.SetParent(traySlotPrefab.transform, false);
        var iconImage = iconGO.GetComponent<Image>();
        var iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.1f, 0.1f);
        iconRect.anchorMax = new Vector2(0.9f, 0.9f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        var slotTextGO = new GameObject("Text", typeof(Text));
        slotTextGO.transform.SetParent(traySlotPrefab.transform, false);
        var slotText = slotTextGO.GetComponent<Text>();
        slotText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        slotText.fontSize = 40;
        slotText.color = Color.black;
        slotText.alignment = TextAnchor.MiddleCenter;
        var slotTextRect = slotTextGO.GetComponent<RectTransform>();
        slotTextRect.anchorMin = Vector2.zero;
        slotTextRect.anchorMax = Vector2.one;
        slotTextRect.offsetMin = Vector2.zero;
        slotTextRect.offsetMax = Vector2.zero;

        SetField(trayView, "layoutGroup", trayLayout);
        SetField(trayView, "traySlotPrefab", traySlotPrefab);
        SetField(gameController, "_trayView", trayView);

        // ------------------
        // Build Game Over UI
        // ------------------
        var gameOverGO = new GameObject("GameOverPanel", typeof(Image), typeof(GameOverPopup));
        gameOverGO.transform.SetParent(canvasGO.transform, false);
        var goImage = gameOverGO.GetComponent<Image>();
        goImage.color = new Color(0, 0, 0, 0.8f);
        var goRect = gameOverGO.GetComponent<RectTransform>();
        goRect.anchorMin = Vector2.zero;
        goRect.anchorMax = Vector2.one;
        goRect.offsetMin = Vector2.zero;
        goRect.offsetMax = Vector2.zero;

        var goTextGO = new GameObject("Text", typeof(Text));
        goTextGO.transform.SetParent(gameOverGO.transform, false);
        var goText = goTextGO.GetComponent<Text>();
        goText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        goText.fontSize = 60;
        goText.text = "GAME OVER";
        goText.alignment = TextAnchor.MiddleCenter;
        var goTextRect = goTextGO.GetComponent<RectTransform>();
        goTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        goTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        goTextRect.anchoredPosition = new Vector2(0, 100f);
        goTextRect.sizeDelta = new Vector2(400, 100);

        var restartBtnGO = new GameObject("RestartButton", typeof(Image), typeof(Button));
        restartBtnGO.transform.SetParent(gameOverGO.transform, false);
        var restartBtnRect = restartBtnGO.GetComponent<RectTransform>();
        restartBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
        restartBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
        restartBtnRect.anchoredPosition = new Vector2(0, -50f);
        restartBtnRect.sizeDelta = new Vector2(200, 60);

        var restartTextGO = new GameObject("Text", typeof(Text));
        restartTextGO.transform.SetParent(restartBtnGO.transform, false);
        var restartText = restartTextGO.GetComponent<Text>();
        restartText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        restartText.fontSize = 30;
        restartText.color = Color.black;
        restartText.text = "Restart";
        restartText.alignment = TextAnchor.MiddleCenter;
        var restartTextRect = restartTextGO.GetComponent<RectTransform>();
        restartTextRect.anchorMin = Vector2.zero;
        restartTextRect.anchorMax = Vector2.one;
        restartTextRect.offsetMin = Vector2.zero;
        restartTextRect.offsetMax = Vector2.zero;

        var gameOverPopup = gameOverGO.GetComponent<GameOverPopup>();
        SetField(gameOverPopup, "restartButton", restartBtnGO.GetComponent<Button>());
        SetField(gameController, "_gameOverPopup", gameOverPopup);
        gameOverGO.SetActive(false); // Hidden by default

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.unity");

        Debug.Log("GAME_SCENE_BUILDER_DONE");
    }

    private static void CreateHudButton(
        Transform parent, string name, Vector2 anchoredPosition, GameController gameController,
        System.Type hudComponentType, Sprite iconSprite)
    {
        const float buttonSize = 84f;
        var knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        var buttonGO = new GameObject(name, typeof(Image), typeof(Button));
        buttonGO.transform.SetParent(parent, false);
        var rect = buttonGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(buttonSize, buttonSize);

        var faceImage = buttonGO.GetComponent<Image>();
        faceImage.sprite = knobSprite;
        faceImage.type = Image.Type.Simple;
        faceImage.color = CardOffWhite;

        var iconGO = new GameObject("Icon", typeof(Image));
        iconGO.transform.SetParent(buttonGO.transform, false);
        var iconImage = iconGO.GetComponent<Image>();
        iconImage.sprite = iconSprite;
        iconImage.type = Image.Type.Simple;
        iconImage.color = DarkHudText;
        var iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.24f, 0.24f);
        iconRect.anchorMax = new Vector2(0.76f, 0.76f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        var badgeGO = new GameObject("Badge", typeof(Image));
        badgeGO.transform.SetParent(buttonGO.transform, false);
        var badgeImage = badgeGO.GetComponent<Image>();
        badgeImage.sprite = knobSprite;
        badgeImage.type = Image.Type.Simple;
        badgeImage.color = BadgeTerracotta;
        var badgeRect = badgeGO.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(1f, 1f);
        badgeRect.anchorMax = new Vector2(1f, 1f);
        badgeRect.pivot = new Vector2(0.5f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(-4f, -4f);
        badgeRect.sizeDelta = new Vector2(30f, 30f);

        var badgeTextGO = new GameObject("BadgeText", typeof(Text));
        badgeTextGO.transform.SetParent(badgeGO.transform, false);
        var badgeText = badgeTextGO.GetComponent<Text>();
        badgeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        badgeText.fontSize = 18;
        badgeText.alignment = TextAnchor.MiddleCenter;
        badgeText.color = Color.white;
        badgeText.text = "3";
        var badgeTextRect = badgeTextGO.GetComponent<RectTransform>();
        badgeTextRect.anchorMin = Vector2.zero;
        badgeTextRect.anchorMax = Vector2.one;
        badgeTextRect.offsetMin = Vector2.zero;
        badgeTextRect.offsetMax = Vector2.zero;

        var usesDisplay = buttonGO.AddComponent<ControlButtonUsesDisplay>();
        var button = buttonGO.GetComponent<Button>();
        SetField(usesDisplay, "_button", button);
        SetField(usesDisplay, "_faceImage", faceImage);
        SetField(usesDisplay, "_iconImage", iconImage);
        SetField(usesDisplay, "_badgeBackground", badgeImage);
        SetField(usesDisplay, "_badgeText", badgeText);

        var hudComponent = buttonGO.AddComponent(hudComponentType);
        SetField(hudComponent, "_button", button);
        SetField(hudComponent, "_usesDisplay", usesDisplay);
        SetField(hudComponent, "_gameController", gameController);
    }

    private static void SetField(Object target, string fieldName, Object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(fieldName);
        RequireNotNull(property, target.GetType().Name + "." + fieldName);
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RequireNotNull(Object value, string description)
    {
        if (value == null)
            throw new System.Exception("GAME_SCENE_BUILDER_MISSING: " + description);
    }

    private static void RequireNotNull(SerializedProperty value, string description)
    {
        if (value == null)
            throw new System.Exception("GAME_SCENE_BUILDER_MISSING_PROPERTY: " + description);
    }
}
```

- [ ] **Step 2: Create the `RegenerateAll` convenience entry point**

Create `unity/GameClient/Assets/Scripts/Editor/RegenerateAll.cs`:

```csharp
using UnityEngine;

public static class RegenerateAll
{
    public static void Run()
    {
        TileIconGenerator.Generate();
        HudIconGenerator.Generate();
        DataAssetGenerator.Generate();
        TilePrefabGenerator.Generate();
        GameSceneBuilder.Build();
        Debug.Log("REGENERATE_ALL_DONE");
    }
}
```

- [ ] **Step 3: Run `RegenerateAll` and confirm every generator succeeds end-to-end**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -executeMethod RegenerateAll.Run -logFile /tmp/regenerate-all.log -quit
grep -E "_DONE|error CS|MISSING" /tmp/regenerate-all.log
```
Expected: `TILE_ICON_GENERATOR_DONE`, `HUD_ICON_GENERATOR_DONE`, `DATA_ASSET_GENERATOR_DONE`, `TILE_PREFAB_GENERATOR_DONE`, `GAME_SCENE_BUILDER_DONE`, `REGENERATE_ALL_DONE` all present, no `error CS`/`MISSING` lines.

- [ ] **Step 4: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder.cs \
  unity/GameClient/Assets/Scripts/Editor/RegenerateAll.cs \
  unity/GameClient/Assets/Scenes/Game.unity
git commit -m "feat: green background, score pill, and circular badged control buttons in scene builder"
```

---

## Task 9: Full regeneration, EditMode tests, and visual verification

**Files:**
- None (verification-only task; regenerates assets already covered by Task 8's `RegenerateAll` and captures a screenshot with the existing `ScreenshotTest.cs`).

**Interfaces:**
- Consumes: `RegenerateAll.Run()` (Task 8), `ScreenshotTest.Run()` (pre-existing, `unity/GameClient/Assets/Scripts/Editor/ScreenshotTest.cs`).

- [ ] **Step 1: Run the full EditMode suite one more time against the final state**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -runTests -testPlatform EditMode -testResults /tmp/results-final.xml -logFile /tmp/compile-final.log -quit
grep -iE "error CS" /tmp/compile-final.log
grep -E "test-run" /tmp/results-final.xml | head -2
```
Expected: no `error CS`, `test-run` shows `failures="0"`.

- [ ] **Step 2: Capture a Play-mode screenshot and visually inspect it against the acceptance checklist**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -executeMethod ScreenshotTest.Run -logFile /tmp/screenshot.log -quit
grep "SCREENSHOT_CAPTURED" /tmp/screenshot.log
```
Then use the Read tool on the captured `screenshot.png` (path printed by the log line above, relative to `unity/GameClient/`) and check it against the spec's acceptance checklist:
- Every tile shows an icon (not just a color fill) on an off-white rounded card with a colored accent border.
- Tile shadows visibly differ by layer (higher-layer tiles cast a larger/darker offset shadow).
- Background is deep muted green, not gray-blue.
- The three control buttons are same-size circles with a visible badge in the corner.
- The score pill has a visible off-white container behind the text.

If anything reads wrong (icon shapes illegible at size, colors clashing, badge overlapping the icon, etc.), fix the specific generator/prefab/scene-builder code from the relevant earlier task and rerun `RegenerateAll.Run` (Step 3 of Task 8) before re-screenshotting — do not hand-edit the generated `Tile.prefab`/`Game.unity`/`DefaultTileSet.asset` files directly, since they're fully regenerated from code on every run and direct edits would be silently discarded.

- [ ] **Step 3: Clean up the temporary screenshot artifact**

The screenshot file is a local debugging artifact, not project source — confirm it isn't tracked before finishing:

```bash
git status --short unity/GameClient/screenshot.png
```
If it shows as untracked, leave it (harmless, git-ignored or not committed) or delete it; do not `git add` it.

- [ ] **Step 4: Final full-repo status check**

```bash
git status --short
git log --oneline -12
```
Confirm every task's commit is present and there is no unexpected leftover modified/untracked state beyond the screenshot.
