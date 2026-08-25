# GameDomain

Pure C# core gameplay logic for the mahjong-style tile-matching game — no
`UnityEngine` reference anywhere in `src/`. See
`docs/superpowers/specs/2026-08-25-core-gameplay-foundation-design.md` for
the design this implements.

## Running tests

    export PATH="$HOME/.dotnet:$PATH"   # if dotnet isn't already on PATH
    cd domain
    dotnet test

## Layout

- `src/Model` — TileSlot, LevelDefinition, TileCell, Move, BoardState
- `src/Generation` — FreedomRuleCalculator, ReverseConstructionSolver, BoardGenerator
- `src/Gameplay` — MatchValidator, HintFinder, UndoStack, ShuffleService, ComboScorer
- `tests/Fixtures` — hand-authored small/medium/large layered-turtle test shapes
- `tests/Solving` — an independent backtracking solver used only to verify
  generator output; not shipped production code
- `tests/Regression` — 200-boards-per-shape solvability regression suite

## Next step (Sub-project 2 / Plan B)

This entire `src/` folder is designed to be copied into a Unity project's
`Assets/Scripts/Domain`, scoped by its own `.asmdef` with zero Unity engine
references, per the architecture decision in the design spec. The `tests/`
folder's NUnit tests are portable to Unity's Test Framework (EditMode) with
minimal changes, since both run on NUnit.
