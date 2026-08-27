using TMPro;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    // Replaces the plain score plaque: a gold fill that grows left-to-right with
    // the score, plus the score number. Fill is a unit quad whose X scale and X
    // position are driven so its LEFT edge stays pinned while it grows.
    public sealed class ProgressBar3D : MonoBehaviour
    {
        [SerializeField] private Transform _fill;
        [SerializeField] private TextMeshPro _label;
        [SerializeField] private GameController _gameController;
        [SerializeField] private float _maxScore = 2000f;
        [SerializeField] private float _trackWidth = 2.6f;
        [SerializeField] private float _fillHeight = 0.24f;

        private void OnEnable()
        {
            if (_gameController != null)
                _gameController.ScoreChanged += HandleScoreChanged;
            HandleScoreChanged(0, 0);
        }

        private void OnDisable()
        {
            if (_gameController != null)
                _gameController.ScoreChanged -= HandleScoreChanged;
        }

        private void HandleScoreChanged(int score, int comboCount)
        {
            float frac = _maxScore > 0f ? Mathf.Clamp01(score / _maxScore) : 0f;
            if (_fill != null)
            {
                float w = _trackWidth * frac;
                _fill.localScale = new Vector3(w, _fillHeight, 1f);
                // pin the left edge: centre sits at -half + w/2
                var p = _fill.localPosition;
                p.x = -_trackWidth * 0.5f + w * 0.5f;
                _fill.localPosition = p;
            }
            if (_label != null) _label.text = score.ToString();
        }
    }
}
