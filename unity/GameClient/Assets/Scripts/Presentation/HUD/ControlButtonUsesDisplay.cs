using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class ControlButtonUsesDisplay : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _faceImage;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _badgeBackground;
        [SerializeField] private Text _badgeText;
        [SerializeField] private float _disabledAlpha = 0.4f;

        public void SetRemaining(int remaining)
        {
            if (_badgeText != null)
                _badgeText.text = remaining.ToString();

            bool available = remaining > 0;
            if (_button != null)
                _button.interactable = available;

            float alpha = available ? 1f : _disabledAlpha;
            SetAlpha(_faceImage, alpha);
            SetAlpha(_iconImage, alpha);
            SetAlpha(_badgeBackground, alpha);
            SetAlpha(_badgeText, alpha);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
