using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    public sealed class ControlButtonUsesDisplay3D : MonoBehaviour
    {
        [SerializeField] private PressScaleButton3D _button;
        [SerializeField] private MeshRenderer _faceRenderer;
        [SerializeField] private SpriteRenderer _iconSpriteRenderer;
        [SerializeField] private Renderer _badgeRenderer;
        [SerializeField] private TMPro.TextMeshPro _badgeText;
        [SerializeField] private Material _enabledFaceMaterial;
        [SerializeField] private Material _disabledFaceMaterial;

        [SerializeField] private Color _enabledIconColor = Color.white;
        [SerializeField] private Color _disabledIconColor = new Color(0.50f, 0.55f, 0.62f, 0.5f);

        public void SetRemaining(int remaining)
        {
            if (_badgeText != null)
                _badgeText.text = remaining.ToString();

            bool available = remaining > 0;
            if (_button != null)
                _button.Interactable = available;

            if (_faceRenderer != null)
            {
                if (available)
                {
                    if (_enabledFaceMaterial != null) _faceRenderer.sharedMaterial = _enabledFaceMaterial;
                }
                else
                {
                    if (_disabledFaceMaterial != null) _faceRenderer.sharedMaterial = _disabledFaceMaterial;
                }
            }

            if (_iconSpriteRenderer != null)
            {
                _iconSpriteRenderer.color = available ? _enabledIconColor : _disabledIconColor;
            }

            if (_badgeRenderer != null)
            {
                _badgeRenderer.gameObject.SetActive(available);
            }
            if (_badgeText != null)
            {
                _badgeText.gameObject.SetActive(available);
            }
        }
    }
}
