using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class UndoButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private GameController _gameController;

        private void Start()
        {
            if (_button != null)
                _button.onClick.AddListener(() => _gameController.OnUndoRequested());
        }
    }
}
