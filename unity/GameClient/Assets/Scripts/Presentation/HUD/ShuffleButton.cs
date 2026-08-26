using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class ShuffleButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private ControlButtonUsesDisplay _usesDisplay;
        [SerializeField] private GameController _gameController;

        private void Start()
        {
            if (_button != null)
                _button.onClick.AddListener(() => _gameController.OnShuffleRequested());
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
            _usesDisplay?.SetRemaining(shufflesRemaining);
        }
    }
}
