using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation
{
    public class GameOverPopup : MonoBehaviour
    {
        public Button restartButton;
        public Text titleText;
        public Text messageText;

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

        public void ShowWin(GameController controller, int score)
        {
            _gameController = controller;
            if (titleText != null) titleText.text = "Well done!";
            if (messageText != null) messageText.text = "The board is clear! Final score: " + score;
            gameObject.SetActive(true);
        }

        public void ShowStuck(GameController controller)
        {
            _gameController = controller;
            if (titleText != null) titleText.text = "No matches left";
            if (messageText != null) messageText.text = "Try shuffling, or start a fresh board.";
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
