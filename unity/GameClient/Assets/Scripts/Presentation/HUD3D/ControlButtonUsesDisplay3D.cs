using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    public sealed class ControlButtonUsesDisplay3D : MonoBehaviour
    {
        [SerializeField] private PressScaleButton3D _button;
        [SerializeField] private MeshRenderer _faceRenderer;
        [SerializeField] private MeshRenderer _iconRenderer;
        [SerializeField] private TMPro.TextMeshPro _badgeText;
        [SerializeField] private float _disabledAlpha = 0.4f;

        // Must match GameSceneBuilder3D's DarkHudText - the icon material there
        // is tinted this color because its source PNGs are opaque-white glyphs
        // shaped by alpha, invisible untinted against the white face. Tint
        // defaults to white otherwise (see MeshRendererTint), which is correct
        // for TileView3D/TraySlotView3D's pre-colored icon textures but would
        // silently overwrite this tint back to white the first time
        // SetRemaining runs, since SetAlpha only ever adjusts the alpha of
        // whatever color the tint was constructed with.
        private static readonly Color IconTintColor = new Color(40f / 255f, 46f / 255f, 36f / 255f, 1f);

        private GameClient.Presentation.Board.MeshRendererTint _faceTint;
        private GameClient.Presentation.Board.MeshRendererTint _iconTint;

        private void Awake()
        {
            if (_faceRenderer != null) _faceTint = new GameClient.Presentation.Board.MeshRendererTint(_faceRenderer);
            if (_iconRenderer != null) _iconTint = new GameClient.Presentation.Board.MeshRendererTint(_iconRenderer, initialColor: IconTintColor);
        }

        public void SetRemaining(int remaining)
        {
            if (_badgeText != null)
                _badgeText.text = remaining.ToString();

            bool available = remaining > 0;
            if (_button != null)
                _button.Interactable = available;

            float alpha = available ? 1f : _disabledAlpha;
            SetAlpha(_faceTint, alpha);
            SetAlpha(_iconTint, alpha);
            if (_badgeText != null)
            {
                var c = _badgeText.color;
                c.a = alpha;
                _badgeText.color = c;
            }
        }

        private static void SetAlpha(GameClient.Presentation.Board.MeshRendererTint tint, float alpha)
        {
            if (tint == null) return;
            var c = tint.Color;
            c.a = alpha;
            tint.Color = c;
        }
    }
}
