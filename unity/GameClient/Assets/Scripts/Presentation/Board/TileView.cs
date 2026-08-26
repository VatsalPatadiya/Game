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

        // Diagonal offset applied per layer so a covering tile visibly
        // reveals a sliver of whatever it's stacked on (the reference's
        // cascading-deck look) - was -0.04/0.08, which was under 10% of a
        // tile and read as flat with a faint shadow, not physically stacked.
        private const float LayerOffsetX = -0.15f;
        private const float LayerOffsetY = 0.18f;

        // Pulled well in front of every layer while being dragged (layers
        // only go up to -maxLayer*0.1 in Z) so the lifted tile always
        // renders above its neighbors, then restored on snap-back.
        private const float DragLiftZ = -1f;
        private const float DragSnapBackDuration = 0.18f;

        private Vector3 _originalLocalPos;
        private Coroutine _shakeCoroutine;
        private Coroutine _clearCoroutine;
        private Coroutine _fadeCoroutine;
        private Coroutine _dragSnapCoroutine;

        public string SlotId { get; private set; }
        public int Layer { get; private set; }

        public void Initialize(string slotId, int layer, Sprite icon, Color accentColor)
        {
            SlotId = slotId;
            Layer = layer;

            _originalLocalPos = transform.localPosition + new Vector3(layer * LayerOffsetX, layer * LayerOffsetY, 0f);
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

        // Tap-confirm flash (~70ms) then a quick scale-down+fade (~100ms) in
        // place - replaces the old cross-screen flying-proxy: footage showed
        // no in-transit frame was catchable even at 10fps, so the tile just
        // needs to disappear fast at its own position while the tray slot
        // pops in on its own, concurrently (see TraySlotView.PlayPopIn).
        public void PlayTapAway(System.Action onComplete)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(TapAwayRoutine(onComplete));
        }

        private IEnumerator TapAwayRoutine(System.Action onComplete)
        {
            if (_selectionGlowRenderer != null)
            {
                var c = _selectionGlowColor;
                c.a = 1f;
                _selectionGlowRenderer.color = c;
            }

            yield return new WaitForSeconds(CardAnimator.TapConfirmFlashDuration);

            if (_selectionGlowRenderer != null)
            {
                var c = _selectionGlowColor;
                c.a = 0f;
                _selectionGlowRenderer.color = c;
            }

            yield return CardAnimator.ScaleDownAndFadeOut(
                transform, BuildRendererArray(), CardAnimator.TapAwayDuration, onComplete);
        }

        // Restores full scale and visibility; only used defensively if a
        // tap-away completes but the domain push turns out to be invalid.
        public void PlayFadeInOnly()
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(RestoreAfterTapAway());
        }

        private IEnumerator RestoreAfterTapAway()
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

            float duration = CardAnimator.TapAwayDuration;
            float elapsed = 0f;
            var startScale = transform.localScale;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.one, t);
                for (int i = 0; i < tints.Length; i++)
                {
                    if (tints[i] == null) continue;
                    var c = tints[i].Color;
                    c.a = Mathf.Lerp(fromAlphas[i], 1f, t);
                    tints[i].Color = c;
                }
                yield return null;
            }

            transform.localScale = Vector3.one;
            for (int i = 0; i < tints.Length; i++)
            {
                if (tints[i] == null) continue;
                var c = tints[i].Color;
                c.a = 1f;
                tints[i].Color = c;
            }
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

        // Drag-to-peek: the player can nudge any tile aside with a finger
        // drag to see what's stacked underneath it. Purely visual - it only
        // moves this transform, never touches board/domain state - and
        // always springs back to _originalLocalPos on release, so the
        // player's actual slot/layer never changes from a drag.
        public void BeginDrag()
        {
            if (_dragSnapCoroutine != null)
            {
                StopCoroutine(_dragSnapCoroutine);
                _dragSnapCoroutine = null;
            }
        }

        public void UpdateDragOffset(Vector3 worldDelta)
        {
            transform.localPosition = _originalLocalPos + new Vector3(worldDelta.x, worldDelta.y, DragLiftZ);
        }

        public void EndDrag()
        {
            if (_dragSnapCoroutine != null) StopCoroutine(_dragSnapCoroutine);
            _dragSnapCoroutine = StartCoroutine(SnapBackRoutine());
        }

        private IEnumerator SnapBackRoutine()
        {
            var start = transform.localPosition;
            float elapsed = 0f;
            while (elapsed < DragSnapBackDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / DragSnapBackDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                transform.localPosition = Vector3.Lerp(start, _originalLocalPos, eased);
                yield return null;
            }
            transform.localPosition = _originalLocalPos;
            _dragSnapCoroutine = null;
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
