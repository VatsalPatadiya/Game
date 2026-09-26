# Sub-project #4 — Progression & Meta

## Status
Scope approved in chat 2026-09-09. Fourth sub-project of the master plan.

## Scope (user-confirmed)
IN: level select + 0-3 stars + sequential unlock; save/load; settings (sound/music)
+ pause menu. Levels are **data-driven**. Save is a **JSON file** in
`Application.persistentDataPath`.
DEFERRED: currency/coins (#6-adjacent); daily challenge (#5); real level content
authoring (#7 — this sub-project ships a small starter set + the data pipeline).

## Decomposition (passes, each independently committable)
- **4A — Foundation (domain, tested)**: level-data model + a small level catalog,
  a star-rating rule, a `GameProgress` model, and a JSON `SaveSystem`. No UI.
- **4B — Level select screen**: a scrollable/paged list of levels showing number,
  lock state, and earned stars; PLAY enters the selected level. Reuses the jade
  chrome.
- **4C — Gameplay integration**: on win, compute stars, unlock the next level,
  save; the level-start screen reflects the chosen level's data.
- **4D — Settings + pause**: settings (sound/music toggles, persisted) and an
  in-game pause menu via the hamburger button.

## 4A design (this pass)

### Data model (domain, no UnityEngine dependency where possible)
- `LevelData` (serializable): `int LevelId`, `string Name`, `int TileCount` (or
  shape id), `int Difficulty` (1-5), `int ParMoves`/`ParSeconds` for star scoring.
  Concrete level list lives in a `LevelCatalog` (an ordered `List<LevelData>`);
  a starter set of ~5 is defined in code now, expandable to authored assets in #7.
- `LevelResult`: `int LevelId`, `int Stars` (0-3), `int Score`.
- `GameProgress` (serializable): `int HighestUnlockedLevelId`, and a map
  `LevelId -> stars` (`List<LevelStarEntry>` for JSON friendliness).

### Star rating rule
`StarRating.Evaluate(LevelData level, int aidsUsed, bool won)`:
- 0 stars if not won.
- Won → base 1 star; +1 if `aidsUsed <= level.ParAids` (define ParAids on
  LevelData, e.g. 2); +1 if won with 0 aids used. (Simple, deterministic, no
  timing dependency for v1; refine later.)

### SaveSystem (JSON)
- `SaveSystem.Save(GameProgress)` writes `progress.json` to
  `Application.persistentDataPath` via `JsonUtility.ToJson`.
- `SaveSystem.Load()` returns the parsed `GameProgress` or a fresh default
  (level 1 unlocked) when the file is missing/corrupt.
- Pure enough to test: inject the file path so EditMode tests use a temp path.

### Testing (4A)
- `StarRating.Evaluate`: not-won=0; won-with-many-aids=1; won-within-par=2;
  won-no-aids=3.
- `SaveSystem` round-trip: Save then Load returns equal progress; Load of a
  missing file returns the default (level 1 unlocked).
- `GameProgress.RecordResult` keeps the max stars per level and advances
  `HighestUnlockedLevelId`.

## Later passes (4B-4D) — summarized
- 4B level select: a new scene section or screen (jade chrome) listing
  `LevelCatalog` with lock/star state from `GameProgress`.
- 4C: `GameController` reports `LevelResult` on win → `GameProgress.RecordResult`
  → `SaveSystem.Save`; level-start screen reads the selected `LevelData`.
- 4D: `SettingsData` (sound/music bools) persisted via `SaveSystem` (or a second
  json); `AudioManager` reads them; pause menu overlay wired to the hamburger.

## Boundaries
Original code only. This sub-project adds systems; visual style stays the
committed jade/gold. Currency and authored-level content are out of scope here.
