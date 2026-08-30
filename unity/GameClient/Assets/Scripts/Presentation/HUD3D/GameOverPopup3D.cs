using TMPro;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    public class GameOverPopup3D : MonoBehaviour
    {
        public PressScaleButton3D restartButton;
        public TextMeshPro titleText;
        public TextMeshPro messageText;

        private GameController _gameController;

        private void Start()
        {
            if (restartButton != null)
                restartButton.OnClick += () => _gameController?.RestartLevel();
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

        public void ShowLose(GameController controller)
        {
            _gameController = controller;
            if (titleText != null) titleText.text = "Tray full!";
            if (messageText != null) messageText.text = "No more matches possible. Try again!";
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
