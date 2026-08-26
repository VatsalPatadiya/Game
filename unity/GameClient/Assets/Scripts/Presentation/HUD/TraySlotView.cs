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
