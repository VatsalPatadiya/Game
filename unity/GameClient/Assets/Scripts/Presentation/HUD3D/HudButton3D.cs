using UnityEngine;
using TMPro;

namespace GameClient.Presentation.HUD3D
{
    public sealed class HudButton3D : MonoBehaviour
    {
        public PressScaleButton3D Button;
        public ControlButtonUsesDisplay3D UsesDisplay;
        public SpriteRenderer IconRenderer;
        public TextMeshPro LevelLabel;
    }
}
