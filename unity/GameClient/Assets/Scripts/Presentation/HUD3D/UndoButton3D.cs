using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    public sealed class UndoButton3D : MonoBehaviour
    {
        [SerializeField] private PressScaleButton3D _button;
        [SerializeField] private ControlButtonUsesDisplay3D _usesDisplay;
        [SerializeField] private GameController _gameController;

        private void Start()
        {
            if (_button != null)
                _button.OnClick += () => _gameController.OnUndoRequested();
        }

        private void OnEnable()
        {
            if (_gameController != null)
                _gameController.UsesChanged += HandleUsesChanged;
        }

        private void OnDisable()
        {
            if (_gameController != null)
                _gameController.UsesChanged -= HandleUsesChanged;
        }

        private void HandleUsesChanged(int hintsRemaining, int undosRemaining, int shufflesRemaining)
        {
            _usesDisplay?.SetRemaining(undosRemaining);
        }
    }
}
