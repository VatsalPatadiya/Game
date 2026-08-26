using System.Collections;
using GameClient.Presentation.Board;
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    // Root holds a static left-edge Divider plus a Content child (glow +
    // icon) - animations scale/fade Content only, so the divider between
    // tray sections stays put while a tile pops in or clears. There's no
    // per-slot card/accent/shadow anymore: the tray panel itself is one
    // continuous bar (see GameSceneBuilder), and each section just shows
    // its icon directly against that shared background, matching the
    // reference footage's single-bar-with-dividers construction.
    public sealed class TraySlotView : MonoBehaviour
    {
        [SerializeField] private RectTransform _content;
        [SerializeField] private Image _glowImage;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Color _highlightColor = new Color(1f, 0.85f, 0.2f, 1f);

        private Coroutine _clearCoroutine;
        private Coroutine _popInCoroutine;

        public RectTransform RectTransform => (RectTransform)transform;

        public void SetEmpty()
        {
            if (_glowImage != null) { var c = _highlightColor; c.a = 0f; _glowImage.color = c; }
            if (_iconImage != null) _iconImage.enabled = false;
        }

        public void SetFilled(Sprite icon, Color accentColor)
        {
            accentColor.a = 1f;
            if (_glowImage != null) { var c = _highlightColor; c.a = 0f; _glowImage.color = c; }

            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.color = accentColor;
                _iconImage.enabled = true;
            }
        }

        // Fills the slot then pops it in (scale 0 -> slight overshoot -> 1),
        // timed to land at essentially the same moment the tapped board tile
        // finishes disappearing (see TileView.PlayTapAway) - the two read as
        // connected by timing rather than by a visible path between them.
        public void PlayPopIn(Sprite icon, Color accentColor)
        {
            SetFilled(icon, accentColor);
            if (_popInCoroutine != null) StopCoroutine(_popInCoroutine);
            _popInCoroutine = StartCoroutine(PopInRoutine());
        }

        private IEnumerator PopInRoutine()
        {
            float duration = CardAnimator.TrayPopInDuration;
            float overshoot = CardAnimator.TrayPopInOvershoot;
            const float overshootFraction = 0.7f;

            _content.localScale = Vector3.zero;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = t < overshootFraction
                    ? Mathf.Lerp(0f, overshoot, t / overshootFraction)
                    : Mathf.Lerp(overshoot, 1f, (t - overshootFraction) / (1f - overshootFraction));
                _content.localScale = Vector3.one * scale;
                yield return null;
            }

            _content.localScale = Vector3.one;
        }

        // Brief highlight so the player can see which two tiles matched, then
        // the same scale-up+fade used everywhere else, then resets to the
        // empty state — this slot is reused, never destroyed.
        public void PlayHighlightThenClear(System.Action onComplete)
        {
            if (_clearCoroutine != null) StopCoroutine(_clearCoroutine);
            var glow = _glowImage != null ? new ImageTint(_glowImage) : null;
            _clearCoroutine = StartCoroutine(CardAnimator.HighlightThenClear(
                glow, _highlightColor, _content, BuildRendererArray(),
                () =>
                {
                    _content.localScale = Vector3.one;
                    SetEmpty();
                    onComplete?.Invoke();
                }));
        }

        private ITintable[] BuildRendererArray()
        {
            return new ITintable[]
            {
                _glowImage != null ? new ImageTint(_glowImage) : null,
                _iconImage != null ? new ImageTint(_iconImage) : null,
            };
        }
    }
}
