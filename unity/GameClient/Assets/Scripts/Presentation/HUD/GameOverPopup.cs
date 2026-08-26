using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation
{
    public class GameOverPopup : MonoBehaviour
    {
        public Button restartButton;
        private GameController _gameController;

        private void Start()
        {
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(() =>
                {
                    if (_gameController != null)
                        _gameController.RestartLevel();
                });
            }
        }

        public void Show(GameController controller)
        {
            _gameController = controller;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
