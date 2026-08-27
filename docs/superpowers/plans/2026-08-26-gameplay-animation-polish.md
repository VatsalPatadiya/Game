# Gameplay Interaction & Animation Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deal-in animation on level load, a fly-to-tray animation on tap, one shared card visual/animation system used by both the board and the tray (no more square boxes anywhere), a highlight-then-clear animation for tray matches with reflow, and a redesigned win/stuck end-of-level popup.

**Architecture:** Board tiles are `SpriteRenderer` (world space) and tray slots are `Image` (UI Canvas) — Unity has no single prefab type spanning both. "Shared visual card construction" is achieved two ways: (1) a `CardStyle` constants file used by both the Editor-time `Tile` prefab builder and a new Editor-time tray-slot prefab builder, so corner radius/layer sizes/colors can never drift between them, and (2) a runtime `CardAnimator` + `ITintable` adapter pair so the actual animation code (easing, timing, alpha math) is one implementation driving either a `SpriteRenderer` or an `Image`. The tap-to-tray flight and the tray reflow both reuse the *same* temporary flight-card mechanism (an `Instantiate()` of the tray slot prefab, moved between two screen positions), so that piece of infrastructure is built once and used twice. Win detection is added as a pure read (`board.Cells.Values.All(c => c.Cleared)`) in `GameController` — no `BoardState` changes.

**Tech Stack:** Unity 6000.5.9f1, C#, same headless Editor-script-driven workflow as the prior pass (`-batchmode -executeMethod X -quit`, `RegenerateAll.Run` for the full pipeline).

**Spec:** the "Gameplay Interaction & Animation Polish — Spec (Round 2)" provided in conversation. Items 6-9 are explicitly out of scope (left blank in the source request).

## Global Constraints

- Domain layer (`BoardState`, `UndoStack`, `ShuffleService`, `TrayManager`, `MatchValidator`, `HintFinder`, `FreedomRuleCalculator`, `ReverseConstructionSolver`) is not modified by any task in this plan. Every task's file list is Presentation (`Assets/Scripts/Presentation/**`) or Editor (`Assets/Scripts/Editor/**`) only.
- No fast/flashing/jarring motion. Durations: deal-in 200ms, tap fade-out 100ms, flight 280ms, tray highlight hold 130ms, clear 200ms, reflow 150ms — all ease-out, no overshoot/bounce.
- Any new interactive element keeps the existing 72-88dp minimum tap target and WCAG AA contrast (reuse the palette already established: card `#F7F4EB`, board green `#2A3D30`, dark text `#282E24`, accent colors from `DefaultTileSet.asset`).
- Prefer the shared `CardStyle`/`CardAnimator`/flight-card building blocks over parallel implementations — every task below that touches a card visual or a card animation routes through them rather than writing its own.

---

## Task 1: `CardStyle` — shared card geometry constants (Editor-time)

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Editor/CardStyle.cs`
- Modify: `unity/GameClient/Assets/Scripts/Editor/TilePrefabGenerator.cs`

**Interfaces:**
- Produces: `CardStyle.ShadowSizeRatio/GlowSizeRatio/AccentSizeRatio/CardSizeRatio/IconSizeRatio` (floats, fractions of a tile's full footprint), `CardStyle.ShadowColor/GlowColor/AccentDefaultColor/CardColor/EmptySlotColor` (Colors) — consumed by this task's `TilePrefabGenerator` and Task 6 (`GameSceneBuilder`'s tray slot builder).

- [ ] **Step 1: Create `CardStyle.cs`**

```csharp
using UnityEngine;

// Single source of truth for what a "tile card" looks like, shared by the
// world-space Tile prefab (TilePrefabGenerator, SpriteRenderer-based) and the
// UI tray slot prefab (GameSceneBuilder, Image-based) so the two can never
// visually drift apart. Ratios are fractions of the tile's full footprint
// (world units for the board tile, normalized 0-1 anchors for the tray slot).
public static class CardStyle
{
    public const float ShadowSizeRatio = 0.82f;
    public const float GlowSizeRatio = 1.0f;
    public const float AccentSizeRatio = 0.86f;
    public const float CardSizeRatio = 0.78f;
    public const float IconSizeRatio = 0.5f;

    public static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.3f);
    public static readonly Color GlowColor = new Color(1f, 0.85f, 0.2f, 0f);
    public static readonly Color AccentDefaultColor = new Color(0.69f, 0.26f, 0.16f, 1f);
    public static readonly Color CardColor = new Color(0.969f, 0.957f, 0.922f, 1f);
    public static readonly Color EmptySlotColor = new Color(1f, 1f, 1f, 0.12f);
}
```

- [ ] **Step 2: Point `TilePrefabGenerator` at the shared constants**

Edit `unity/GameClient/Assets/Scripts/Editor/TilePrefabGenerator.cs` in full:

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

        var shadow = CreateSlicedChild(root.transform, "Shadow",
            new Vector2(CardStyle.ShadowSizeRatio, CardStyle.ShadowSizeRatio), -2,
            new Vector3(0.05f, -0.05f, 0.06f), CardStyle.ShadowColor);

        var selectionGlow = CreateSlicedChild(root.transform, "SelectionGlow",
            new Vector2(CardStyle.GlowSizeRatio, CardStyle.GlowSizeRatio), -1,
            Vector3.zero, CardStyle.GlowColor);

        var accent = CreateSlicedChild(root.transform, "AccentBorder",
            new Vector2(CardStyle.AccentSizeRatio, CardStyle.AccentSizeRatio), 0,
            Vector3.zero, CardStyle.AccentDefaultColor);

        var card = CreateSlicedChild(root.transform, "Card",
            new Vector2(CardStyle.CardSizeRatio, CardStyle.CardSizeRatio), 1,
            Vector3.zero, CardStyle.CardColor);

        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(root.transform, false);
        iconGO.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        iconGO.transform.localScale = new Vector3(CardStyle.IconSizeRatio, CardStyle.IconSizeRatio, 1f);
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

- [ ] **Step 3: Regenerate the prefab and confirm it's byte-identical in structure**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -executeMethod TilePrefabGenerator.Generate -logFile /tmp/round2-task1.log -quit
grep -E "TILE_PREFAB_GENERATOR_DONE|error CS" /tmp/round2-task1.log
```
Expected: `TILE_PREFAB_GENERATOR_DONE`, no `error CS`. This is a pure refactor (literals replaced with named constants of the same values) — `git diff unity/GameClient/Assets/Prefabs/Tile.prefab` should show no meaningful diff beyond possible GUID churn from regeneration.

- [ ] **Step 4: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Editor/CardStyle.cs \
  unity/GameClient/Assets/Scripts/Editor/TilePrefabGenerator.cs \
  unity/GameClient/Assets/Prefabs/Tile.prefab
git commit -m "refactor: extract CardStyle as the shared source of truth for tile card geometry"
```

---

## Task 2: `ITintable` + `CardAnimator` — shared runtime animation infrastructure

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Presentation/Board/ITintable.cs`
- Create: `unity/GameClient/Assets/Scripts/Presentation/Board/CardAnimator.cs`

**Interfaces:**
- Produces: `ITintable` (interface with `Color Color { get; set; }`), `SpriteRendererTint`, `ImageTint` — consumed by Task 3 (`TileView`) and Task 4 (`TraySlotView`).
- Produces: `CardAnimator.ScaleAndFadeIn`, `CardAnimator.ScaleUpAndFadeOut`, `CardAnimator.HighlightThenClear`, `CardAnimator.FadeAlpha`, `CardAnimator.MoveRectTransform`, `CardAnimator.EaseOut`, and duration constants `ClearDuration/DealInDuration/FlightDuration/FastFadeDuration/HighlightHoldDuration` — consumed by Task 3 (`TileView`), Task 4 (`TraySlotView`), Task 5 (`BoardView` deal-in), Task 7 (`TrayView` reflow), and Task 9 (`GameController` tap-flight).

- [ ] **Step 1: Create the tint adapters**

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.Board
{
    // Lets CardAnimator's coroutines drive either a SpriteRenderer (board
    // tiles) or an Image (tray/flight cards) through the same alpha/color
    // math, so the animation code itself only needs to exist once.
    public interface ITintable
    {
        Color Color { get; set; }
    }

    public sealed class SpriteRendererTint : ITintable
    {
        private readonly SpriteRenderer _renderer;
        public SpriteRendererTint(SpriteRenderer renderer) { _renderer = renderer; }
        public Color Color { get => _renderer.color; set => _renderer.color = value; }
    }

    public sealed class ImageTint : ITintable
    {
        private readonly Image _image;
        public ImageTint(Image image) { _image = image; }
        public Color Color { get => _image.color; set => _image.color = value; }
    }
}
```

- [ ] **Step 2: Create `CardAnimator`**

```csharp
using System;
using System.Collections;
using UnityEngine;

namespace GameClient.Presentation.Board
{
    // Shared timing/easing for every card animation in the game. Board tiles
    // and tray slots both drive these coroutines through ITintable so a
    // clear, a highlight, a deal-in, or a flight can never feel different
    // between the board and the tray.
    public static class CardAnimator
    {
        public const float ClearDuration = 0.2f;
        public const float DealInDuration = 0.2f;
        public const float FlightDuration = 0.28f;
        public const float FastFadeDuration = 0.1f;
        public const float HighlightHoldDuration = 0.13f;

        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        public static IEnumerator ScaleAndFadeIn(
            Transform target, ITintable[] renderers, Color[] targetColors, float delay, float duration)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            target.localScale = Vector3.zero;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                var c = targetColors[i];
                c.a = 0f;
                renderers[i].Color = c;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOut(Mathf.Clamp01(elapsed / duration));
                target.localScale = Vector3.one * t;
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    var c = targetColors[i];
                    c.a = targetColors[i].a * t;
                    renderers[i].Color = c;
                }
                yield return null;
            }

            target.localScale = Vector3.one;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].Color = targetColors[i];
            }
        }

        public static IEnumerator ScaleUpAndFadeOut(Transform target, ITintable[] renderers, Action onComplete)
        {
            float elapsed = 0f;
            var startScale = target.localScale;
            var endScale = startScale * 1.15f;
            var startAlphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                startAlphas[i] = renderers[i] != null ? renderers[i].Color.a : 0f;

            while (elapsed < ClearDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / ClearDuration);
                target.localScale = Vector3.Lerp(startScale, endScale, t);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    var c = renderers[i].Color;
                    c.a = startAlphas[i] * (1f - t);
                    renderers[i].Color = c;
                }
                yield return null;
            }

            onComplete?.Invoke();
        }

        public static IEnumerator HighlightThenClear(
            ITintable glow, Color glowColor, Transform target, ITintable[] renderers, Action onComplete)
        {
            if (glow != null)
            {
                var c = glowColor;
                c.a = 1f;
                glow.Color = c;
            }

            yield return new WaitForSeconds(HighlightHoldDuration);

            yield return ScaleUpAndFadeOut(target, renderers, onComplete);
        }

        public static IEnumerator FadeAlpha(ITintable renderer, float fromAlpha, float toAlpha, float duration)
        {
            if (renderer == null) yield break;
            var c = renderer.Color;
            c.a = fromAlpha;
            renderer.Color = c;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                c.a = Mathf.Lerp(fromAlpha, toAlpha, t);
                renderer.Color = c;
                yield return null;
            }

            c.a = toAlpha;
            renderer.Color = c;
        }

        public static IEnumerator MoveRectTransform(
            RectTransform rect, Vector3 fromScreenPos, Vector3 toScreenPos, float duration)
        {
            rect.position = fromScreenPos;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOut(Mathf.Clamp01(elapsed / duration));
                rect.position = Vector3.Lerp(fromScreenPos, toScreenPos, t);
                yield return null;
            }
            rect.position = toScreenPos;
        }
    }
}
```

- [ ] **Step 2: Compile-check**

These two files have no callers yet (Task 3 wires the first one). Verify they compile standalone:

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -executeMethod UnityEditor.SyncVS.SyncSolution -logFile /tmp/round2-task2.log -quit
grep -iE "error CS" /tmp/round2-task2.log
```
Expected: no `error CS` lines.

- [ ] **Step 3: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Presentation/Board/ITintable.cs \
  unity/GameClient/Assets/Scripts/Presentation/Board/CardAnimator.cs
git commit -m "feat: add shared ITintable/CardAnimator runtime animation infrastructure"
```

---

## Task 3: `TileView` — route through `CardAnimator`, add deal-in and tap-fade

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/Board/TileView.cs`

**Interfaces:**
- Consumes: `CardAnimator.*`, `ITintable`, `SpriteRendererTint` (Task 2).
- Produces: `TileView.PlayDealIn(float delaySeconds, Action onComplete)` — consumed by Task 5 (`BoardView`). `TileView.PlayFadeOutOnly(Action onComplete)` / `PlayFadeInOnly()` — consumed by Task 9 (`GameController`). `Initialize`/`SetFree`/`Highlight`/`PlayClearAndDestroy`/`PlayShake` signatures unchanged.

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
        private Coroutine _fadeCoroutine;

        public string SlotId { get; private set; }
        public int Layer { get; private set; }

        public void Initialize(string slotId, int layer, Sprite icon, Color accentColor)
        {
            SlotId = slotId;
            Layer = layer;

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

        public void SetFree(bool isFree) => RefreshCardColor(isFree, Layer);

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

        // Call after Initialize(). Scales/fades the tile in from nothing up to
        // whatever colors Initialize() already set, starting after delaySeconds.
        public void PlayDealIn(float delaySeconds, System.Action onComplete)
        {
            var renderers = BuildRendererArray();
            var targetColors = new[]
            {
                _shadowRenderer != null ? _shadowRenderer.color : default,
                _selectionGlowRenderer != null ? _selectionGlowRenderer.color : default,
                _accentRenderer != null ? _accentRenderer.color : default,
                _cardRenderer != null ? _cardRenderer.color : default,
                _iconRenderer != null ? _iconRenderer.color : default,
            };
            StartCoroutine(DealInRoutine(renderers, targetColors, delaySeconds, onComplete));
        }

        private IEnumerator DealInRoutine(
            ITintable[] renderers, Color[] targetColors, float delay, System.Action onComplete)
        {
            yield return CardAnimator.ScaleAndFadeIn(transform, renderers, targetColors, delay, CardAnimator.DealInDuration);
            onComplete?.Invoke();
        }

        // Fast (~100ms) fade of the visible layers only (not the glow), used
        // when a valid tap sends this tile flying off toward the tray.
        public void PlayFadeOutOnly(System.Action onComplete)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeVisibleRenderers(0f, CardAnimator.FastFadeDuration, onComplete));
        }

        // Restores full visibility; only used defensively if a tap's flight
        // animation completes but the domain call turns out to be invalid.
        public void PlayFadeInOnly()
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeVisibleRenderers(1f, CardAnimator.FastFadeDuration, null));
        }

        private IEnumerator FadeVisibleRenderers(float toAlpha, float duration, System.Action onComplete)
        {
            var renderers = new[] { _shadowRenderer, _accentRenderer, _cardRenderer, _iconRenderer };
            var tints = new ITintable[renderers.Length];
            var fromAlphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                tints[i] = new SpriteRendererTint(renderers[i]);
                fromAlphas[i] = renderers[i].color.a;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < tints.Length; i++)
                {
                    if (tints[i] == null) continue;
                    var c = tints[i].Color;
                    c.a = Mathf.Lerp(fromAlphas[i], toAlpha, t);
                    tints[i].Color = c;
                }
                yield return null;
            }

            for (int i = 0; i < tints.Length; i++)
            {
                if (tints[i] == null) continue;
                var c = tints[i].Color;
                c.a = toAlpha;
                tints[i].Color = c;
            }

            onComplete?.Invoke();
        }

        public void PlayClearAndDestroy()
        {
            if (_clearCoroutine != null) StopCoroutine(_clearCoroutine);
            _clearCoroutine = StartCoroutine(
                CardAnimator.ScaleUpAndFadeOut(transform, BuildRendererArray(), () => Destroy(gameObject)));
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

        private ITintable[] BuildRendererArray()
        {
            return new ITintable[]
            {
                _shadowRenderer != null ? new SpriteRendererTint(_shadowRenderer) : null,
                _selectionGlowRenderer != null ? new SpriteRendererTint(_selectionGlowRenderer) : null,
                _accentRenderer != null ? new SpriteRendererTint(_accentRenderer) : null,
                _cardRenderer != null ? new SpriteRendererTint(_cardRenderer) : null,
                _iconRenderer != null ? new SpriteRendererTint(_iconRenderer) : null,
            };
        }
    }
}
```

- [ ] **Step 2: Compile-check**

`BoardView.cs` still calls the pre-Round-2 `TileView` API surface (`Initialize`, `SetFree`, `Highlight`, `PlayShake`) which is unchanged here, so this compiles standalone:

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -executeMethod UnityEditor.SyncVS.SyncSolution -logFile /tmp/round2-task3.log -quit
grep -iE "error CS" /tmp/round2-task3.log
```
Expected: no `error CS` lines.

- [ ] **Step 3: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Presentation/Board/TileView.cs
git commit -m "refactor: route TileView animations through CardAnimator, add deal-in and tap fade"
```

---

## Task 4: `TraySlotView` — the tray's half of the shared card

**Files:**
- Create: `unity/GameClient/Assets/Scripts/Presentation/HUD/TraySlotView.cs`

**Interfaces:**
- Consumes: `CardAnimator.HighlightThenClear`, `ITintable`, `ImageTint` (Task 2).
- Produces: `TraySlotView.SetEmpty()`, `SetFilled(Sprite icon, Color accentColor)`, `PlayHighlightThenClear(Action onComplete)`, `RectTransform` (property) — consumed by Task 6 (`GameSceneBuilder`'s tray slot prefab, which attaches this component and wires its fields) and Task 6's rewrite of `TrayView`.

Note: this file lives in `Presentation/HUD` (not `Presentation/Board`) but references `GameClient.Presentation.Board` for `ITintable`/`ImageTint`/`CardAnimator` — those are runtime types in the main assembly, not Editor-only, so this cross-namespace reference is a normal same-assembly reference, not an assembly-boundary problem. Its color defaults intentionally duplicate `TileView`'s literals rather than referencing `CardStyle` (Task 1), because `CardStyle` lives in `Assets/Scripts/Editor/` and is Editor-only — a runtime component cannot reference it. `TileView` already establishes this same "runtime `[SerializeField]` default duplicates the Editor-time constant" pattern for its own highlight color.

- [ ] **Step 1: Create `TraySlotView.cs`**

```csharp
using GameClient.Presentation.Board;
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class TraySlotView : MonoBehaviour
    {
        [SerializeField] private Image _shadowImage;
        [SerializeField] private Image _glowImage;
        [SerializeField] private Image _accentImage;
        [SerializeField] private Image _cardImage;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Color _emptyAccentColor = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] private Color _filledCardColor = new Color(0.969f, 0.957f, 0.922f, 1f);
        [SerializeField] private Color _highlightColor = new Color(1f, 0.85f, 0.2f, 1f);

        private Coroutine _clearCoroutine;

        public RectTransform RectTransform => (RectTransform)transform;

        public void SetEmpty()
        {
            if (_accentImage != null) _accentImage.color = _emptyAccentColor;
            if (_cardImage != null) _cardImage.color = new Color(0f, 0f, 0f, 0f);
            if (_shadowImage != null) _shadowImage.color = new Color(0f, 0f, 0f, 0f);
            if (_glowImage != null) { var c = _highlightColor; c.a = 0f; _glowImage.color = c; }
            if (_iconImage != null) _iconImage.enabled = false;
        }

        public void SetFilled(Sprite icon, Color accentColor)
        {
            accentColor.a = 1f;
            if (_accentImage != null) _accentImage.color = accentColor;
            if (_cardImage != null) _cardImage.color = _filledCardColor;
            if (_shadowImage != null) _shadowImage.color = new Color(0f, 0f, 0f, 0.3f);
            if (_glowImage != null) { var c = _highlightColor; c.a = 0f; _glowImage.color = c; }

            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.color = accentColor;
                _iconImage.enabled = true;
            }
        }

        // Brief highlight so the player can see which two tiles matched, then
        // the same scale-up+fade used everywhere else, then resets to the
        // empty state — this slot is reused, never destroyed.
        public void PlayHighlightThenClear(System.Action onComplete)
        {
            if (_clearCoroutine != null) StopCoroutine(_clearCoroutine);
            var glow = _glowImage != null ? new ImageTint(_glowImage) : null;
            _clearCoroutine = StartCoroutine(CardAnimator.HighlightThenClear(
                glow, _highlightColor, transform, BuildRendererArray(),
                () =>
                {
                    transform.localScale = Vector3.one;
                    SetEmpty();
                    onComplete?.Invoke();
                }));
        }

        private ITintable[] BuildRendererArray()
        {
            return new ITintable[]
            {
                _shadowImage != null ? new ImageTint(_shadowImage) : null,
                _glowImage != null ? new ImageTint(_glowImage) : null,
                _accentImage != null ? new ImageTint(_accentImage) : null,
                _cardImage != null ? new ImageTint(_cardImage) : null,
                _iconImage != null ? new ImageTint(_iconImage) : null,
            };
        }
    }
}
```

- [ ] **Step 2: Compile-check**

Nothing instantiates this component yet (Task 6 does). Verify it compiles standalone:

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -executeMethod UnityEditor.SyncVS.SyncSolution -logFile /tmp/round2-task4.log -quit
grep -iE "error CS" /tmp/round2-task4.log
```
Expected: no `error CS` lines.

- [ ] **Step 3: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Presentation/HUD/TraySlotView.cs
git commit -m "feat: add TraySlotView, the tray's half of the shared card visual"
```

---

## Task 5: `BoardView` — ordered, staggered deal-in

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/Board/BoardView.cs`

**Interfaces:**
- Consumes: `TileView.PlayDealIn(float, Action)` (Task 3).
- Produces: `BoardView.Build(BoardState, Dictionary<string,TileSlot>, bool animateDealIn, Action onDealInComplete = null)` (signature changes — was `Build(BoardState, Dictionary<string,TileSlot>)`; consumed by Task 9's `GameController`, which updates all 3 existing call sites). `BoardView.RemoveTileInstant(string slotId)` — consumed by Task 9. `BoardView.TileSet` (property) — consumed by Task 9.

- [ ] **Step 1: Rewrite `BoardView.cs`**

```csharp
using System;
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
        private const float TargetDealInSeconds = 1.0f;
        private const float MinStaggerSeconds = 0.008f;
        private const float MaxStaggerSeconds = 0.03f;

        [SerializeField] private TileView _tilePrefab;
        [SerializeField] private TileSetAsset _tileSet;
        [SerializeField] private Camera _camera;
        [SerializeField] private float _cellSize = 0.95f;
        [SerializeField] private float _cameraMargin = 0.3f;

        private readonly Dictionary<string, TileView> _tileViews = new Dictionary<string, TileView>();
        private Dictionary<string, TileSlot> _slotsById;

        public TileSetAsset TileSet => _tileSet;

        // animateDealIn should only be true for a fresh level load. Undo and
        // Shuffle rebuild the whole board too, but replaying the deal-in
        // flourish on every one of those would be repetitive rather than
        // polished, so they pass false and tiles simply appear at full
        // opacity as before.
        public void Build(
            BoardState board, Dictionary<string, TileSlot> slotsById, bool animateDealIn, Action onDealInComplete = null)
        {
            _slotsById = slotsById;

            foreach (var view in _tileViews.Values)
                if (view != null) Destroy(view.gameObject);
            _tileViews.Clear();

            FitCameraToBoard(slotsById);

            // Layer ascending, then top-to-bottom, then left-to-right within a
            // layer, so the deal-in reads as the board building up from the
            // bottom layer to the top, matching the shadow-depth-per-layer cue.
            var orderedCells = board.Cells
                .Where(kv => !kv.Value.Cleared)
                .OrderBy(kv => slotsById[kv.Key].Layer)
                .ThenBy(kv => -slotsById[kv.Key].Y)
                .ThenBy(kv => slotsById[kv.Key].X)
                .ToList();

            int tileCount = orderedCells.Count;
            float stagger = tileCount > 0
                ? Mathf.Clamp(TargetDealInSeconds / tileCount, MinStaggerSeconds, MaxStaggerSeconds)
                : 0f;
            int pendingDealIns = animateDealIn ? tileCount : 0;

            for (int i = 0; i < orderedCells.Count; i++)
            {
                var kv = orderedCells[i];
                var slot = slotsById[kv.Key];
                var view = Instantiate(_tilePrefab, transform);
                view.transform.localPosition = new Vector3(
                    slot.X * _cellSize,
                    slot.Y * _cellSize,
                    -slot.Layer * 0.1f);
                view.Initialize(slot.Id, slot.Layer, TileVisual.IconFor(_tileSet, kv.Value.Value), TileVisual.AccentColorFor(_tileSet, kv.Value.Value));
                _tileViews[kv.Key] = view;

                if (animateDealIn)
                {
                    float delay = i * stagger;
                    view.PlayDealIn(delay, () =>
                    {
                        pendingDealIns--;
                        if (pendingDealIns == 0)
                            onDealInComplete?.Invoke();
                    });
                }
            }

            if (animateDealIn && tileCount == 0)
                onDealInComplete?.Invoke();

            RefreshFreeStates(board);
        }

        // Orthographic size only controls vertical half-height; the visible width
        // depends on the device's actual aspect ratio. A fixed size tuned for one
        // aspect ratio crops the board on narrower phones, so this recomputes the
        // size (and re-centers) every time the board is built, from the board's
        // real bounds and Screen.width/Screen.height, taking whichever axis is the
        // tighter constraint (letterboxing the other) instead of guessing a value.
        private void FitCameraToBoard(Dictionary<string, TileSlot> slotsById)
        {
            if (_camera == null || slotsById.Count == 0) return;

            float minX = slotsById.Values.Min(s => s.X);
            float maxX = slotsById.Values.Max(s => s.X);
            float minY = slotsById.Values.Min(s => s.Y);
            float maxY = slotsById.Values.Max(s => s.Y);

            float boardWidth = (maxX - minX) * _cellSize + _cellSize;
            float boardHeight = (maxY - minY) * _cellSize + _cellSize;

            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 0.5f;
            float sizeForWidth = (boardWidth / 2f + _cameraMargin) / aspect;
            float sizeForHeight = boardHeight / 2f + _cameraMargin;
            _camera.orthographicSize = Mathf.Max(sizeForWidth, sizeForHeight);

            float centerX = (minX + maxX) / 2f * _cellSize;
            float centerY = (minY + maxY) / 2f * _cellSize;
            var pos = _camera.transform.position;
            _camera.transform.position = new Vector3(centerX, centerY, pos.z);
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

        // Used after a tile has already been faded out by the tap-to-tray
        // flight (see GameController) — no further animation needed here.
        public void RemoveTileInstant(string slotId)
        {
            if (!_tileViews.TryGetValue(slotId, out var view)) return;
            _tileViews.Remove(slotId);
            if (view != null) Destroy(view.gameObject);
        }

        public TileView GetTileView(string slotId) =>
            _tileViews.TryGetValue(slotId, out var view) ? view : null;
    }
}
```

- [ ] **Step 2: Compile-check**

`GameController.cs` still calls the old two-argument `Build(board, slotsById)` at its three call sites — that will now fail to compile until Task 9 updates them. This is expected and resolved in Task 9; do not run a full-project compile check here. Stage and commit this file now.

- [ ] **Step 3: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Presentation/Board/BoardView.cs
git commit -m "feat: add ordered, staggered deal-in animation and RemoveTileInstant to BoardView"
```

---

## Task 6: `GameSceneBuilder` — rebuild the tray slot prefab from `CardStyle`

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder.cs`

**Interfaces:**
- Consumes: `CardStyle.*` (Task 1), `TraySlotView` (Task 4).
- Produces: `Assets/Scenes/Game.unity`'s `traySlotPrefab` GameObject now has a `TraySlotView` component with its 5 `Image` fields wired, and 5 children named `Shadow`, `SelectionGlow`, `AccentBorder`, `Card`, `Icon` — consumed by Task 7 (`TrayView`, which now finds/wires `TraySlotView` instead of doing its own `Find()` calls).

The current `traySlotPrefab` construction (a flat `Image`/`Text` hierarchy, unrelated to `TilePrefabGenerator`'s card layering) is exactly the "two different visual recipes" problem the spec calls out. This task replaces it with a 5-layer stack whose *ratios* come from the same `CardStyle` the `Tile` prefab uses, translated from world-unit sizes to normalized `RectTransform` anchors (`inset = (1 - ratio) / 2`, so a layer spans `[inset, 1-inset]` of the slot).

- [ ] **Step 1: Replace the tray slot prefab construction block**

In `unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder.cs`, find the block that currently builds `traySlotPrefab` (from `var traySlotPrefab = new GameObject("TraySlot", ...)` through the `SetField(trayView, "tileSet", tileSet);` line) and replace it with:

```csharp
        var trayView = trayGO.GetComponent<TrayView>();
        var traySlotPrefab = new GameObject("TraySlot", typeof(RectTransform));
        var slotRect = traySlotPrefab.GetComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(80f, 80f);

        var shadowLayer = CreateCardLayer(traySlotPrefab.transform, "Shadow", CardStyle.ShadowSizeRatio, CardStyle.ShadowColor);
        shadowLayer.rectTransform.anchoredPosition = new Vector2(4f, -4f);

        var glowLayer = CreateCardLayer(traySlotPrefab.transform, "SelectionGlow", CardStyle.GlowSizeRatio, CardStyle.GlowColor);
        var accentLayer = CreateCardLayer(traySlotPrefab.transform, "AccentBorder", CardStyle.AccentSizeRatio, CardStyle.AccentDefaultColor);
        var cardLayer = CreateCardLayer(traySlotPrefab.transform, "Card", CardStyle.CardSizeRatio, CardStyle.CardColor);

        var slotIconGO = new GameObject("Icon", typeof(Image));
        slotIconGO.transform.SetParent(traySlotPrefab.transform, false);
        var slotIconImage = slotIconGO.GetComponent<Image>();
        slotIconImage.type = Image.Type.Simple;
        var iconInset = (1f - CardStyle.IconSizeRatio) / 2f;
        var slotIconRect = slotIconGO.GetComponent<RectTransform>();
        slotIconRect.anchorMin = new Vector2(iconInset, iconInset);
        slotIconRect.anchorMax = new Vector2(1f - iconInset, 1f - iconInset);
        slotIconRect.offsetMin = Vector2.zero;
        slotIconRect.offsetMax = Vector2.zero;

        var traySlotView = traySlotPrefab.AddComponent<TraySlotView>();
        SetField(traySlotView, "_shadowImage", shadowLayer);
        SetField(traySlotView, "_glowImage", glowLayer);
        SetField(traySlotView, "_accentImage", accentLayer);
        SetField(traySlotView, "_cardImage", cardLayer);
        SetField(traySlotView, "_iconImage", slotIconImage);

        SetField(trayView, "layoutGroup", trayLayout);
        SetField(trayView, "traySlotPrefab", traySlotPrefab);
        SetField(trayView, "tileSet", tileSet);
```

- [ ] **Step 2: Add the `CreateCardLayer` helper**

Add this method near `CreateHudButton` (both are small builder helpers):

```csharp
    private static Image CreateCardLayer(Transform parent, string name, float sizeRatio, Color color)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = color;
        var inset = (1f - sizeRatio) / 2f;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(inset, inset);
        rect.anchorMax = new Vector2(1f - inset, 1f - inset);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }
```

- [ ] **Step 3: Add the `using` for `TraySlotView` and `CardStyle`**

`TraySlotView` is in `GameClient.Presentation.HUD`, already covered by the existing `using GameClient.Presentation.HUD;` at the top of the file. `CardStyle` is global-namespace (matching every other Editor generator script), so no new `using` is needed for it either — confirm both are already covered before moving on.

- [ ] **Step 4: Do not run a batch verification yet**

`GameController.cs` still calls `BoardView.Build(board, slotsById)` with the pre-Task-5 two-argument signature, which no longer compiles since Task 5 changed that signature to three arguments — the whole project won't compile again until Task 9 fixes `GameController.cs`. So `-executeMethod GameSceneBuilder.Build` would fail here on that unrelated stale reference, not on anything this task changed. Stage and commit without running Unity; Task 9 is where `RegenerateAll.Run` and the full EditMode suite verify everything together, including this task's prefab structure.

- [ ] **Step 5: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder.cs unity/GameClient/Assets/Scenes/Game.unity
git commit -m "feat: rebuild tray slot prefab from CardStyle, matching the board tile card exactly"
```

---

## Task 7: `TrayView` — match detection, highlight-then-clear, and reflow

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/HUD/TrayView.cs`

**Interfaces:**
- Consumes: `TraySlotView.SetEmpty/SetFilled/PlayHighlightThenClear/RectTransform` (Task 4), `CardAnimator.MoveRectTransform` (Task 2), `TileVisual.IconFor/AccentColorFor` (existing).
- Produces: `TrayView.GetSlotScreenPosition(int) : Vector3`, `PlayArrival(int, Sprite, Color)`, `SpawnFlightCard(Sprite, Color, Vector3) : GameObject`, `ResolveAfterPush(List<string> oldTrayIds, string newTileId, List<string> newTrayIds, BoardState board) : IEnumerator` — all consumed by Task 9 (`GameController`'s tap-to-tray coroutine).

`UpdateTray`/`SetSlotFilled`/`SetSlotEmpty` from the prior round are removed — nothing calls them after this task (the tap flow now drives the tray through the arrival/resolve API below instead of one whole-tray refresh call), and leaving them around would just be dead code.

- [ ] **Step 1: Rewrite `TrayView.cs`**

```csharp
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameClient.Data;
using GameClient.Presentation.Board;
using GameDomain.Model;
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public class TrayView : MonoBehaviour
    {
        private const float ReflowDuration = 0.15f;

        public HorizontalLayoutGroup layoutGroup;
        public GameObject traySlotPrefab;
        public TileSetAsset tileSet;

        private List<TraySlotView> _slots = new List<TraySlotView>();

        public int SlotCount => _slots.Count;

        public void Initialize(int maxTraySize)
        {
            foreach (var slot in _slots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            _slots.Clear();

            for (int i = 0; i < maxTraySize; i++)
            {
                var slotGO = Instantiate(traySlotPrefab, layoutGroup.transform);
                var slotView = slotGO.GetComponent<TraySlotView>();
                _slots.Add(slotView);
                slotView.SetEmpty();
            }
        }

        public Vector3 GetSlotScreenPosition(int index) => _slots[index].RectTransform.position;

        public void PlayArrival(int index, Sprite icon, Color accentColor)
        {
            if (index < 0 || index >= _slots.Count) return;
            _slots[index].SetFilled(icon, accentColor);
        }

        // Spawns a free-standing copy of the tray card (parented under the
        // Canvas root, not the layout group, so it can be positioned/animated
        // independently) — shared by GameController's tap-to-tray flight and
        // this class's own reflow.
        public GameObject SpawnFlightCard(Sprite icon, Color accentColor, Vector3 startScreenPosition)
        {
            var flightCard = Instantiate(traySlotPrefab, transform.root, false);
            var flightSlotView = flightCard.GetComponent<TraySlotView>();
            flightSlotView.SetFilled(icon, accentColor);
            var rect = (RectTransform)flightCard.transform;
            rect.position = startScreenPosition;
            return flightCard;
        }

        // Compares the tray immediately before this push (plus the tile that
        // just landed) against the tray after TrayManager's match-check to
        // find which two slots to highlight+clear, then reflows whatever
        // remains into its new slot positions. Slot *indices* are what
        // matters here, since the tray's persistent slots are fixed by
        // index, not by tile identity.
        public IEnumerator ResolveAfterPush(
            List<string> oldTrayIds, string newTileId, List<string> newTrayIds, BoardState board)
        {
            var beforePush = new List<string>(oldTrayIds) { newTileId };

            if (newTrayIds.Count == beforePush.Count)
                yield break; // landed, no match — nothing further to animate

            var matchedIds = beforePush.Except(newTrayIds).ToList();
            int firstIndex = beforePush.IndexOf(matchedIds[0]);
            int secondIndex = beforePush.IndexOf(matchedIds[1]);

            bool clearedFirst = false, clearedSecond = false;
            _slots[firstIndex].PlayHighlightThenClear(() => clearedFirst = true);
            _slots[secondIndex].PlayHighlightThenClear(() => clearedSecond = true);

            yield return new WaitUntil(() => clearedFirst && clearedSecond);

            var reflowRoutines = new List<Coroutine>();
            for (int newIndex = 0; newIndex < newTrayIds.Count; newIndex++)
            {
                string id = newTrayIds[newIndex];
                int oldIndex = beforePush.IndexOf(id);
                if (oldIndex == newIndex) continue;

                reflowRoutines.Add(StartCoroutine(ReflowSlot(oldIndex, newIndex, board.Cells[id].Value)));
            }

            foreach (var routine in reflowRoutines)
                yield return routine;

            for (int i = newTrayIds.Count; i < _slots.Count; i++)
                _slots[i].SetEmpty();
        }

        private IEnumerator ReflowSlot(int fromIndex, int toIndex, string value)
        {
            var icon = TileVisual.IconFor(tileSet, value);
            var accent = TileVisual.AccentColorFor(tileSet, value);
            var fromPos = _slots[fromIndex].RectTransform.position;
            var toPos = _slots[toIndex].RectTransform.position;

            _slots[fromIndex].SetEmpty();

            var flightCard = SpawnFlightCard(icon, accent, fromPos);
            var rect = (RectTransform)flightCard.transform;
            yield return CardAnimator.MoveRectTransform(rect, fromPos, toPos, ReflowDuration);
            Destroy(flightCard);

            _slots[toIndex].SetFilled(icon, accent);
        }
    }
}
```

- [ ] **Step 2: Compile-check**

`GameController.cs` doesn't call any of `TrayView`'s new methods yet (Task 9 wires that up) and no longer references the removed `UpdateTray` after this task lands — but it *currently* still calls `_trayView.UpdateTray(...)`, so this file alone will leave the project non-compiling until Task 9. Stage and commit now without running a compile check; Task 9 verifies the whole chain compiles.

- [ ] **Step 3: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Presentation/HUD/TrayView.cs
git commit -m "feat: TrayView match-detection, highlight-then-clear, and reflow via shared flight cards"
```

---

## Task 8: End-of-level popup redesign — win vs. stuck, card visuals, styled Restart

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/HUD/GameOverPopup.cs`
- Modify: `unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder.cs`

**Interfaces:**
- Consumes: `CardStyle.*` and `GameSceneBuilder.CreateCardLayer` (Task 1 and Task 6 — the popup card reuses the exact same shadow+accent+card layering as tiles, per spec §5's explicit requirement).
- Produces: `GameOverPopup.ShowWin(GameController, int score)`, `GameOverPopup.ShowStuck(GameController)` — replacing the old single `Show(GameController)` — consumed by Task 9 (`GameController`'s win/stuck detection).

- [ ] **Step 1: Rewrite `GameOverPopup.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation
{
    public class GameOverPopup : MonoBehaviour
    {
        public Button restartButton;
        public Text titleText;
        public Text messageText;

        private GameController _gameController;

        private void Start()
        {
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(() =>
                {
                    if (_gameController != null)
                        _gameController.RestartLevel();
                });
            }
        }

        public void ShowWin(GameController controller, int score)
        {
            _gameController = controller;
            if (titleText != null) titleText.text = "Well done!";
            if (messageText != null) messageText.text = "The board is clear! Final score: " + score;
            gameObject.SetActive(true);
        }

        public void ShowStuck(GameController controller)
        {
            _gameController = controller;
            if (titleText != null) titleText.text = "No matches left";
            if (messageText != null) messageText.text = "Try shuffling, or start a fresh board.";
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
```

- [ ] **Step 2: Replace the "Build Game Over UI" block in `GameSceneBuilder.cs`**

Find the block from `var gameOverGO = new GameObject("GameOverPanel", ...)` through `gameOverGO.SetActive(false); // Hidden by default` and replace it with:

```csharp
        // ------------------
        // Build Game Over UI
        // ------------------
        var gameOverGO = new GameObject("GameOverPanel", typeof(Image), typeof(GameOverPopup));
        gameOverGO.transform.SetParent(canvasGO.transform, false);
        var goScrimImage = gameOverGO.GetComponent<Image>();
        goScrimImage.color = new Color(0.05f, 0.08f, 0.06f, 0.55f);
        var goRect = gameOverGO.GetComponent<RectTransform>();
        goRect.anchorMin = Vector2.zero;
        goRect.anchorMax = Vector2.one;
        goRect.offsetMin = Vector2.zero;
        goRect.offsetMax = Vector2.zero;

        var popupCardGO = new GameObject("PopupCard", typeof(RectTransform));
        popupCardGO.transform.SetParent(gameOverGO.transform, false);
        var popupCardRect = popupCardGO.GetComponent<RectTransform>();
        popupCardRect.anchorMin = new Vector2(0.5f, 0.5f);
        popupCardRect.anchorMax = new Vector2(0.5f, 0.5f);
        popupCardRect.pivot = new Vector2(0.5f, 0.5f);
        popupCardRect.anchoredPosition = Vector2.zero;
        popupCardRect.sizeDelta = new Vector2(320f, 280f);

        CreateCardLayer(popupCardGO.transform, "Shadow", CardStyle.ShadowSizeRatio, CardStyle.ShadowColor)
            .rectTransform.anchoredPosition = new Vector2(6f, -6f);
        CreateCardLayer(popupCardGO.transform, "AccentBorder", CardStyle.AccentSizeRatio, CardStyle.AccentDefaultColor);
        CreateCardLayer(popupCardGO.transform, "Card", CardStyle.CardSizeRatio, CardStyle.CardColor);

        var goTitleGO = new GameObject("Title", typeof(Text));
        goTitleGO.transform.SetParent(popupCardGO.transform, false);
        var goTitleText = goTitleGO.GetComponent<Text>();
        goTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        goTitleText.fontSize = 30;
        goTitleText.fontStyle = FontStyle.Bold;
        goTitleText.color = DarkHudText;
        goTitleText.alignment = TextAnchor.MiddleCenter;
        var goTitleRect = goTitleGO.GetComponent<RectTransform>();
        goTitleRect.anchorMin = new Vector2(0.1f, 0.62f);
        goTitleRect.anchorMax = new Vector2(0.9f, 0.85f);
        goTitleRect.offsetMin = Vector2.zero;
        goTitleRect.offsetMax = Vector2.zero;

        var goMessageGO = new GameObject("Message", typeof(Text));
        goMessageGO.transform.SetParent(popupCardGO.transform, false);
        var goMessageText = goMessageGO.GetComponent<Text>();
        goMessageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        goMessageText.fontSize = 20;
        goMessageText.color = DarkHudText;
        goMessageText.alignment = TextAnchor.MiddleCenter;
        var goMessageRect = goMessageGO.GetComponent<RectTransform>();
        goMessageRect.anchorMin = new Vector2(0.08f, 0.38f);
        goMessageRect.anchorMax = new Vector2(0.92f, 0.6f);
        goMessageRect.offsetMin = Vector2.zero;
        goMessageRect.offsetMax = Vector2.zero;

        var restartBtnGO = new GameObject("RestartButton", typeof(Image), typeof(Button));
        restartBtnGO.transform.SetParent(popupCardGO.transform, false);
        var restartBtnImage = restartBtnGO.GetComponent<Image>();
        restartBtnImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        restartBtnImage.type = Image.Type.Sliced;
        restartBtnImage.color = BadgeTerracotta;
        var restartBtnRect = restartBtnGO.GetComponent<RectTransform>();
        restartBtnRect.anchorMin = new Vector2(0.5f, 0.12f);
        restartBtnRect.anchorMax = new Vector2(0.5f, 0.12f);
        restartBtnRect.pivot = new Vector2(0.5f, 0.5f);
        restartBtnRect.anchoredPosition = Vector2.zero;
        restartBtnRect.sizeDelta = new Vector2(200f, 76f); // >=72dp per the tap-target constraint

        var restartTextGO = new GameObject("Text", typeof(Text));
        restartTextGO.transform.SetParent(restartBtnGO.transform, false);
        var restartText = restartTextGO.GetComponent<Text>();
        restartText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        restartText.fontSize = 26;
        restartText.color = Color.white;
        restartText.text = "Restart";
        restartText.alignment = TextAnchor.MiddleCenter;
        var restartTextRect = restartTextGO.GetComponent<RectTransform>();
        restartTextRect.anchorMin = Vector2.zero;
        restartTextRect.anchorMax = Vector2.one;
        restartTextRect.offsetMin = Vector2.zero;
        restartTextRect.offsetMax = Vector2.zero;

        var gameOverPopup = gameOverGO.GetComponent<GameOverPopup>();
        SetField(gameOverPopup, "restartButton", restartBtnGO.GetComponent<Button>());
        SetField(gameOverPopup, "titleText", goTitleText);
        SetField(gameOverPopup, "messageText", goMessageText);
        SetField(gameController, "_gameOverPopup", gameOverPopup);
        gameOverGO.SetActive(false); // Hidden by default
```

`BadgeTerracotta` and `DarkHudText` are the existing palette fields already defined at the top of `GameSceneBuilder` (from the prior round) — no new color constants needed. White restart-button text on `BadgeTerracotta` (176,66,40) is the same pair already verified at 5.74:1 for the control-button badges; title/message dark text on the card's off-white is the same pair already verified at 12.67:1.

- [ ] **Step 3: Do not run a batch verification yet**

`GameController.cs` still calls the old `_gameOverPopup.Show(this)` (removed — `GameOverPopup` now exposes `ShowWin`/`ShowStuck` instead) and the old two-argument `BoardView.Build(board, slotsById)` — the project won't compile again until Task 9 fixes `GameController.cs`. Stage and commit without running Unity; Task 9's `RegenerateAll.Run` and EditMode suite verify this task's popup construction along with everything else.

- [ ] **Step 4: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Presentation/HUD/GameOverPopup.cs \
  unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder.cs
git commit -m "feat: redesign end-of-level popup as a card with distinct win/stuck messaging"
```

---

## Task 9: `GameController` — input lock, tap-to-tray orchestration, win/stuck detection

**Files:**
- Modify: `unity/GameClient/Assets/Scripts/Presentation/GameController.cs`
- Modify: `unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder.cs`

**Interfaces:**
- Consumes: `BoardView.Build(BoardState, Dictionary<string,TileSlot>, bool, Action)`, `BoardView.RemoveTileInstant`, `BoardView.TileSet` (Task 5); `TrayView.SpawnFlightCard/GetSlotScreenPosition/PlayArrival/ResolveAfterPush` (Task 7); `GameOverPopup.ShowWin/ShowStuck` (Task 8); `TileView.PlayFadeOutOnly/PlayFadeInOnly` (Task 3); `CardAnimator.MoveRectTransform/FlightDuration` (Task 2); `TileVisual.IconFor/AccentColorFor` (existing).
- Produces: `GameController.IsInputLocked` (bool property) — consumed by Task 10's manual verification and by the HUD button handlers added here. This is the task that makes the whole project compile again.

This is the first point in the plan where a full-project compile is expected to succeed — every other task deliberately deferred its Unity verification here.

- [ ] **Step 1: Rewrite `GameController.cs`**

```csharp
using System;
using System.Collections;
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
        [SerializeField] private Camera _camera;

        private BoardState _board;
        private List<TileSlot> _shape;
        private Dictionary<string, TileSlot> _slotsById;
        private readonly ComboScorer _comboScorer = new ComboScorer();

        public event Action<int, int> ScoreChanged;
        public event Action<int, int, int> UsesChanged;

        // True while the deal-in animation or a tap's flight-to-tray sequence
        // is still playing, so a second tap (or a hint/undo/shuffle press)
        // can't land mid-animation and desync the board from what's visible.
        public bool IsInputLocked { get; private set; }

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

            if (_trayView != null)
                _trayView.Initialize(4); // 4 slots

            IsInputLocked = true;
            _boardView.Build(_board, _slotsById, animateDealIn: true, onDealInComplete: () => IsInputLocked = false);

            ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
            NotifyUsesChanged();
        }

        public void OnTileTapped(string slotId)
        {
            if (_board.IsGameOver) return;
            if (IsInputLocked) return;

            // Compute remaining tiles (not cleared and not in tray)
            var remaining = new HashSet<string>(
                _board.Cells.Where(kv => !kv.Value.Cleared && !_board.TrayTileIds.Contains(kv.Key)).Select(kv => kv.Key));

            if (!FreedomRuleCalculator.IsFree(_slotsById[slotId], remaining))
            {
                _boardView.GetTileView(slotId)?.PlayShake();
                return;
            }

            StartCoroutine(TapToTrayRoutine(slotId));
        }

        // Fades the tapped board tile out, flies a temporary card to its
        // landing slot, and only *then* runs the actual domain push (and
        // whatever match it triggers) — TrayManager.TryPushToTray is called
        // exactly once, unchanged, just later than an instant tap would.
        private IEnumerator TapToTrayRoutine(string slotId)
        {
            IsInputLocked = true;

            var tileView = _boardView.GetTileView(slotId);
            string value = _board.Cells[slotId].Value;
            var icon = TileVisual.IconFor(_boardView.TileSet, value);
            var accentColor = TileVisual.AccentColorFor(_boardView.TileSet, value);
            var oldTrayIds = new List<string>(_board.TrayTileIds);
            int targetIndex = oldTrayIds.Count;

            Vector3 startScreenPos = _camera != null && tileView != null
                ? _camera.WorldToScreenPoint(tileView.transform.position)
                : Vector3.zero;

            bool faded = false;
            tileView?.PlayFadeOutOnly(() => faded = true);
            yield return new WaitUntil(() => faded || tileView == null);

            var flightCard = _trayView.SpawnFlightCard(icon, accentColor, startScreenPos);
            var flightRect = (RectTransform)flightCard.transform;
            Vector3 targetScreenPos = _trayView.GetSlotScreenPosition(targetIndex);
            yield return CardAnimator.MoveRectTransform(flightRect, startScreenPos, targetScreenPos, CardAnimator.FlightDuration);
            Destroy(flightCard);

            _trayView.PlayArrival(targetIndex, icon, accentColor);

            bool pushed = TrayManager.TryPushToTray(_board, _slotsById, slotId);
            if (!pushed)
            {
                // Shouldn't happen given the pre-check and the input lock
                // (nothing else can mutate the board mid-flight), but recover
                // gracefully rather than leaving the tile permanently invisible.
                tileView?.PlayFadeInOnly();
                IsInputLocked = false;
                yield break;
            }

            _boardView.RemoveTileInstant(slotId);

            yield return _trayView.ResolveAfterPush(oldTrayIds, slotId, _board.TrayTileIds, _board);

            _boardView.RefreshFreeStates(_board);
            ScoreChanged?.Invoke(_board.Score, _board.ComboCount);

            CheckEndOfLevel();

            IsInputLocked = false;
        }

        private void CheckEndOfLevel()
        {
            if (_board.Cells.Values.All(c => c.Cleared))
            {
                if (_gameOverPopup != null)
                    _gameOverPopup.ShowWin(this, _board.Score);
            }
            else if (_board.IsGameOver)
            {
                if (_gameOverPopup != null)
                    _gameOverPopup.ShowStuck(this);
            }
        }

        public void OnHintRequested()
        {
            if (IsInputLocked) return;
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
            if (IsInputLocked) return;

            if (UndoStack.TryUndo(_board))
            {
                _boardView.Build(_board, _slotsById, animateDealIn: false);
                ScoreChanged?.Invoke(_board.Score, _board.ComboCount);
                NotifyUsesChanged();
            }
        }

        public void OnShuffleRequested()
        {
            if (IsInputLocked) return;

            try
            {
                if (ShuffleService.Shuffle(_board, _shape, new System.Random()))
                {
                    _boardView.Build(_board, _slotsById, animateDealIn: false);
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

- [ ] **Step 2: Wire the new `_camera` field in `GameSceneBuilder`**

Find `SetField(gameController, "_boardView", boardView);` in `unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder.cs` and add immediately after it:

```csharp
        SetField(gameController, "_camera", camera);
```

- [ ] **Step 3: Full regeneration and EditMode test pass**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -executeMethod RegenerateAll.Run -logFile /tmp/round2-task9-regen.log -quit
grep -E "_DONE|error CS|MISSING" /tmp/round2-task9-regen.log

"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -runTests -testPlatform EditMode -testResults /tmp/round2-task9-results.xml -logFile /tmp/round2-task9-compile.log
grep -iE "error CS" /tmp/round2-task9-compile.log
python3 -c "
import re
with open('/tmp/round2-task9-results.xml') as f: c = f.read()
m = re.search(r'<test-run[^>]*>', c)
print(m.group(0) if m else 'NO TEST-RUN FOUND')
"
```
Expected: `RegenerateAll` reports all 6 `_DONE` markers with no `error CS`/`MISSING`; the EditMode run has no `error CS` and `test-run` shows `failures="0"` (all 38 tests — this pass touches no domain code, so the count should be unchanged from the end of the prior round). Note: omit `-quit` from the `-runTests` invocation — combining it with `-runTests` was found in the prior round to make Unity exit before the test runner actually executes.

- [ ] **Step 4: Commit**

```bash
git add unity/GameClient/Assets/Scripts/Presentation/GameController.cs \
  unity/GameClient/Assets/Scripts/Editor/GameSceneBuilder.cs \
  unity/GameClient/Assets/Scenes/Game.unity \
  unity/GameClient/Assets/Data/DefaultTileSet.asset \
  unity/GameClient/Assets/Prefabs/Tile.prefab
git status --short
git commit -m "feat: orchestrate deal-in lock, tap-to-tray flight, and win/stuck detection in GameController"
```

---

## Task 10: Verification against the acceptance checklist, and APK rebuild

**Files:**
- None (verification-only task).

**Interfaces:**
- Consumes: `RegenerateAll.Run()`, `AndroidBuilder.Build()` (both pre-existing).

There is no way to capture a real in-engine screenshot in this `-nographics` headless environment (confirmed in the prior round — `EditorApplication.isPlaying` never becomes true without a display, so `ScreenshotTest` spins forever and must be killed rather than trusted). Verification here is: (a) the EditMode suite staying green, (b) reasoning through the acceptance checklist against the actual committed code, and (c) an on-device APK test, since that's the only way anyone — including you — can actually see the animations play.

- [ ] **Step 1: Re-run the full EditMode suite one more time against the final state**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -runTests -testPlatform EditMode -testResults /tmp/round2-final-results.xml -logFile /tmp/round2-final-compile.log
grep -iE "error CS" /tmp/round2-final-compile.log
python3 -c "
import re
with open('/tmp/round2-final-results.xml') as f: c = f.read()
m = re.search(r'<test-run[^>]*>', c)
print(m.group(0) if m else 'NO TEST-RUN FOUND')
"
```
Expected: no `error CS`, `failures="0"`.

- [ ] **Step 2: Walk the acceptance checklist against the code, item by item**

- Deal-in stagger/order: `BoardView.Build`'s `OrderBy(Layer).ThenBy(-Y).ThenBy(X)` plus `Mathf.Clamp(1.0f / tileCount, 0.008f, 0.03f)` (Task 5).
- Input disabled during deal-in: `GameController.IsInputLocked` set `true` before `Build(..., animateDealIn: true, ...)`, cleared only in the `onDealInComplete` callback that fires when every tile's `PlayDealIn` has finished (Task 5 + Task 9).
- Tap flies to tray, match-check deferred: `GameController.TapToTrayRoutine` fades the real tile, animates a flight card, and only calls `TrayManager.TryPushToTray` (which does push+match-check atomically, unchanged) after the flight completes (Task 9).
- No square-cornered tiles anywhere: tray slot prefab and `Tile` prefab both built from `CardStyle`'s ratios (Task 1, Task 6); the end-of-level popup card also reuses `CreateCardLayer` (Task 8).
- Matched tray tiles highlight before clearing: `TraySlotView.PlayHighlightThenClear` via `CardAnimator.HighlightThenClear` (Task 4), invoked by `TrayView.ResolveAfterPush` on the two slots whose ids disappeared from `TrayTileIds` (Task 7).
- Tray reflow: `TrayView.ResolveAfterPush` → `ReflowSlot`, reusing `SpawnFlightCard` (Task 7).
- Win/stuck popup, distinct messaging, no navigation added: `GameController.CheckEndOfLevel` (Task 9), `GameOverPopup.ShowWin`/`ShowStuck` (Task 8) — only a Restart button exists, unchanged from the constraint.
- No domain-layer files appear in `git diff` for this round — confirm with `git diff --stat <first-round2-commit>..HEAD -- unity/GameClient/Assets/Scripts/Domain` returning empty.

```bash
git log --oneline | grep -m1 "add shared ITintable" # first Round 2 commit, adjust if the log differs
git diff --stat $(git log --oneline | grep -m1 "add shared ITintable" | cut -d' ' -f1)^..HEAD -- unity/GameClient/Assets/Scripts/Domain
```
Expected: empty output (no domain files touched).

- [ ] **Step 3: Rebuild the APK**

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
"$UNITY" -batchmode -nographics -projectPath unity/GameClient \
  -executeMethod AndroidBuilder.Build -logFile /tmp/round2-android-build.log -quit
grep -E "ANDROID_BUILD_RESULT|ANDROID_BUILD_TOTAL_ERRORS|ANDROID_BUILD_OUTPUT_PATH" /tmp/round2-android-build.log
```
Expected: `ANDROID_BUILD_RESULT: Succeeded`, `ANDROID_BUILD_TOTAL_ERRORS: 0`.

- [ ] **Step 4: Push and report**

```bash
git push origin main
git log --oneline -12
```

Report to the user: EditMode test count/result, APK path, and a plain-language summary of what to look for on-device (staggered deal-in on level start/restart, tiles flying into the tray on tap, tray tiles now matching the board's card look, matched tray pairs flashing before clearing with the rest sliding over, and the new popup on a win or a stuck board). Since none of this was screenshot-verified in-engine, explicitly ask for on-device confirmation rather than asserting it looks right.

