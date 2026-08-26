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

        public string SlotId { get; private set; }
        public int Layer { get; private set; }

        public void Initialize(string slotId, int layer, Sprite icon, Color accentColor)
        {
            SlotId = slotId;
            Layer = layer;

            // Apply a slight isometric offset based on layer to give a 3D stacked feel
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

        public void SetFree(bool isFree)
        {
            RefreshCardColor(isFree, Layer);
        }

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

        public void PlayClearAndDestroy()
        {
            if (_clearCoroutine != null) StopCoroutine(_clearCoroutine);
            _clearCoroutine = StartCoroutine(ClearRoutine());
        }

        private IEnumerator ClearRoutine()
        {
            const float duration = 0.2f;
            float elapsed = 0f;
            var startScale = transform.localScale;
            var endScale = startScale * 1.15f;

            var renderers = new[] { _shadowRenderer, _selectionGlowRenderer, _accentRenderer, _cardRenderer, _iconRenderer };
            var startAlphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                startAlphas[i] = renderers[i] != null ? renderers[i].color.a : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.localScale = Vector3.Lerp(startScale, endScale, t);

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    var c = renderers[i].color;
                    c.a = startAlphas[i] * (1f - t);
                    renderers[i].color = c;
                }

                yield return null;
            }

            Destroy(gameObject);
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
    }
}
