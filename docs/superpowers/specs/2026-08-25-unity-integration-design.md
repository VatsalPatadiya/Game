# Unity Integration — Design Spec

Date: 2026-08-25
Status: Approved for planning

## Context

Sub-project 1 (`domain/`) delivered a pure C# class library implementing the
board generator, freedom rule, and match/hint/undo/shuffle/combo services —
fully tested via `dotnet test`, zero Unity dependency. It has no UI and
produces nothing installable.

This spec covers Sub-project 2: wrapping that domain code in a real Unity
project, rendering the layered-turtle board, wiring tap input to the
gameplay services, building an accessibility-first HUD, and producing an
installable Android build — the first version of this game playable on an
actual phone.

Environment check performed before this spec was written: Unity 6000.5.9f1
is installed (Unity Hub + Editor), and Android Build Support (with its
SDK/NDK/JDK) has been installed via `unityhub --headless install-modules`.
No manual Unity Hub interaction was required for this step.

## Decisions

- **Unity project location:** `unity/GameClient/` at the repo root, sibling
  to `domain/`.
- **Asset creation approach:** headless, script-driven. Editor C# scripts
  (kept in `Assets/Scripts/Editor/`, not shipped in the build) are invoked
  via `Unity -batchmode -executeMethod <Type>.<Method>` to construct
  prefabs, scenes, and ScriptableObject instances — the standard
  CI-friendly technique for scripting Unity without GUI interaction.
- **Manual/scripted split:** all `.cs` files, `.asmdef` files, Player
  Settings/Android manifest config, and the build itself are scripted.
  Prefab/scene/ScriptableObject-instance creation defaults to
  headless-scripted for reproducibility. Cosmetic arrangement (sprite
  positions/scale, camera framing, HUD layout on-screen) is built with a
  working default via script, with the option to nudge it live in the
  Editor afterward — flagged per-task during planning, not decided
  up front for every file.
- **Target platform:** Android only, matching Sub-project 1's platform
  decision. iOS is out of scope.
- **Domain code placement:** `domain/src` is copied into
  `unity/GameClient/Assets/Scripts/Domain`, scoped by its own `.asmdef`
  with zero Unity engine references — enforcing the same "zero Unity
  dependency" boundary Sub-project 1 built and verified.
- **Level-shape authoring:** `LayeredRowShapeBuilder` — the row-lengths-
  per-layer topology builder proven in Sub-project 1's test fixtures
  (`TestLayoutShapes.BuildLayeredRowShape`) — is promoted from test-only
  code into `Domain/Generation` as shipped production code. Production
  levels reuse this exact, already-verified algorithm rather than a
  second implementation.

## Scope

**In scope:**
- Unity project setup at `unity/GameClient/`, Android build target.
- `Assets/Scripts/Domain`: copy of `domain/src`, own `.asmdef`, zero
  `UnityEngine` references — verified by inspection, matching Sub-project
  1's Global Constraint.
- `LayeredRowShapeBuilder` promoted to production `Domain/Generation` code.
- `LevelShapeAsset`, `TileSetAsset`, `AccessibilityTokens` ScriptableObjects
  (`Assets/Scripts/Data`).
- `GameController`: owns `LevelDefinition`/`BoardState`/`slotsById`/
  `ComboScorer`, orchestrates tile-tap selection → `MatchValidator.TryMatch`,
  and the Hint/Undo/Shuffle button handlers.
- `TileInputController`: layer-aware tap resolution (highest `Layer` among
  overlapping hits wins, since upper tiles must intercept taps over the
  tiles they cover).
- `BoardView`/`TileView`: spawn/update tiles from `BoardState`, recompute
  and render free/blocked visual state after every mutation, icon+color
  tile rendering (never color-alone).
- HUD: score display, Hint/Undo/Shuffle buttons, sized/contrast-checked
  from `AccessibilityTokens`.
- Clear animation on match, gentle shake on mismatch (no score penalty),
  per the original design spec's low-pressure interaction rules.
- One playable Android `.apk`, sideloadable onto a real device.

**Explicitly out of scope** (later sub-projects): save/load, level
map/progression, daily challenge, currency, ads/IAP, analytics, crash
reporting, remote config, final art/audio content (placeholder tile
icons/colors only), localization, iOS build.

## Project Structure

```
/unity/GameClient
  /Assets
    /Scripts
      /Domain                 -- copy of domain/src, own .asmdef, zero UnityEngine references
        /Model  /Generation  /Gameplay
      /Presentation
        /Board                -- BoardView.cs, TileView.cs, TileInputController.cs
        /HUD                  -- HUDController.cs, ScoreDisplay.cs, HintButton.cs, UndoButton.cs, ShuffleButton.cs
        /Effects               -- ClearAnimation.cs, ComboEffect.cs
        GameController.cs
      /Data                    -- LevelShapeAsset.cs, TileSetAsset.cs, AccessibilityTokens.cs
      /Editor                  -- headless asset-generation scripts (setup-time only, not shipped)
    /Scenes  Game.scene
    /Prefabs  Tile.prefab, ComboEffectFX.prefab
  /ProjectSettings
  /Packages
```

## Domain Integration

`domain/src`'s five files (Model/Generation/Gameplay) are copied verbatim
into `Assets/Scripts/Domain`, with a `Domain.asmdef` that has no reference
to `UnityEngine` or any Unity package — enforcing the same hard rule
Sub-project 1's Global Constraints established. `LayeredRowShapeBuilder`
is added to `Domain/Generation` as new production code, using the exact
algorithm already proven correct by Sub-project 1's
`TestLayoutShapesTests` and the 600-board solvability regression suite
(since it only changes slot-topology construction, not the freedom
rule or generator itself, the existing solvability guarantee still
applies to any board built from it).

`LevelShapeAsset` (ScriptableObject): `int[] RowLengthsByLayer`, `int
LevelId`, `string TileSetId`. At load time, `GameController` calls
`LayeredRowShapeBuilder.Build(asset.RowLengthsByLayer)` to get a
`List<TileSlot>`, wraps it in a `LevelDefinition`, and calls
`BoardGenerator.Generate(level, new Random(...))`.

## GameController, Input, and Rendering

**`GameController`** (MonoBehaviour): holds the active `LevelDefinition`,
`BoardState`, a `Dictionary<string, TileSlot> slotsById` built once per
level load and reused for every subsequent tap (avoiding an O(n) rebuild
per interaction), and one `ComboScorer` instance for the session.

- Tap flow: first tap on a free tile selects it (visual highlight); second
  tap on a different free tile calls
  `MatchValidator.TryMatch(board, slotsById, selected, tapped)`. On success:
  `ComboScorer.RegisterMatch(board, DateTime.UtcNow)`, play clear animation,
  refresh `BoardView`. On failure: deselect, play mismatch shake, no score
  change.
- Hint button: `HintFinder.FindFreePair(board, slotsById)`, highlight both
  returned tiles if non-null.
- Undo button: `UndoStack.TryUndo(board)`, refresh `BoardView` on success.
- Shuffle button: `ShuffleService.Shuffle(board, level.Shape, new
  Random(...))`, refresh `BoardView`; catches `BoardGenerationException`
  (should not occur under normal play, but the domain layer's own contract
  allows it) and surfaces a non-fatal "try again" state rather than
  crashing.

**`TileInputController`**: on tap/click, performs a 2D physics query at the
tap point (`Physics2D.OverlapPointAll` or equivalent), and among all
colliders hit, resolves to the one whose backing `TileSlot.Layer` is
highest — since layered tiles can visually and physically overlap, the
topmost tile must intercept the tap, matching the freedom rule's own
"nothing above it" semantics. Forwards the resolved slot id to
`GameController`.

**`BoardView`/`TileView`**: `BoardView` spawns one `TileView` per
uncleared cell from `Tile.prefab`, positioned/layered by the `TileSlot`'s
`X`/`Y`/`Layer`. After every mutation (match, undo, shuffle), `BoardView`
recomputes free/blocked state for every remaining tile via
`FreedomRuleCalculator.IsFree` and updates each `TileView`: free tiles
render full-contrast and interactive; blocked tiles render dimmed and
non-interactive. `TileView` always renders a tile's value as icon+color,
never color alone (accessibility requirement carried over from the
original design spec).

## Accessibility UI

`AccessibilityTokens` (ScriptableObject): minimum tap target (88dp
default), minimum body text size (20pt), pre-checked WCAG AA (4.5:1)
contrast color pairs, and the tile icon set (shape+color per value). HUD
buttons (`ScoreDisplay`, `HintButton`, `UndoButton`, `ShuffleButton`) and
`TileView` all read size/contrast from this single asset rather than
hardcoding per-screen, so later screens stay consistent by construction —
same design as Sub-project 1's spec.

## Android Build

- Scripting backend: IL2CPP, target architecture ARM64.
- Minimum API level: 24 (Android 7.0) — a reasonable modern floor without
  excluding older devices some players may have. Target API level: 34,
  matching the Android SDK Platform installed alongside Android Build
  Support in this session and a current, stable Play-Store-compliant
  baseline.
- No special permissions in the manifest for this sub-project (no
  networking, ads, or IAP yet).
- Output: a `.apk` under `unity/GameClient/Builds/`, installed onto a
  physical device via `adb install` (requires enabling "install unknown
  apps" / USB debugging on the device once).

## Testing Strategy

- `Domain` code ported into Unity keeps its existing NUnit tests, adapted
  to run under Unity's Test Framework (EditMode) rather than `dotnet test`
  — same test logic, different runner, since both are NUnit-based per
  Sub-project 1's README.
- `LayeredRowShapeBuilder`'s promotion to production code is verified by
  confirming the ported `TestLayoutShapesTests`/`SolvabilityRegressionTests`
  still pass unchanged under EditMode — proving the production algorithm
  didn't diverge from what was already proven solvable.
- Manual verification pass: build and run in the Unity Editor (Play mode)
  first for fast iteration, then a real Android device install for the
  final check of touch input, tap-target sizing, and layered-tile hit
  resolution — this can't be meaningfully unit-tested and is exploratory
  by nature.

## Open Items for Later Sub-Projects

- Save/load, level map/progression, daily challenge, currency: Sub-project
  3 (Meta & Progression).
- Final art/audio content, more than the one level shape needed to prove
  the loop: Sub-project 4 (Content Pipeline).
- Ads/IAP: Sub-project 5 (Monetization).
- iOS build: not scheduled; revisit if/when needed.
