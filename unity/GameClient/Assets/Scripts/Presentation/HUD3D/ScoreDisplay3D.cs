using TMPro;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    public sealed class ScoreDisplay3D : MonoBehaviour
    {
        [SerializeField] private TextMeshPro _scoreText;
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
