# Sub-project #2 — Premium Jade Visual Re-theme

## Status
Design approved in chat 2026-09-08. Second sub-project of the "Premium Mahjong
Solitaire" master plan. Follows sub-project #1 (on-board pair-match revert,
branch `onboard-match-mechanic-revert`).

## Constraint document
`premium-ui-guidelines.md` (user-provided) is the binding visual spec — a hard
constraint list, not suggestions. This design does not restate it; it references
its sections (§) and records the decomposition + per-pass decisions. Every pass
must pass that document's Definition of Done before it is "done."

## Theme (from the guidelines + master plan)
- Palette: deep jade green dominant (#0B3B2E–#123D2E), warm ivory tile faces
  (#F2EBD9 range), aged gold/brass accents (#D8A24A–#C98A2E), deep umber/near-black
  panel recesses (#1B120C). This **supersedes** the current wood/bronze "Ember"
  chrome + lighter warm felt.
- Every surface communicates a material (jade = polished stone, gold = lacquered
  metal, ivory = lacquered bone), never a single flat fill.
- Target quality bar: Vita Mahjong / Mahjong Scapes tier.
- 120 FPS is a hard ceiling (§10), checked every pass — not a separate pass.

## Decomposition (each pass = a see-and-approve checkpoint)
The user has been burned by board-wide visual changes landing before they can
judge them, so **every pass prototypes a hero sample for approval before rolling
out**, and passes ship one at a time.

- **Pass A — Jade background** (§2). ← this document details it; approved & first.
- **Pass B — Tile material** (§5): ivory/jade polished-stone faces, real bevel,
  stack-height-scaled shadows, selection lift, match burst.
- **Pass C — Buttons + icon tiers** (§3, §4): jade/gold lacquered PLAY button
  (multi-stop gradient, top highlight, hue-matched shadow, pressed + idle-glow
  states); 2-tier icon system with elevation + animated badges. Replaces the wood
  /bronze chrome.
- **Pass D — Panels + score/combo** (§6): recessed panel material; animated
  count-ups; the combo *meter* (a fill bar — distinct from the removed tray).
- **Pass E — Typography + spacing + motion** (§7–9): add a premium display font
  (deferred here per user), type scale, spacing rhythm, easing consistency.

Fonts: the project currently ships only LiberationSans. Adding a licensed display
font is **deferred to Pass E** (user decision), so Passes A–D keep LiberationSans.

## Pass A — Jade background (detailed)

### Current state
`FeltBackgroundGenerator.Generate()` bakes `Assets/Textures/Felt.png` — a radial
gradient (warm green core #57875B → felt #3E6753 → near-black #04140A) plus faint
grain. It is consumed via `FeltScreen.mat` (URP/Unlit) by a camera-parented
screen-filling backdrop (`BuildScreenFillingBackdrop`) on **both** the game screen
and the level-start screen, with a low-alpha leaf overlay
(`BuildLeafDecoration`). The current tone is lighter/warmer than the guidelines'
deep-jade target and reads as "flat template" felt.

### Approved look (hero sample A1 + faint lattice)
Rendered as PNG candidates and approved by the user:
- Bloom: highlight #1E5B45 → centre #123D2E, an intentional overhead bloom sitting
  in the upper third (bloom_y ≈ 0.34), core radius ≈ 0.62.
- Vignette: → edge #05160F, radius ≈ 1.02, plus deliberate corner darkening
  (≈ 0.60 strength past 0.5 normalised corner distance) so corners read darkest
  and the eye is drawn up-centre to the content.
- Texture: a **very faint diagonal lattice** (traditional motif) at ≈ 3% amplitude
  — "felt, not seen." Plus the existing ~1.4% felt grain.
- No concentric-ring motif (an early candidate read as a visible bullseye — the
  exact "shape/artifact" §2 forbids; rejected).
- No drifting particles in this pass — particles are motion and must animate to
  avoid reading as "static blotches" (§2, §11), so they belong to the motion pass
  (E), not a baked texture.

### Implementation
- Modify **only** `FeltBackgroundGenerator.cs`:
  - Replace the three palette constants with the approved jade values.
  - Add the deliberate corner-darkening term.
  - Add the faint diagonal-lattice term (guarded, low amplitude) alongside grain.
  - Keep the bake target `Assets/Textures/Felt.png`, size 1024, sRGB, Clamp,
    mipChain — so `FeltScreen.mat`, `Felt.mat`, and every consumer stay wired
    unchanged (no `GameSceneBuilder3D` edits).
- Regenerate via `RegenerateAll` (or the menu item) so `Felt.png` re-bakes; both
  screens pick it up through the shared `FeltScreen.mat`.
- The reference Python prototype (`scratchpad/jade_bg.py`) holds the exact
  formulas the C# must match; the C# bake is the source of truth once ported.

### Testing / verification
- No unit tests (pure baked texture / editor generator).
- Bake `Felt.png`, then **inspect the PNG directly** before building — confirm the
  jade tone, upper bloom, dark corners, invisible lattice.
- Build + deploy to device RZ8M70HFTCM; screenshot both the level-start screen and
  the game board; confirm the deep-jade backdrop reads premium behind the ivory
  tiles and does not compete with foreground contrast (squint test, §2).
- §10 gate: this pass changes only a baked texture consumed by an already-present
  Unlit quad — zero new draw calls, no runtime cost. Confirm draw-call count
  unchanged in the Frame Debugger if any doubt.

### Boundaries
Original art/code only. Pass A touches one generator and one baked texture; it
does not restyle tiles, buttons, panels, or type (later passes).
