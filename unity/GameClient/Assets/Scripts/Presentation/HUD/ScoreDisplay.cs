using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    public sealed class ScoreDisplay : MonoBehaviour
    {
        [SerializeField] private Text _scoreText;
        [SerializeField] private GameController _gameController;

        private void OnEnable()
        {
            if (_gameController != null)
                _gameController.ScoreChanged += HandleScoreChanged;
        }

        private void OnDisable()
        {
            if (_gameController != null)
                _gameController.ScoreChanged -= HandleScoreChanged;
        }

        private void HandleScoreChanged(int score, int comboCount)
        {
            if (_scoreText == null) return;
            _scoreText.text = comboCount > 1
                ? "Score: " + score + "  x" + comboCount
                : "Score: " + score;
        }
    }
}
