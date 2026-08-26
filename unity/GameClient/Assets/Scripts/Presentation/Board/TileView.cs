using System.Collections;
using UnityEngine;

namespace GameClient.Presentation.Board
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class TileView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _backgroundRenderer;
        [SerializeField] private SpriteRenderer _iconRenderer;
        [SerializeField] private TextMesh _textMesh;
        [SerializeField] private Color _freeColor = Color.white;
        [SerializeField] private Color _blockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        [SerializeField] private Color _highlightColor = new Color(1f, 0.85f, 0.2f, 1f);
        
        private Vector3 _originalLocalPos;
        private Coroutine _shakeCoroutine;

        public string SlotId { get; private set; }
        public int Layer { get; private set; }

        public void Initialize(string slotId, int layer, Color tileColor, string value)
        {
            SlotId = slotId;
            Layer = layer;

            // Apply a slight isometric offset based on layer to give a 3D stacked feel
            _originalLocalPos = transform.localPosition + new Vector3(layer * -0.04f, layer * 0.08f, 0f);
            transform.localPosition = _originalLocalPos;

            if (_iconRenderer != null)
                _iconRenderer.color = tileColor;
            
            if (_textMesh != null)
            {
                // Convert "pair_0" to "A", "pair_1" to "B", etc.
                if (value != null && value.StartsWith("pair_"))
                {
                    if (int.TryParse(value.Substring(5), out int num))
                        _textMesh.text = ((char)('A' + num)).ToString();
                    else
                        _textMesh.text = value.Substring(0, 1);
                }
                else
                {
                    _textMesh.text = value != null && value.Length > 0 ? value.Substring(0, 1) : "";
                }
            }
        }

        public void SetFree(bool isFree)
        {
            if (_backgroundRenderer != null)
            {
                // Darken lower layers slightly, and darken even more if blocked
                float layerBrightness = Mathf.Clamp01(1f - (2 - Layer) * 0.1f);
                Color baseColor = isFree ? _freeColor : _blockedColor;
                _backgroundRenderer.color = new Color(baseColor.r * layerBrightness, baseColor.g * layerBrightness, baseColor.b * layerBrightness, baseColor.a);
            }
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

        public void PlayShake()
        {
            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            float duration = 0.2f;
            float elapsed = 0f;
            
            // Flash red
            if (_backgroundRenderer != null) _backgroundRenderer.color = Color.red;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float xOffset = Mathf.Sin(elapsed * 40f) * 0.1f;
                transform.localPosition = _originalLocalPos + new Vector3(xOffset, 0, 0);
                yield return null;
            }

            transform.localPosition = _originalLocalPos;
            
            // Re-apply original color (assume blocked since it was shaken)
            if (_backgroundRenderer != null)
            {
                float layerBrightness = Mathf.Clamp01(1f - (2 - Layer) * 0.1f);
                _backgroundRenderer.color = new Color(_blockedColor.r * layerBrightness, _blockedColor.g * layerBrightness, _blockedColor.b * layerBrightness, _blockedColor.a);
            }
        }
    }
}
