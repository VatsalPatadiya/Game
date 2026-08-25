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

        public string SlotId { get; private set; }
        public int Layer { get; private set; }

        public void Initialize(string slotId, int layer, Color tileColor, string value)
        {
            SlotId = slotId;
            Layer = layer;
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
