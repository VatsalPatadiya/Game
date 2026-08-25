<!-- unity/README.md -->
# GameClient (Unity)

Unity 6 client for the mahjong-style tile-matching game. Wraps the
`domain/` C# library (ported into `Assets/Scripts/Domain`, zero
`UnityEngine` references enforced by `Domain.asmdef`) in a playable board,
tap input, and HUD. See
`docs/superpowers/specs/2026-08-25-unity-integration-design.md` for the
design this implements.

## Opening the project

Open `unity/GameClient` in Unity Hub with Unity 6000.5.9f1. Open
`Assets/Scenes/Game.unity` and press Play to test in the Editor.

## Running the EditMode tests

    UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
    "$UNITY" -batchmode -nographics -projectPath unity/GameClient \
      -runTests -testPlatform EditMode -testResults /tmp/results.xml -quit

## Building for Android

    UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
    "$UNITY" -batchmode -nographics -projectPath unity/GameClient \
      -executeMethod AndroidBuilder.Build -quit

Output: `unity/GameClient/Builds/GameClient.apk` (git-ignored).

## Installing on a device

1. On the Android phone: Settings → About phone → tap "Build number" 7
   times to enable Developer Options, then enable "USB debugging" under
   Developer Options.
2. Connect the phone via USB, accept the "Allow USB debugging" prompt.
3. `adb install unity/GameClient/Builds/GameClient.apk`

If `adb` isn't on PATH, it's bundled with the Android SDK Unity installed:
`~/Library/Android/sdk/platform-tools/adb` (or wherever Unity Hub placed
the Android SDK on this machine — check Unity's Android External Tools
preferences if the default path doesn't exist).

## Known placeholder-art gaps (intentional, deferred to Sub-project 4)

- Tiles render as plain colored squares (hash-of-value → hue), not
  distinct icons per value — accessibility's "icon+color, never
  color-alone" rule is only half-satisfied until real per-value art
  lands.
- Undo and Shuffle both trigger a full board rebuild rather than a
  smooth partial transition — correct, but visually blunt.

## Layout

- `Assets/Scripts/Domain` — ported `domain/src`, `noEngineReferences: true`
- `Assets/Scripts/Tests/EditMode` — ported `domain/tests`, run via Unity
  Test Framework
- `Assets/Scripts/Presentation` — Board (`BoardView`, `TileView`,
  `TileInputController`), HUD, Effects, `GameController`
- `Assets/Scripts/Data` — `AccessibilityTokens`, `LevelShapeAsset`,
  `TileSetAsset` ScriptableObject classes
- `Assets/Scripts/Editor` — headless asset/scene/build generation scripts
  (not shipped in the build)
- `Assets/Data`, `Assets/Prefabs`, `Assets/Scenes` — generated assets

## Next step (Sub-project 3 / future work)

Save/load, level map, daily challenge, currency (Sub-project 3), real art
and more levels (Sub-project 4), ads/IAP (Sub-project 5) are all
explicitly out of scope here — see the design spec's Open Items.
