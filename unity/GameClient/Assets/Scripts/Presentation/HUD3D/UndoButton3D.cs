using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    public sealed class UndoButton3D : MonoBehaviour
    {
        [SerializeField] private HudButton3D _hudButton;
        [SerializeField] private GameController _gameController;

        private void Start()
        {
            if (_hudButton != null && _hudButton.Button != null)
                _hudButton.Button.OnClick += () => _gameController.OnUndoRequested();
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
            if (_hudButton != null && _hudButton.UsesDisplay != null)
                _hudButton.UsesDisplay.SetRemaining(undosRemaining);
        }
    }
}
