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
}
