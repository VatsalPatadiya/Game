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
        // Mockup's .progress-fill keeps a visible sliver even at 0 score
        // (`width:5%`, and its JS clamps with Math.max(4, ...)). Without this
        // the gold fill collapses to zero width at score 0, leaving the wood
        // bar reading as an empty hollow plank instead of a track with a fill.
        [SerializeField] private float _minVisibleFrac = 0.05f;

        // Mockup's .progress-fill animates width over `transition:width 900ms
        // cubic-bezier(.22,.9,.3,1)` on every score change instead of
        // snapping instantly - matched here with an exponential ease-out
        // toward the target fraction each frame, tuned so it's ~95% settled
        // by 0.9s (k = -ln(0.05)/0.9).
        private const float FillLerpRate = 3.3f;
        private float _displayedFrac;
        private float _targetFrac;

        private void OnEnable()
        {
            if (_gameController != null)
                _gameController.ScoreChanged += HandleScoreChanged;
            HandleScoreChanged(0, 0);
            _displayedFrac = _targetFrac;
            ApplyFill(_displayedFrac);
        }

        private void OnDisable()
        {
            if (_gameController != null)
                _gameController.ScoreChanged -= HandleScoreChanged;
        }

        private void Update()
        {
            if (Mathf.Approximately(_displayedFrac, _targetFrac)) return;
            _displayedFrac = Mathf.Lerp(_displayedFrac, _targetFrac, 1f - Mathf.Exp(-FillLerpRate * Time.deltaTime));
            if (Mathf.Abs(_displayedFrac - _targetFrac) < 0.0005f) _displayedFrac = _targetFrac;
            ApplyFill(_displayedFrac);
        }

        private void HandleScoreChanged(int score, int comboCount)
        {
            _targetFrac = _maxScore > 0f ? Mathf.Clamp01(score / _maxScore) : 0f;
            if (_label != null) _label.text = score.ToString(); // score number snaps immediately, like the mockup
        }

        private void ApplyFill(float frac)
        {
            if (_fill == null) return;
            float w = _trackWidth * Mathf.Max(_minVisibleFrac, frac);
            _fill.localScale = new Vector3(w, _fillHeight, 1f);
            // pin the left edge: centre sits at -half + w/2
            var p = _fill.localPosition;
            p.x = -_trackWidth * 0.5f + w * 0.5f;
            _fill.localPosition = p;
        }
    }
}
