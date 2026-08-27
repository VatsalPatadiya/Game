# 2D → 3D Conversion Design

**Status**: Approved by user 2026-08-27. Next step: implementation plan via `superpowers:writing-plans`.

## Goal

Convert the Unity mahjong-style tile-matching game's presentation layer from its current 2D sprite-based rendering (`SpriteRenderer`/`Image`, orthographic camera) to a full 3D presentation: realistic-looking (carved-tile) 3D board, tray, and HUD, entirely procedurally generated (no external 3D models/textures), with a fixed-angle perspective camera. This fully replaces the 2D presentation layer — there is no dual-mode/fallback; the 2D code is deleted once 3D is validated.

## Decisions made during brainstorming

| Question | Decision |
|---|---|
| Visual fidelity | Realistic 3D (carved-tile look), not simple flat-block stylization |
| Asset source | Best-effort procedural approximation only — no external/purchased/commissioned 3D models or textures; this sandbox cannot source or browse Asset Store content |
| Camera | Fixed angle (same interaction model as today's tap/drag), not player-rotatable/orbiting |
| Scope | Everything becomes 3D — board, tray, HUD buttons, popups. Not just the board with 2D UI overlay |
| Performance vs. visuals | Prioritize visuals; accept the test device (Galaxy A50, 2019 mid-range, 60Hz-only display) may run this poorly. No fallback quality tier is being designed for it |
| 2D code | Fully deleted once 3D is validated — no dual-maintenance of two renderers |

## Approach

**Chosen**: Migrate to Universal Render Pipeline (URP) + cube-primitive-based procedural meshes with a shader that fakes the beveled/carved edge via normal manipulation, textured using the existing SDF-generated card texture as the material's base map.

Rejected alternatives (see brainstorming transcript for full trade-off discussion):
- Stay on Built-in RP + hand-authored beveled mesh geometry — more custom geometry-math effort for a lower visual ceiling (Built-in RP's lighting/shadows are the limiting factor, not geometry fidelity).
- URP + hand-authored beveled geometry combined — most ambitious, deferred as a possible follow-up refinement only if the shader-based bevel from the chosen approach looks unconvincing once built.

## Architecture

- Add the URP package; configure a mobile-tier URP Asset with real-time shadows enabled; switch Graphics Settings to it.
- Real 3D depth: tiles get true X/Y/Z world positions, with Z genuinely encoding stack height (replacing the current fake-depth trick of small XY pixel offsets per layer).
- Tray becomes a physical 3D shelf mesh with tile-shaped slots.
- HUD buttons and popups become real 3D meshes (discs with depth, panels with depth) positioned in world space in front of the camera — not a 2D Canvas skinned to look 3D.
- Camera: fixed-angle perspective, framing the whole scene. Replaces `BoardView.FitCameraToBoard`'s orthographic-size math with equivalent perspective FOV/distance math.
- Lighting: one directional key light + ambient fill, real-time soft shadows on (per the "prioritize visuals" decision).

## Components

New/replaced presentation components (in-place replacement of the current 2D equivalents, which are deleted once validated):

- **`TileMeshGenerator`** (Editor) — replaces `TilePrefabGenerator.cs`. Builds the cube-based tile prefab: `MeshRenderer`/`MeshFilter`/`MeshCollider`, carved-edge shader material.
- **`CardMaterialGenerator`** (Editor) — extends `CardSpriteGenerator.cs`. Reuses the existing SDF-generated rounded-rect + icon texture, feeds it into a URP Lit material's Base Map instead of a `SpriteRenderer` sprite.
- **`BoardView3D`** / **`TileView3D`** — replace `BoardView.cs`/`TileView.cs`. Reuse point: `CardAnimator` (the shared animation coroutine library used for deal-in, tap-away, clear, shake, drag-to-peek) already operates through the abstract `ITintable` interface rather than directly on `SpriteRenderer`, so only one new adapter — `MeshRendererTint` (via `MaterialPropertyBlock`) — is needed to plug 3D meshes into the same animation code already written this project. This is the one significant piece of prior work that survives the conversion largely intact.
- **`TrayView3D`/`TraySlotView3D`**, **`HUD3D`** — 3D shelf and button/popup meshes; same component responsibilities as their 2D equivalents (`TrayView`, `TraySlotView`, `GameOverPopup`, HUD button scripts), different rendering.
- **`TileInputController3D`** — replaces `TileInputController.cs`. Swaps `Physics2D.OverlapPointAll` for `Physics.RaycastAll`; topmost-tile picking simplifies since tiles now have genuine spatial depth instead of a synthetic `Layer` int field driving a 2D sort trick.

## Data flow

- `GameDomain.*` (board state, matching/freedom rules, board generation, solving) has zero `UnityEngine` references (`noEngineReferences: true` on its asmdef) and requires **no changes**. This is the one layer fully insulated from the entire conversion.
- `GameController.cs`'s public API (`OnTileTapped`, `TapToTrayRoutine`, `OnHintRequested`, `OnUndoRequested`, `OnShuffleRequested`, etc.) stays structurally the same — it only deals with slot IDs and domain state, and calls into the new 3D view classes instead of the 2D ones. Its internal calls to `_boardView`/`_trayView` methods get retargeted to the 3D equivalents' equivalent method signatures.

## Error handling / risk

- **Device performance**: URP real-time shadows may run poorly on the Galaxy A50 test device. No fallback quality tier is planned, per the "prioritize visuals" decision. A sane mobile URP quality tier will still be set (not a desktop-grade default) so the app degrades in framerate rather than failing to render/crashing.
- **URP migration risk**: existing Canvas-based UI shaders are generally URP-compatible, but this is a real one-time migration step to verify, not a zero-risk flip.

## Testing

- All 40 existing EditMode tests (`GameDomain.Tests.*`, including the solvability regression suite) require zero changes and must keep passing throughout the conversion — this is the one part of the project immune to it.
- New 3D rendering/mesh-generation work is Editor-tool code, verified the same way all of this project's visual work has been verified so far: regenerate → compile/test check → install on device → screenshot/visual confirmation. No new automated visual-regression tests are planned for mesh/shader output, consistent with existing project convention.

## Scope acknowledgment

This touches nearly every presentation-layer file in the project (tiles, tray, HUD, popups, input, camera) plus a render pipeline migration. This is a large, multi-session undertaking — comparable in size to all prior visual-iteration rounds in this project combined, not a quick follow-on change.
