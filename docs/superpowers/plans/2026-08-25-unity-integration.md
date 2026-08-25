# Unity Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wrap the `domain/` C# library in a real Unity project, render the layered-turtle board, wire tap input to the gameplay services, build an accessibility-first HUD, and produce an installable Android `.apk`.

**Architecture:** A Unity 6 project at `unity/GameClient/`. `domain/src` is copied into `Assets/Scripts/Domain` behind an `.asmdef` with `noEngineReferences: true` — enforced by the Unity compiler, not just convention — so the zero-Unity-dependency boundary from Sub-project 1 physically cannot be violated. All asset creation (prefabs, scenes, ScriptableObject instances) is done by headless Editor C# scripts invoked via `Unity -batchmode -executeMethod`, verified working on this machine before this plan was written (project creation, `-executeMethod`, and `PrefabUtility.SaveAsPrefabAsset` all confirmed functional with Unity 6000.5.9f1 and no license blocker).

**Tech Stack:** Unity 6000.5.9f1 (already installed), Android Build Support + SDK/NDK/OpenJDK (already installed via `unityhub --headless install-modules`), Unity Test Framework (NUnit-based, reusing the domain library's existing tests).

**Spec:** `docs/superpowers/specs/2026-08-25-unity-integration-design.md`

## Global Constraints

- `Assets/Scripts/Domain` must never reference `UnityEngine` — enforced by `Domain.asmdef`'s `"noEngineReferences": true`, which turns any violation into a Unity compiler error.
- Unity project lives at `unity/GameClient/` (repo root, sibling to `domain/`).
- Android is the only build target. Minimum API level 24, target API level 34.
- Every Unity invocation in this plan uses `-batchmode -nographics -projectPath unity/GameClient` plus a `-logFile <path>` so output is captured to a file, not the terminal — Unity's batch-mode logs are very long; grep the log file for the specific markers each task specifies rather than dumping the whole thing.
- The .NET SDK used for `domain/` is at `~/.dotnet`; Unity has its own bundled toolchain and does not need it, but keep `export PATH="$HOME/.dotnet:$PATH"` in mind if a step ever shells out to `dotnet`.
- Unity path for all commands: `UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"`.
- If any `-executeMethod` invocation exits non-zero, or the log contains `error CS` (a C# compiler error) or an uncaught exception, the task is not done — do not proceed past a task with unclean Unity output, even if a prefab/asset file was still written to disk.
- Presentation-layer components (anything under `Assets/Scripts/Presentation`) are verified by manual Play Mode testing at the end of this plan (Task 12), not by per-task automated tests — this matches the spec's own Testing Strategy, since MonoBehaviour UI/input code isn't meaningfully unit-testable without a much larger PlayMode-test harness this sub-project doesn't need.

---

### Task 1: Unity project bootstrap

**Files:**
- Create: `unity/GameClient/` (via headless Unity project creation)
- Create: `unity/.gitignore`

**Interfaces:**
- Produces: an empty Unity 6 project at `unity/GameClient/`, Android as the active build target, committed to git with Unity's standard ignore rules in place.

- [ ] **Step 1: Create the project headlessly**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
mkdir -p unity
"$UNITY" -batchmode -nographics -createProject unity/GameClient -quit -logFile /tmp/unity-task1-create.log
echo "Exit code: $?"
grep -iE "error|exception" /tmp/unity-task1-create.log || echo "No errors found in log"
```

Expected: exit code 0, no `error`/`exception` lines. `unity/GameClient/Assets`, `ProjectSettings`, `Packages` directories exist.

- [ ] **Step 2: Add the Unity `.gitignore`**

```gitignore
# unity/.gitignore
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]serSettings/
[Mm]emoryCaptures/
.vs/
*.pidb.meta
*.pdb.meta
*.mdb.meta
sysinfo.txt
*.apk
*.aab
*.unitypackage
crashlytics-build.properties
```

- [ ] **Step 3: Switch the active build target to Android**

```bash
mkdir -p unity/GameClient/Assets/Scripts/Editor
cat > unity/GameClient/Assets/Scripts/Editor/ProjectSetup.cs << 'EOF'
using UnityEditor;
using UnityEngine;

public static class ProjectSetup
{
    public static void SwitchToAndroid()
    {
        bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        Debug.Log("PROJECT_SETUP_ANDROID_SWITCH_RESULT: " + switched);
        Debug.Log("PROJECT_SETUP_ACTIVE_TARGET: " + EditorUserBuildSettings.activeBuildTarget);
    }
}
EOF
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient -executeMethod ProjectSetup.SwitchToAndroid -quit -logFile /tmp/unity-task1-android.log
echo "Exit code: $?"
grep -E "PROJECT_SETUP_ANDROID_SWITCH_RESULT|PROJECT_SETUP_ACTIVE_TARGET|error CS|Exception" /tmp/unity-task1-android.log
```

Expected: `PROJECT_SETUP_ANDROID_SWITCH_RESULT: True` (or `False` if it was already Android — either is fine as long as the next line confirms it) and `PROJECT_SETUP_ACTIVE_TARGET: Android`. No `error CS` or unhandled `Exception` lines.

- [ ] **Step 4: Commit**

```bash
git add unity/
git commit -m "chore: bootstrap Unity project, target Android"
```

---

### Task 2: Port the domain library into the Unity project

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Domain/` (copy of `domain/src/`)
- Create: `unity/GameClient/Assets/Scripts/Domain/Domain.asmdef`
- Create: `unity/GameClient/Assets/Scripts/Tests/EditMode/` (copy of `domain/tests/`)
- Create: `unity/GameClient/Assets/Scripts/Tests/EditMode/Domain.Tests.asmdef`

**Interfaces:**
- Produces: the exact same `GameDomain.Model`/`GameDomain.Generation`/`GameDomain.Gameplay` public API Sub-project 1 built and tested (`BoardGenerator.Generate`, `MatchValidator.TryMatch`, `HintFinder.FindFreePair`, `UndoStack.TryUndo`, `ShuffleService.Shuffle`, `ComboScorer`), now compiled inside Unity and verified via the same tests, ported to run under Unity Test Framework (EditMode). Every later task in this plan depends on this exact API surface.

- [ ] **Step 1: Copy the domain source tree**

```bash
mkdir -p unity/GameClient/Assets/Scripts/Domain
cp -r domain/src/Model unity/GameClient/Assets/Scripts/Domain/
cp -r domain/src/Generation unity/GameClient/Assets/Scripts/Domain/
cp -r domain/src/Gameplay unity/GameClient/Assets/Scripts/Domain/
find unity/GameClient/Assets/Scripts/Domain -name "*.cs" | wc -l
```

Expected: 14 `.cs` files copied (5 Model, 4 Generation, 5 Gameplay).

- [ ] **Step 2: Write `Domain.asmdef`**

```json
{
    "name": "Domain",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": true
}
```

Save as `unity/GameClient/Assets/Scripts/Domain/Domain.asmdef`. `"noEngineReferences": true` is the enforcement mechanism for this plan's hardest constraint — Unity will refuse to compile this assembly if any file under it references `UnityEngine`.

- [ ] **Step 3: Copy the domain test tree and add the missing `using` statements**

The domain project's test `.csproj` had `ImplicitUsings` enabled, so its test files never needed an explicit `using NUnit.Framework;`. Unity has no such implicit-usings mechanism, so every copied test file needs that line added if it isn't already there.

```bash
mkdir -p unity/GameClient/Assets/Scripts/Tests/EditMode
cp -r domain/tests/Fixtures unity/GameClient/Assets/Scripts/Tests/EditMode/
cp -r domain/tests/Model unity/GameClient/Assets/Scripts/Tests/EditMode/
cp -r domain/tests/Generation unity/GameClient/Assets/Scripts/Tests/EditMode/
cp -r domain/tests/Solving unity/GameClient/Assets/Scripts/Tests/EditMode/
cp -r domain/tests/Regression unity/GameClient/Assets/Scripts/Tests/EditMode/
cp -r domain/tests/Gameplay unity/GameClient/Assets/Scripts/Tests/EditMode/

for f in $(find unity/GameClient/Assets/Scripts/Tests/EditMode -name "*.cs"); do
  if ! grep -q "using NUnit.Framework;" "$f"; then
    sed -i '' '1i\
using NUnit.Framework;
' "$f"
  fi
done

find unity/GameClient/Assets/Scripts/Tests/EditMode -name "*.cs" | wc -l
grep -L "using NUnit.Framework;" $(find unity/GameClient/Assets/Scripts/Tests/EditMode -name "*.cs") || echo "All files have the using statement"
```

Expected: 14 `.cs` files copied, and the final `grep -L` (files *missing* the using statement) prints nothing but the "All files have..." fallback message.

- [ ] **Step 4: Write `Domain.Tests.asmdef`**

```json
{
    "name": "Domain.Tests",
    "rootNamespace": "",
    "references": [
        "Domain",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Save as `unity/GameClient/Assets/Scripts/Tests/EditMode/Domain.Tests.asmdef`.

- [ ] **Step 5: Run the ported tests headlessly and verify they pass**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient -runTests -testPlatform EditMode -testResults /tmp/unity-task2-results.xml -logFile /tmp/unity-task2-run.log
echo "Exit code: $?"
grep -E "error CS|CompilerError" /tmp/unity-task2-run.log
grep -oE 'result="[A-Za-z]+"' /tmp/unity-task2-results.xml | sort | uniq -c
grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' /tmp/unity-task2-results.xml | head -1
```

Expected: no `error CS`/`CompilerError` lines. The results XML's top-level `<test-run ... total="33" passed="33" failed="0" ...>` (33, matching the domain library's final verified count — 32 domain tests plus the one still-retained `UnitTest1`-style scaffold test does *not* apply here since this port doesn't copy Sub-project 1's own scaffold-only test; if the count differs, read the XML directly to see which test is missing or extra and report it rather than assuming — do not force the number to match blindly).

If `-runTests` reports 0 tests found: this usually means the `.asmdef` `defineConstraints`/`includePlatforms` are misconfigured (the assembly isn't recognized as a test assembly) — re-check Step 4's file against the exact JSON above rather than guessing at a fix.

- [ ] **Step 6: Commit**

```bash
git add unity/
git commit -m "feat: port GameDomain library into Unity project (EditMode tests ported)"
```

---

### Task 3: Promote `LayeredRowShapeBuilder` to production code

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Domain/Generation/LayeredRowShapeBuilder.cs`
- Modify: `unity/GameClient/Assets/Scripts/Tests/EditMode/Fixtures/TestLayoutShapes.cs`

**Interfaces:**
- Consumes: `GameDomain.Model.TileSlot` (already ported).
- Produces: `GameDomain.Generation.LayeredRowShapeBuilder.Build(int[] rowLengthsByLayer) : List<TileSlot>` — the same topology-construction algorithm `TestLayoutShapes.BuildLayeredRowShape` already proved correct (structural validity, 600-board solvability regression), now available to production code so real levels can use it. Task 4's `LevelShapeAsset` calls this.

- [ ] **Step 1: Add the production builder**

```csharp
// unity/GameClient/Assets/Scripts/Domain/Generation/LayeredRowShapeBuilder.cs
using System.Collections.Generic;
using GameDomain.Model;

namespace GameDomain.Generation
{
    public static class LayeredRowShapeBuilder
    {
        public static List<TileSlot> Build(int[] rowLengthsByLayer)
        {
            var slots = new List<TileSlot>();
            var byLayerIndex = new Dictionary<(int layer, int index), TileSlot>();

            for (int layer = 0; layer < rowLengthsByLayer.Length; layer++)
            {
                int length = rowLengthsByLayer[layer];
                for (int index = 0; index < length; index++)
                {
                    var slot = new TileSlot
                    {
                        Id = "L" + layer + "_" + index,
                        X = index,
                        Y = 0,
                        Layer = layer,
                        CoveredByIds = new List<string>(),
                        LeftNeighborId = index > 0 ? "L" + layer + "_" + (index - 1) : null,
                        RightNeighborId = index < length - 1 ? "L" + layer + "_" + (index + 1) : null
                    };
                    slots.Add(slot);
                    byLayerIndex[(layer, index)] = slot;
                }
            }

            for (int layer = 1; layer < rowLengthsByLayer.Length; layer++)
            {
                int upperLength = rowLengthsByLayer[layer];
                int lowerLength = rowLengthsByLayer[layer - 1];
                for (int index = 0; index < upperLength; index++)
                {
                    var upperSlot = byLayerIndex[(layer, index)];
                    if (index < lowerLength && byLayerIndex.TryGetValue((layer - 1, index), out var lowerA))
                        lowerA.CoveredByIds.Add(upperSlot.Id);
                    if (index + 1 < lowerLength && byLayerIndex.TryGetValue((layer - 1, index + 1), out var lowerB))
                        lowerB.CoveredByIds.Add(upperSlot.Id);
                }
            }

            return slots;
        }
    }
}
```

This is the exact algorithm from `TestLayoutShapes.BuildLayeredRowShape`, moved to production code unchanged.

- [ ] **Step 2: Make the test fixture delegate to it, so there is exactly one copy of this algorithm**

Replace the body of `unity/GameClient/Assets/Scripts/Tests/EditMode/Fixtures/TestLayoutShapes.cs`'s `BuildLayeredRowShape` method (keep the file, keep `SmallShape`/`MediumShape`/`LargeShape`) with:

```csharp
using System.Collections.Generic;
using GameDomain.Generation;
using GameDomain.Model;

namespace GameDomain.Tests.Fixtures
{
    public static class TestLayoutShapes
    {
        public static List<TileSlot> BuildLayeredRowShape(int[] rowLengthsByLayer) =>
            LayeredRowShapeBuilder.Build(rowLengthsByLayer);

        public static List<TileSlot> SmallShape() => BuildLayeredRowShape(new[] { 8 });
        public static List<TileSlot> MediumShape() => BuildLayeredRowShape(new[] { 12, 6 });
        public static List<TileSlot> LargeShape() => BuildLayeredRowShape(new[] { 20, 12, 6, 2 });
    }
}
```

- [ ] **Step 3: Re-run the full EditMode suite and confirm it's still green**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient -runTests -testPlatform EditMode -testResults /tmp/unity-task3-results.xml -logFile /tmp/unity-task3-run.log
echo "Exit code: $?"
grep -E "error CS|CompilerError" /tmp/unity-task3-run.log
grep -oE 'total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' /tmp/unity-task3-results.xml | head -1
```

Expected: same pass count as Task 2 (the fixture's *behavior* is unchanged, only its implementation now delegates instead of duplicating) — 0 failures, no compiler errors. This proves the promoted production algorithm produces identical results to what the 600-board regression suite already validated.

- [ ] **Step 4: Commit**

```bash
git add unity/
git commit -m "feat: promote LayeredRowShapeBuilder to production Domain code"
```

---

### Task 4: Data layer — AccessibilityTokens, LevelShapeAsset, TileSetAsset

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Data/AccessibilityTokens.cs`
- Create: `unity/GameClient/Assets/Scripts/Data/LevelShapeAsset.cs`
- Create: `unity/GameClient/Assets/Scripts/Data/TileSetAsset.cs`
- Create: `unity/GameClient/Assets/Scripts/Editor/DataAssetGenerator.cs`
- Create (generated by Step 3): `unity/GameClient/Assets/Data/DefaultAccessibilityTokens.asset`, `unity/GameClient/Assets/Data/SmallTestLevel.asset`, `unity/GameClient/Assets/Data/DefaultTileSet.asset`

**Interfaces:**
- Produces: `GameClient.Data.AccessibilityTokens` (fields: `MinTapTargetSize`, `MinBodyTextSize`, `FreeTileColor`, `BlockedTileColor`, `HighlightColor`, `HudTextColor`, `HudBackgroundColor`), `GameClient.Data.LevelShapeAsset` (fields: `LevelId`, `RowLengthsByLayer`, `TileSetId`), `GameClient.Data.TileSetAsset` (field: `TileSetId`) — plus one generated instance of each, which Task 8 (`GameController`) and Task 11 (scene assembly) reference directly.

- [ ] **Step 1: Write the ScriptableObject class definitions**

```csharp
// unity/GameClient/Assets/Scripts/Data/AccessibilityTokens.cs
using UnityEngine;

namespace GameClient.Data
{
    [CreateAssetMenu(fileName = "AccessibilityTokens", menuName = "GameClient/Accessibility Tokens")]
    public sealed class AccessibilityTokens : ScriptableObject
    {
        [Min(0)] public float MinTapTargetSize = 88f;
        [Min(0)] public float MinBodyTextSize = 20f;
        public Color FreeTileColor = Color.white;
        public Color BlockedTileColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        public Color HighlightColor = new Color(1f, 0.85f, 0.2f, 1f);
        public Color HudTextColor = Color.black;
        public Color HudBackgroundColor = Color.white;
    }
}
```

```csharp
// unity/GameClient/Assets/Scripts/Data/LevelShapeAsset.cs
using UnityEngine;

namespace GameClient.Data
{
    [CreateAssetMenu(fileName = "LevelShapeAsset", menuName = "GameClient/Level Shape")]
    public sealed class LevelShapeAsset : ScriptableObject
    {
        public int LevelId;
        public int[] RowLengthsByLayer;
        public string TileSetId;
    }
}
```

```csharp
// unity/GameClient/Assets/Scripts/Data/TileSetAsset.cs
using UnityEngine;

namespace GameClient.Data
{
    [CreateAssetMenu(fileName = "TileSetAsset", menuName = "GameClient/Tile Set")]
    public sealed class TileSetAsset : ScriptableObject
    {
        public string TileSetId;
    }
}
```

(`TileSetAsset` is intentionally minimal for this sub-project — placeholder tiles are plain colored shapes rendered directly by `TileView`, not sprite lookups. Real per-value sprite art is Sub-project 4's Content Pipeline work; this field only exists so `LevelDefinition.TileSetId` has somewhere to point.)

- [ ] **Step 2: Write the headless generator script**

```csharp
// unity/GameClient/Assets/Scripts/Editor/DataAssetGenerator.cs
using System.IO;
using GameClient.Data;
using UnityEditor;
using UnityEngine;

public static class DataAssetGenerator
{
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Data");

        var tokens = ScriptableObject.CreateInstance<AccessibilityTokens>();
        AssetDatabase.CreateAsset(tokens, "Assets/Data/DefaultAccessibilityTokens.asset");

        var tileSet = ScriptableObject.CreateInstance<TileSetAsset>();
        tileSet.TileSetId = "default";
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

- [ ] **Step 3: Run it headlessly**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient -executeMethod DataAssetGenerator.Generate -quit -logFile /tmp/unity-task4-gen.log
echo "Exit code: $?"
grep -E "DATA_ASSET_GENERATOR_DONE|error CS|Exception" /tmp/unity-task4-gen.log
ls unity/GameClient/Assets/Data/
```

Expected: `DATA_ASSET_GENERATOR_DONE` present, no `error CS`/`Exception`, and `Assets/Data/` contains the three `.asset` files.

- [ ] **Step 4: Commit**

```bash
git add unity/
git commit -m "feat: add accessibility tokens, level shape, and tile set data assets"
```

---

### Task 5: TileView component and `Tile.prefab`

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Presentation/Board/TileView.cs`
- Create: `unity/GameClient/Assets/Scripts/Editor/TilePrefabGenerator.cs`
- Create (generated by Step 3): `unity/GameClient/Assets/Prefabs/Tile.prefab`

**Interfaces:**
- Produces: `GameClient.Presentation.Board.TileView` — a `MonoBehaviour` with `SlotId` (string, read-only), `Layer` (int, read-only), `Initialize(string slotId, int layer, Color tileColor)`, `SetFree(bool isFree)`, `Highlight()`, and `PlayClearAndDestroy()` (coroutine: scales to zero over 0.2s then destroys the GameObject). Task 6 (`BoardView`) instantiates `Tile.prefab` and calls these methods; Task 7 (`TileInputController`) reads `SlotId`/`Layer` off tiles it hits.

- [ ] **Step 1: Write `TileView`**

```csharp
// unity/GameClient/Assets/Scripts/Presentation/Board/TileView.cs
using System.Collections;
using UnityEngine;

namespace GameClient.Presentation.Board
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class TileView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _backgroundRenderer;
        [SerializeField] private SpriteRenderer _iconRenderer;
        [SerializeField] private Color _freeColor = Color.white;
        [SerializeField] private Color _blockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Color _highlightColor = new Color(1f, 0.85f, 0.2f, 1f);

        public string SlotId { get; private set; }
        public int Layer { get; private set; }

        public void Initialize(string slotId, int layer, Color tileColor)
        {
            SlotId = slotId;
            Layer = layer;
            if (_iconRenderer != null)
                _iconRenderer.color = tileColor;
        }

        public void SetFree(bool isFree)
        {
            if (_backgroundRenderer != null)
                _backgroundRenderer.color = isFree ? _freeColor : _blockedColor;

            var collider = GetComponent<BoxCollider2D>();
            if (collider != null)
                collider.enabled = isFree;
        }

        public void Highlight()
        {
            if (_backgroundRenderer != null)
                _backgroundRenderer.color = _highlightColor;
        }

        public void PlayClearAndDestroy()
        {
            StartCoroutine(ClearRoutine());
        }

        private IEnumerator ClearRoutine()
        {
            const float duration = 0.2f;
            float elapsed = 0f;
            var startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
```

- [ ] **Step 2: Write the headless prefab generator**

```csharp
// unity/GameClient/Assets/Scripts/Editor/TilePrefabGenerator.cs
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

        var background = new GameObject("Background");
        background.transform.SetParent(root.transform);
        var backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = CreateSquareSprite();
        backgroundRenderer.sortingOrder = 0;

        var icon = new GameObject("Icon");
        icon.transform.SetParent(root.transform);
        var iconRenderer = icon.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = CreateSquareSprite();
        iconRenderer.sortingOrder = 1;
        icon.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

        var collider = root.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        var tileView = root.AddComponent<TileView>();
        var serialized = new SerializedObject(tileView);
        serialized.FindProperty("_backgroundRenderer").objectReferenceValue = backgroundRenderer;
        serialized.FindProperty("_iconRenderer").objectReferenceValue = iconRenderer;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Tile.prefab");
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log("TILE_PREFAB_GENERATOR_DONE");
    }

    private static Sprite CreateSquareSprite()
    {
        var texture = new Texture2D(4, 4);
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }
}
```

The generator asserts nothing explicitly on the two `SerializedProperty` lookups — if either field name in `TileView` doesn't match, `FindProperty` returns `null` and the next line throws `NullReferenceException`, which will show up as `Exception` in the log grep below. Treat any such exception as this task not being done; do not proceed with a prefab whose renderer references failed to wire.

- [ ] **Step 3: Run it headlessly**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient -executeMethod TilePrefabGenerator.Generate -quit -logFile /tmp/unity-task5-gen.log
echo "Exit code: $?"
grep -E "TILE_PREFAB_GENERATOR_DONE|error CS|Exception" /tmp/unity-task5-gen.log
ls unity/GameClient/Assets/Prefabs/Tile.prefab
```

Expected: `TILE_PREFAB_GENERATOR_DONE` present, no `error CS`/`Exception`, `Tile.prefab` exists.

- [ ] **Step 4: Commit**

```bash
git add unity/
git commit -m "feat: add TileView component and generate Tile.prefab"
```

---

### Task 6: BoardView

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Presentation/Board/BoardView.cs`

**Interfaces:**
- Consumes: `GameDomain.Model.BoardState`, `GameDomain.Model.TileSlot`, `GameDomain.Generation.FreedomRuleCalculator.IsFree` (Task 2), `GameClient.Presentation.Board.TileView` (Task 5).
- Produces: `GameClient.Presentation.Board.BoardView` — a `MonoBehaviour` with `Build(BoardState board, Dictionary<string, TileSlot> slotsById)` (destroys any existing tiles, spawns one `TileView` per uncleared cell, positions by `X`/`Y`/`Layer`, then refreshes free/blocked state), `RefreshFreeStates(BoardState board)` (recomputes and re-applies free/blocked state without respawning), `RemoveTiles(IEnumerable<string> slotIds)` (plays each tile's clear animation and removes it from tracking), and `GetTileView(string slotId) : TileView`. Task 8 (`GameController`) drives all of these.

- [ ] **Step 1: Write `BoardView`**

```csharp
// unity/GameClient/Assets/Scripts/Presentation/Board/BoardView.cs
using System.Collections.Generic;
using System.Linq;
using GameDomain.Generation;
using GameDomain.Model;
using UnityEngine;

namespace GameClient.Presentation.Board
{
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private TileView _tilePrefab;
        [SerializeField] private float _cellSize = 1f;

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
                view.Initialize(kv.Key, slot.Layer, ColorForValue(kv.Value.Value));
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

        private static Color ColorForValue(string value)
        {
            int hash = value.GetHashCode();
            float hue = Mathf.Abs(hash % 360) / 360f;
            return Color.HSVToRGB(hue, 0.65f, 0.9f);
        }
    }
}
```

`ColorForValue` is a deliberately simple placeholder — a stable hash-to-hue mapping so every occurrence of the same value renders the same color, satisfying "icon+color, never color-alone" only partially (color is real; a distinct icon per value is Sub-project 4's Content Pipeline work with real art, out of scope here). Note this as a known placeholder-art gap, not a defect.

- [ ] **Step 2: Verify it compiles**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient -quit -logFile /tmp/unity-task6-compile.log
echo "Exit code: $?"
grep -E "error CS" /tmp/unity-task6-compile.log || echo "No compiler errors"
```

Expected: no `error CS` lines. (There's no scene or executeMethod call yet that exercises `BoardView` at runtime — this task only proves it compiles; behavior is verified in Task 12's manual Play Mode pass once the scene wires it up.)

- [ ] **Step 3: Commit**

```bash
git add unity/
git commit -m "feat: add BoardView"
```

---

### Task 7: TileInputController

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Presentation/Board/TileInputController.cs`

**Interfaces:**
- Consumes: `GameClient.Presentation.Board.TileView` (Task 5), `GameClient.Presentation.GameController.OnTileTapped(string slotId)` (Task 8 — forward-referenced here; Task 8 must expose this exact method name).
- Produces: `GameClient.Presentation.Board.TileInputController` — a `MonoBehaviour` that, on tap/click, resolves which `TileView` was hit (by physics overlap at the tap point) and, when multiple layered tiles overlap, picks the one with the highest `Layer` — matching the freedom rule's "nothing above it" semantics — then forwards its `SlotId` to `GameController`.

- [ ] **Step 1: Write `TileInputController`**

```csharp
// unity/GameClient/Assets/Scripts/Presentation/Board/TileInputController.cs
using UnityEngine;

namespace GameClient.Presentation.Board
{
    public sealed class TileInputController : MonoBehaviour
    {
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private GameController _gameController;

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            var worldPoint = _targetCamera.ScreenToWorldPoint(Input.mousePosition);
            var hits = Physics2D.OverlapPointAll(new Vector2(worldPoint.x, worldPoint.y));

            TileView topmost = null;
            foreach (var hit in hits)
            {
                var view = hit.GetComponent<TileView>();
                if (view == null) continue;
                if (topmost == null || view.Layer > topmost.Layer)
                    topmost = view;
            }

            if (topmost != null)
                _gameController.OnTileTapped(topmost.SlotId);
        }
    }
}
```

This references `GameController` by type, which Task 8 creates — this task will not compile cleanly on its own until Task 8 exists. That is expected and acceptable: proceed directly to Task 8 without a separate compile-verification step here (Task 8's own compile check covers both files together).

- [ ] **Step 2: Commit**

```bash
git add unity/
git commit -m "feat: add TileInputController (compiles once GameController exists)"
```

---

### Task 8: GameController

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Presentation/GameController.cs`

**Interfaces:**
- Consumes: `GameDomain.Gameplay.{MatchValidator,HintFinder,UndoStack,ShuffleService,ComboScorer}`, `GameDomain.Generation.{BoardGenerator,LayeredRowShapeBuilder,BoardGenerationException}`, `GameDomain.Model.{LevelDefinition,BoardState,TileSlot}` (Task 2/3), `GameClient.Data.LevelShapeAsset` (Task 4), `GameClient.Presentation.Board.BoardView` (Task 6).
- Produces: `GameClient.Presentation.GameController` — a `MonoBehaviour` with `OnTileTapped(string slotId)` (Task 7 calls this), `OnHintRequested()`, `OnUndoRequested()`, `OnShuffleRequested()` (Task 9's HUD buttons call these), and a public `event Action<int, int> ScoreChanged` (score, comboCount) that Task 9's `ScoreDisplay` subscribes to.

- [ ] **Step 1: Write `GameController`**

```csharp
// unity/GameClient/Assets/Scripts/Presentation/GameController.cs
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
        [SerializeField] private LevelShapeAsset _levelShape;

        private BoardState _board;
        private List<TileSlot> _shape;
        private Dictionary<string, TileSlot> _slotsById;
        private string _selectedSlotId;
        private readonly ComboScorer _comboScorer = new ComboScorer();

        public event Action<int, int> ScoreChanged;

        private void Start()
        {
            LoadLevel();
        }

        private void LoadLevel()
        {
            _shape = LayeredRowShapeBuilder.Build(_levelShape.RowLengthsByLayer);
            _slotsById = _shape.ToDictionary(s => s.Id);

            var level = new LevelDefinition
            {
                LevelId = _levelShape.LevelId,
                Shape = _shape,
                TileSetId = _levelShape.TileSetId
            };

            _board = BoardGenerator.Generate(level, new Random());
            _boardView.Build(_board, _slotsById);
            ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
        }

        public void OnTileTapped(string slotId)
        {
            if (_selectedSlotId == null)
            {
                _selectedSlotId = slotId;
                _boardView.GetTileView(slotId)?.Highlight();
                return;
            }

            if (_selectedSlotId == slotId)
            {
                _selectedSlotId = null;
                _boardView.RefreshFreeStates(_board);
                return;
            }

            string firstSelected = _selectedSlotId;
            _selectedSlotId = null;

            bool matched = MatchValidator.TryMatch(_board, _slotsById, firstSelected, slotId);
            if (matched)
            {
                _comboScorer.RegisterMatch(_board, DateTime.UtcNow);
                _boardView.RemoveTiles(new[] { firstSelected, slotId });
                ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
            }

            _boardView.RefreshFreeStates(_board);
        }

        public void OnHintRequested()
        {
            var hint = HintFinder.FindFreePair(_board, _slotsById);
            if (!hint.HasValue) return;

            _boardView.GetTileView(hint.Value.slotIdA)?.Highlight();
            _boardView.GetTileView(hint.Value.slotIdB)?.Highlight();
        }

        public void OnUndoRequested()
        {
            if (UndoStack.TryUndo(_board))
            {
                _boardView.Build(_board, _slotsById);
                ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
            }
        }

        public void OnShuffleRequested()
        {
            try
            {
                ShuffleService.Shuffle(_board, _shape, new Random());
                _boardView.Build(_board, _slotsById);
            }
            catch (BoardGenerationException ex)
            {
                Debug.LogWarning("Shuffle could not find a solvable arrangement, board left unchanged: " + ex.Message);
            }
        }
    }
}
```

`OnUndoRequested` and `OnShuffleRequested` both call `_boardView.Build` (a full rebuild) rather than a more surgical partial update — simplest correct implementation for a first playable version; visually this means a brief respawn-flicker on undo/shuffle rather than a smooth transition. Note this as a known simplification for later polish, not a defect.

- [ ] **Step 2: Verify the project compiles (this also covers Task 7's `TileInputController`, which references this file)**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient -quit -logFile /tmp/unity-task8-compile.log
echo "Exit code: $?"
grep -E "error CS" /tmp/unity-task8-compile.log || echo "No compiler errors"
```

Expected: no `error CS` lines.

- [ ] **Step 3: Commit**

```bash
git add unity/
git commit -m "feat: add GameController orchestrating tap/hint/undo/shuffle flow"
```

---

### Task 9: HUD — ScoreDisplay, HintButton, UndoButton, ShuffleButton

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Presentation/HUD/ScoreDisplay.cs`
- Create: `unity/GameClient/Assets/Scripts/Presentation/HUD/HintButton.cs`
- Create: `unity/GameClient/Assets/Scripts/Presentation/HUD/UndoButton.cs`
- Create: `unity/GameClient/Assets/Scripts/Presentation/HUD/ShuffleButton.cs`

**Interfaces:**
- Consumes: `GameClient.Presentation.GameController` (Task 8) — its `ScoreChanged` event and `OnHintRequested`/`OnUndoRequested`/`OnShuffleRequested` methods.
- Produces: four small `MonoBehaviour`s that Task 11's scene assembly wires to `Text`/`Button` UI elements and to the scene's single `GameController` instance.

- [ ] **Step 1: Write the four HUD components**

```csharp
// unity/GameClient/Assets/Scripts/Presentation/HUD/ScoreDisplay.cs
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class ScoreDisplay : MonoBehaviour
    {
        [SerializeField] private Text _scoreText;
        [SerializeField] private GameController _gameController;

        private void OnEnable()
        {
            if (_gameController != null)
                _gameController.ScoreChanged += HandleScoreChanged;
        }

        private void OnDisable()
        {
            if (_gameController != null)
                _gameController.ScoreChanged -= HandleScoreChanged;
        }

        private void HandleScoreChanged(int score, int comboCount)
        {
            if (_scoreText == null) return;
            _scoreText.text = comboCount > 1
                ? "Score: " + score + "  x" + comboCount
                : "Score: " + score;
        }
    }
}
```

```csharp
// unity/GameClient/Assets/Scripts/Presentation/HUD/HintButton.cs
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class HintButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private GameController _gameController;

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(() => _gameController.OnHintRequested());
        }
    }
}
```

```csharp
// unity/GameClient/Assets/Scripts/Presentation/HUD/UndoButton.cs
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class UndoButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private GameController _gameController;

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(() => _gameController.OnUndoRequested());
        }
    }
}
```

```csharp
// unity/GameClient/Assets/Scripts/Presentation/HUD/ShuffleButton.cs
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class ShuffleButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private GameController _gameController;

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(() => _gameController.OnShuffleRequested());
        }
    }
}
```

- [ ] **Step 2: Verify the project compiles**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient -quit -logFile /tmp/unity-task9-compile.log
echo "Exit code: $?"
grep -E "error CS" /tmp/unity-task9-compile.log || echo "No compiler errors"
```

Expected: no `error CS` lines.

- [ ] **Step 3: Commit**

```bash
git add unity/
git commit -m "feat: add HUD components (score display, hint/undo/shuffle buttons)"
```

---

### Task 10: `ComboEffectFX.prefab`

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Presentation/Effects/ComboEffect.cs`
- Create: `unity/GameClient/Assets/Scripts/Editor/ComboEffectPrefabGenerator.cs`
- Create (generated by Step 3): `unity/GameClient/Assets/Prefabs/ComboEffectFX.prefab`

**Interfaces:**
- Produces: `GameClient.Presentation.Effects.ComboEffect` — a `MonoBehaviour` with `Show(int points)` (sets floating text to `"+" + points`, fades and self-destroys over 0.6s) and `ComboEffectFX.prefab`, a `Text`-based popup instantiated wherever the next sub-project wants to trigger it. Not yet wired to `GameController` in this task — that would need a `Canvas` context this task doesn't have; Task 11's scene assembly wires it in alongside the rest of the HUD.

- [ ] **Step 1: Write `ComboEffect`**

```csharp
// unity/GameClient/Assets/Scripts/Presentation/Effects/ComboEffect.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.Effects
{
    public sealed class ComboEffect : MonoBehaviour
    {
        [SerializeField] private Text _label;
        [SerializeField] private float _duration = 0.6f;
        [SerializeField] private float _riseDistance = 40f;

        public void Show(int points)
        {
            if (_label != null)
                _label.text = "+" + points;
            StartCoroutine(RiseAndFade());
        }

        private IEnumerator RiseAndFade()
        {
            var rectTransform = transform as RectTransform;
            Vector3 start = rectTransform != null ? rectTransform.anchoredPosition3D : transform.localPosition;
            Vector3 end = start + new Vector3(0f, _riseDistance, 0f);

            float elapsed = 0f;
            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _duration);

                if (rectTransform != null)
                    rectTransform.anchoredPosition3D = Vector3.Lerp(start, end, t);
                else
                    transform.localPosition = Vector3.Lerp(start, end, t);

                if (_label != null)
                {
                    var color = _label.color;
                    color.a = 1f - t;
                    _label.color = color;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}
```

- [ ] **Step 2: Write the headless prefab generator**

```csharp
// unity/GameClient/Assets/Scripts/Editor/ComboEffectPrefabGenerator.cs
using System.IO;
using GameClient.Presentation.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ComboEffectPrefabGenerator
{
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Prefabs");

        var root = new GameObject("ComboEffectFX", typeof(RectTransform));
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(160f, 40f);

        var text = root.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.yellow;
        text.text = "+0";

        var comboEffect = root.AddComponent<ComboEffect>();
        var serialized = new SerializedObject(comboEffect);
        serialized.FindProperty("_label").objectReferenceValue = text;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/ComboEffectFX.prefab");
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log("COMBO_EFFECT_PREFAB_GENERATOR_DONE");
    }
}
```

- [ ] **Step 3: Run it headlessly**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient -executeMethod ComboEffectPrefabGenerator.Generate -quit -logFile /tmp/unity-task10-gen.log
echo "Exit code: $?"
grep -E "COMBO_EFFECT_PREFAB_GENERATOR_DONE|error CS|Exception" /tmp/unity-task10-gen.log
ls unity/GameClient/Assets/Prefabs/ComboEffectFX.prefab
```

Expected: `COMBO_EFFECT_PREFAB_GENERATOR_DONE` present, no `error CS`/`Exception`, prefab exists.

- [ ] **Step 4: Commit**

```bash
git add unity/
git commit -m "feat: add ComboEffect component and generate ComboEffectFX.prefab"
```

---

### Task 11: Assemble `Game.scene`

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder.cs`
- Create (generated by Step 2): `unity/GameClient/Assets/Scenes/Game.scene`

**Interfaces:**
- Consumes every component built in Tasks 4–10: `LevelShapeAsset`/`AccessibilityTokens` data assets, `Tile.prefab`, `BoardView`, `TileInputController`, `GameController`, the four HUD components, `ComboEffectFX.prefab`.
- Produces: `Game.scene` — an orthographic camera, a `Board` GameObject (`BoardView` + `TileInputController`), a `Canvas` with score text and three buttons (each wired to its HUD component), and a `GameController` GameObject wiring everything together via `SerializedObject` field assignment. This is the scene Task 12 builds and runs.

- [ ] **Step 1: Write the headless scene builder**

```csharp
// unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder.cs
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
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGO = new GameObject("Main Camera", typeof(Camera));
        var camera = cameraGO.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        cameraGO.tag = "MainCamera";

        var boardGO = new GameObject("Board", typeof(BoardView));
        var boardView = boardGO.GetComponent<BoardView>();
        var tilePrefab = AssetDatabase.LoadAssetAtPath<TileView>("Assets/Prefabs/Tile.prefab");
        RequireNotNull(tilePrefab, "Assets/Prefabs/Tile.prefab as TileView");
        SetField(boardView, "_tilePrefab", tilePrefab);

        var inputGO = new GameObject("TileInputController", typeof(TileInputController));
        var inputController = inputGO.GetComponent<TileInputController>();

        var gameControllerGO = new GameObject("GameController", typeof(GameController));
        var gameController = gameControllerGO.GetComponent<GameController>();
        var levelAsset = AssetDatabase.LoadAssetAtPath<LevelShapeAsset>("Assets/Data/SmallTestLevel.asset");
        RequireNotNull(levelAsset, "Assets/Data/SmallTestLevel.asset");
        SetField(gameController, "_boardView", boardView);
        SetField(gameController, "_levelShape", levelAsset);

        SetField(inputController, "_targetCamera", camera);
        SetField(inputController, "_gameController", gameController);

        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scoreGO = new GameObject("ScoreText", typeof(Text), typeof(ScoreDisplay));
        scoreGO.transform.SetParent(canvasGO.transform, false);
        var scoreText = scoreGO.GetComponent<Text>();
        scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        scoreText.fontSize = 32;
        scoreText.text = "Score: 0";
        var scoreRect = scoreGO.GetComponent<RectTransform>();
        scoreRect.anchorMin = new Vector2(0f, 1f);
        scoreRect.anchorMax = new Vector2(0f, 1f);
        scoreRect.pivot = new Vector2(0f, 1f);
        scoreRect.anchoredPosition = new Vector2(20f, -20f);
        scoreRect.sizeDelta = new Vector2(300f, 50f);
        var scoreDisplay = scoreGO.GetComponent<ScoreDisplay>();
        SetField(scoreDisplay, "_scoreText", scoreText);
        SetField(scoreDisplay, "_gameController", gameController);

        CreateHudButton(canvasGO.transform, "HintButton", new Vector2(20f, 20f), gameController,
            typeof(HintButton), "_button", "Hint");
        CreateHudButton(canvasGO.transform, "UndoButton", new Vector2(140f, 20f), gameController,
            typeof(UndoButton), "_button", "Undo");
        CreateHudButton(canvasGO.transform, "ShuffleButton", new Vector2(260f, 20f), gameController,
            typeof(ShuffleButton), "_button", "Shuffle");

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Game.scene");

        Debug.Log("GAME_SCENE_BUILDER_DONE");
    }

    private static void CreateHudButton(
        Transform parent, string name, Vector2 anchoredPosition, GameController gameController,
        System.Type hudComponentType, string buttonFieldName, string label)
    {
        var buttonGO = new GameObject(name, typeof(Image), typeof(Button));
        buttonGO.transform.SetParent(parent, false);
        var rect = buttonGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(100f, 88f);

        var labelGO = new GameObject("Label", typeof(Text));
        labelGO.transform.SetParent(buttonGO.transform, false);
        var text = labelGO.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.text = label;
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var hudComponent = buttonGO.AddComponent(hudComponentType);
        var button = buttonGO.GetComponent<Button>();
        SetField(hudComponent, buttonFieldName, button);
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

Every field assignment goes through `SetField`, which throws immediately (with the exact target type and field name in the message) if `FindProperty` can't find it — a wrong `[SerializeField]` name in any of Tasks 5–9 surfaces here as a clear, specific exception rather than a silently half-wired scene.

- [ ] **Step 2: Run it headlessly**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient -executeMethod GameSceneBuilder.Build -quit -logFile /tmp/unity-task11-gen.log
echo "Exit code: $?"
grep -E "GAME_SCENE_BUILDER_DONE|GAME_SCENE_BUILDER_MISSING|error CS|Exception" /tmp/unity-task11-gen.log
ls unity/GameClient/Assets/Scenes/Game.scene
```

Expected: `GAME_SCENE_BUILDER_DONE` present, no `GAME_SCENE_BUILDER_MISSING*`/`error CS`/other `Exception`, `Game.scene` exists. If any `GAME_SCENE_BUILDER_MISSING*` line appears, it names the exact type and field that failed to wire — go fix that field name in the referenced task's file, do not work around it in the scene builder.

- [ ] **Step 3: Commit**

```bash
git add unity/
git commit -m "feat: assemble Game.scene wiring board, input, HUD, and GameController"
```

---

### Task 12: Android build, manual verification, and handoff

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Editor/AndroidBuilder.cs`
- Create (generated by Step 1): `unity/GameClient/Builds/GameClient.apk` (git-ignored by Task 1's `.gitignore`, not committed)
- Create: `unity/README.md`

**Interfaces:**
- Produces: an installable `.apk` and a documented manual-verification/handoff procedure. No further code interfaces — this is the sub-project's closing task.

- [ ] **Step 1: Write the headless Android build script**

```csharp
// unity/GameClient/Assets/Scripts/Editor/AndroidBuilder.cs
using UnityEditor;
using UnityEngine;

public static class AndroidBuilder
{
    public static void Build()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
        PlayerSettings.applicationIdentifier = "com.gameclient.mahjong";

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Game.scene" },
            locationPathName = "Builds/GameClient.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("ANDROID_BUILD_RESULT: " + report.summary.result);
        Debug.Log("ANDROID_BUILD_TOTAL_ERRORS: " + report.summary.totalErrors);
        Debug.Log("ANDROID_BUILD_OUTPUT_PATH: " + report.summary.outputPath);
    }
}
```

- [ ] **Step 2: Run the build**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient -executeMethod AndroidBuilder.Build -quit -logFile /tmp/unity-task12-build.log
echo "Exit code: $?"
grep -E "ANDROID_BUILD_RESULT|ANDROID_BUILD_TOTAL_ERRORS|ANDROID_BUILD_OUTPUT_PATH|error CS" /tmp/unity-task12-build.log
ls -la unity/GameClient/Builds/GameClient.apk
```

Expected: `ANDROID_BUILD_RESULT: Succeeded`, `ANDROID_BUILD_TOTAL_ERRORS: 0`, and the `.apk` file exists. This step can take several minutes (first Android build compiles IL2CPP and packages the SDK/NDK toolchain output) — do not treat a long-running build as a hang; wait for it to actually finish before checking the log.

If the result is not `Succeeded`: read the full log (not just the grepped lines) for the actual error — do not guess at Android/IL2CPP toolchain issues; report the specific error text if this step needs a fix beyond what this plan anticipated (e.g., a missing SDK component `unityhub --headless install-modules` didn't cover).

- [ ] **Step 3: Write the handoff README**

```markdown
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
`Assets/Scenes/Game.scene` and press Play to test in the Editor.

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
```

- [ ] **Step 4: Commit**

```bash
git add unity/README.md
git commit -m "docs: add Unity client README; confirm Android build succeeds"
```

---

## Done

At the end of Task 12, `unity/GameClient/` is a real Unity 6 project with the domain library ported and its tests green under Unity Test Framework, a playable board (tap-to-match, hint/undo/shuffle, score/combo HUD, accessibility-token-driven sizing/contrast), and a built `.apk` ready to sideload onto an Android phone via `adb install`. The one manual step left for the human is: install the `.apk` on their device and play it (Step 3 of the README) — that can't be automated from this machine.
