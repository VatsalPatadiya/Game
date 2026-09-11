using TMPro;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    public class GameOverPopup3D : MonoBehaviour
    {
        public PressScaleButton3D restartButton;
        public TextMeshPro titleText;
        public TextMeshPro messageText;
        public TextMeshPro primaryButtonText;
        // 3 star quads built by GameSceneBuilder3D.BuildStarRow; colored at
        // runtime per the actual result instead of a fixed sample.
        public MeshRenderer[] starRenderers;
        // So a win/lose/stuck popup can never render underneath an open Pause
        // overlay - each one force-closes the other before showing itself.
        public PauseMenu3D pauseMenu;

        private static readonly Color StarGold = new Color(0.96f, 0.78f, 0.36f);
        private static readonly Color StarMuted = new Color(0.44f, 0.32f, 0.18f);

        private GameController _gameController;

        private void Start()
        {
            if (restartButton != null)
                restartButton.OnClick += () => _gameController?.RestartLevel();
        }

        public void ShowWin(GameController controller, int score, int starsEarned)
        {
            _gameController = controller;
            pauseMenu?.Hide();
            if (titleText != null) titleText.text = "Well done!";
            if (messageText != null) messageText.text = "The board is clear! Final score: " + score;
            if (primaryButtonText != null) primaryButtonText.text = "Next Level";
            SetStars(starsEarned);
            gameObject.SetActive(true);
        }

        public void ShowStuck(GameController controller)
        {
            _gameController = controller;
            pauseMenu?.Hide();
            if (titleText != null) titleText.text = "No matches left";
            if (messageText != null) messageText.text = "Try shuffling, or start a fresh board.";
            if (primaryButtonText != null) primaryButtonText.text = "Try Again";
            SetStars(-1);
            gameObject.SetActive(true);
        }

        public void ShowLose(GameController controller)
        {
            _gameController = controller;
            pauseMenu?.Hide();
            if (titleText != null) titleText.text = "Tray full!";
            if (messageText != null) messageText.text = "No more matches possible. Try again!";
            if (primaryButtonText != null) primaryButtonText.text = "Try Again";
            SetStars(-1);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // filledCount < 0 hides the row entirely (loses/stuck/daily have no
        // star rating).
        private void SetStars(int filledCount)
        {
            if (starRenderers == null) return;
            bool show = filledCount >= 0;
            for (int i = 0; i < starRenderers.Length; i++)
            {
                var r = starRenderers[i];
                if (r == null) continue;
                r.gameObject.SetActive(show);
                if (show) r.material.SetColor("_BaseColor", i < filledCount ? StarGold : StarMuted);
            }
        }
    }
}
