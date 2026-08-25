using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class ShuffleButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private GameController _gameController;

        private void Awake()
        {
            if (_button != null)
                _button.onClick.AddListener(() => _gameController.OnShuffleRequested());
        }
    }
}
