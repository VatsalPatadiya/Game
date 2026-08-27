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

    // Reads/writes MUST go through a MaterialPropertyBlock, not renderer.material,
    // so every tile can share the one CardMaterialGenerator material asset
    // (Task 3) instead of Unity silently instancing a new material per tile -
    // the exact per-tile-tint-without-per-tile-material-instance trick the 2D
    // SpriteRenderer.color path got for free.
    public sealed class MeshRendererTint : ITintable
    {
        private readonly MeshRenderer _renderer;
        private readonly string _colorProperty;
        private readonly MaterialPropertyBlock _block;
        private Color _color;

        public MeshRendererTint(MeshRenderer renderer, string colorProperty = "_BaseColor", Color? initialColor = null)
        {
            _renderer = renderer;
            _colorProperty = colorProperty;
            _block = new MaterialPropertyBlock();
            _color = initialColor ?? Color.white;
        }

        public Color Color
        {
            get => _color;
            set
            {
                _color = value;
                _renderer.GetPropertyBlock(_block);
                _block.SetColor(_colorProperty, value);
                _renderer.SetPropertyBlock(_block);
            }
        }
    }
}
