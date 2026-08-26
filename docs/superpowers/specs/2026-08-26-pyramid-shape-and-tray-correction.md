# Pyramid Shape & Tray/Tile Visual Correction Spec (Round 5)

**Basis for this document**: frame-by-frame extraction from the screen recording you provided (`ScreenRecording_08-26-2026 14-32-07_1.MP4`, Vita Mahjong gameplay), pulled via `ffmpeg` and inspected at full resolution. Frames referenced below are all from that recording. This documents what's actually wrong and why, before making any changes.

---

## 1. What's actually wrong

### 1a. Board shape

Our board (`PyramidShapeBuilder.BuildRandom`) generates a fixed 3-layer shape: a 6×4 rectangle (layer 0), a 4×2 rectangle centered on top (layer 1), and a 2×1 cap (layer 2) — 34 tiles total. Visually this reads as **a flat rectangular grid with a small rectangular bump in the middle**, which is exactly what you're seeing in the current build's screenshot: four clean rows of six, with a slightly-different-looking cluster in the middle two rows.

The reference game's board is a classic mahjong-solitaire **"turtle"** layout: multiple stacked layers, wide at the top, narrowing into **two separate pillars with a hollow gap between them** through the middle rows, then widening again toward the bottom. This is a standard, decades-old genre convention (not something specific to Vita's IP) — but our board doesn't attempt it at all; it's just a rectangle-plus-bump.

**Evidence**: `board_hires.png` + `board_hires_bottom.png` (stitched, full board visible in `check_18s.png` at 18s into the recording) show a tall, symmetric, tapered silhouette with a visible hollow column running through the center-left/center-right split, several tiles deep in places (visible stacked-tile edges), not a rectangle.

### 1b. Tray

I first assumed the reference had no tray at all (classic mahjong solitaire doesn't). That's wrong — there **is** a tray-equivalent, I just didn't recognize it initially because it's not styled as four separate boxed slots the way ours is.

**Evidence**: `bar_over_time.png`, six frames stacked (12s/16s/18s/22s/25s/28s) of the same screen region:
- 12s: the bar under the "40 / 90 / 180" progress row is empty.
- 16s–18s: one tile (a drink-glass icon) sits in the leftmost section of the bar.
- 22s: a second tile (a can icon) appears next to it, and a particle burst fires — the two just matched.
- 25s: back down to one tile — confirms the pair cleared.

So the mechanic is the same as ours (tap a free tile → it lands in a holding area → two matching tiles there clear with an effect). The difference is purely **visual construction**: theirs is one continuous rounded bar with thin internal divider lines between sections; ours is four fully separate square cards with visible gaps between them (see the attached current-build screenshot — four distinct gray rounded squares with real spacing, not a single bar with dividers).

### 1c. Tile card — mostly confirmed correct, one real difference

Comparing our current card construction (off-white face, colored accent border, centered icon, drop shadow) against the reference tiles in `board_hires.png`:

- Off-white dominant card face: **matches**.
- Rounded corners: **matches** (comparable radius).
- Drop shadow / layer depth cue: **matches**.
- Accent border: **differs**. The reference border is a thin, uniform mint/sage-green line (roughly 3–5% of tile width) on every tile regardless of value. Ours is a thick (~8%) border whose *color* changes per tile value.

I'm not copying the reference's uniform-green approach wholesale, and here's why: their icons are full illustrated artwork (a cat, a tomato, a soup can) that are trivially distinguishable from each other by shape alone, so they don't need color to help differentiate values. Ours are abstract geometric icons (7 shapes), and per the accessibility requirement from the very first spec in this project ("icon + color, never color alone"), our accent color is doing real work — it's what lets 7 shapes × 4 colors cover 28 distinct values instead of just 7. Switching to a uniform border color would remove that and force either visible collisions past 7 pair-values or a much larger icon set. So: **thin the border to be closer to the reference's proportions, keep per-value coloring**.

**Icons themselves are intentionally not being changed** — the illustrated cat/can/tomato artwork is Vita's owned content; the first spec in this project explicitly ruled out reproducing reference artwork/characters, and that still stands. Our geometric icon set is the correct call here, just with a thinner border.

---

## 2. What's changing

### 2a. New board shape — `TurtleShapeBuilder`

A new shape generator (`GameDomain.Generation.TurtleShapeBuilder`), replacing `PyramidShapeBuilder.BuildRandom` as what `GameController.LoadLevel()` actually uses. Layout (original, inspired by the genre convention above — not a tile-by-tile trace of Vita's specific board):

- **Layer 0** (base): rows of width 8, 10, 6+6 (hollow split), 6+6 (hollow split), 10, 8 — a symmetric tapered silhouette with a hollow column through the middle two rows.
- **Layer 1**: a centered 4×3 block sitting on top of layer 0's middle.
- **Layer 2**: a centered 2×1 cap.

This keeps the existing `TileSlot`/`CoveredByIds`/`LeftNeighborId`/`RightNeighborId` data model (no domain matching-logic changes — `FreedomRuleCalculator`, `MatchValidator`, `TrayManager` are untouched) and only changes the *geometry* fed into it, the same category of change `PyramidShapeBuilder` and `LayeredRowShapeBuilder` already are.

Because this is a new, more complex (hollow) topology, it gets its own dedicated solvability regression test (200 generated boards, independently verified via the existing backtracking solver) before it ships — same rigor as the round-3 50-tile bucket, not assumed just because smaller shapes passed.

### 2b. Tray — rebuild as a single divided bar

Replace the four-separate-square-cards construction with one continuous rounded bar (matching the score-pill/HUD card treatment already established) with thin internal divider lines between the 4 sections, tile content sitting directly in each section rather than in a separately-carded slot.

### 2c. Card accent border — thinner

Reduce `CardStyle.AccentSizeRatio` so the border works out to roughly 4% of tile width (down from the current 8%), closer to the reference's proportions, while keeping per-value accent coloring exactly as-is.

---

## 3. What's explicitly not changing

- Tray *mechanic* (tap → hold → match-in-tray → clear) — confirmed present in the reference too, just styled differently. Not being removed.
- Icon artwork — staying with our original geometric set, not reproducing Vita's illustrated icons.
- Everything from rounds 2–4 not mentioned above (deal-in animation, tap-to-tray timing, particle burst/praise text, popup, HUD buttons) — unaffected.

---

## Acceptance Checklist

- [ ] Board generates via a new turtle-style shape (wide top, hollow-middle twin pillars, wide bottom, multiple layers) instead of a rectangle-plus-bump
- [ ] New shape has a dedicated solvability regression test (200 boards, backtracking-solver verified) that passes before shipping
- [ ] No domain matching/freedom-rule logic changed — only shape geometry
- [ ] Tray renders as one continuous divided bar, not four separated square cards
- [ ] Tray still functions identically (tap-to-tray timing, match-clear, reflow, celebration) — visual construction only
- [ ] Card accent border thinned to ~4% of tile width, still per-value colored (not switched to a uniform color)
- [ ] Icon artwork unchanged (original geometric set, not reference illustrations)
