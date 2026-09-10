using TMPro;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    // Replaces the plain score plaque: a gold fill that grows left-to-right with
    // the score, plus the score number. Fill is a unit quad whose X scale and X
    // position are driven so its LEFT edge stays pinned while it grows.
    public sealed class ProgressBar3D : MonoBehaviour
    {
        [SerializeField] private MeshFilter _fillFilter;     // plain-quad rectangle fill (never distorts)
        [SerializeField] private TextMeshPro _label;
        [SerializeField] private GameController _gameController;
        [SerializeField] private float _maxScore = 2000f;
        [SerializeField] private float _trackWidth = 2.6f; // USABLE width (pill inner width minus both rounded ends)
        [SerializeField] private float _fillHeight = 0.24f;
        // Mockup's .progress-fill keeps a visible sliver even at 0 score
        // (`width:5%`, and its JS clamps with Math.max(4, ...)). Without this
        // the gold fill collapses to zero width at score 0, leaving the wood
        // bar reading as an empty hollow plank instead of a track with a fill.
        [SerializeField] private float _minVisibleFrac = 0f; // Set to 0 to allow a completely empty fill at 0 score

        // Mockup's .progress-fill animates width over `transition:width 900ms
        // cubic-bezier(.22,.9,.3,1)` on every score change instead of
        // snapping instantly - matched here with an exponential ease-out
        // toward the target fraction each frame, tuned so it's ~95% settled
        // by 0.9s (k = -ln(0.05)/0.9).
        private const float FillLerpRate = 3.3f;
        private float _displayedFrac;
        private float _targetFrac;

        // Score number counts up toward the target instead of snapping (s6:
        // counter changes must animate), with a brief scale-pop on each change.
        private int _targetScore;
        private float _displayedScore;
        private const float ScoreCountRate = 6f;
        private Vector3 _labelBaseScale = Vector3.one;
        private float _pulse; // 0..1 decaying, drives the label scale-pop

        private Mesh _fillMesh;
        private Vector3[] _baseVertices;
        private Vector3[] _workingVertices;

        private void Awake()
        {
            if (_fillFilter != null)
            {
                _fillMesh = _fillFilter.mesh; // Instantiate unique mesh
                _baseVertices = _fillMesh.vertices;
                _workingVertices = new Vector3[_baseVertices.Length];
            }
        }

        private void OnEnable()
        {
            if (_gameController != null)
                _gameController.ScoreChanged += HandleScoreChanged;
            if (_label != null) _labelBaseScale = _label.transform.localScale;
            HandleScoreChanged(0, 0);
            _displayedFrac = _targetFrac;
            _displayedScore = _targetScore;
            ApplyFill(_displayedFrac);
        }

        private void OnDisable()
        {
            if (_gameController != null)
                _gameController.ScoreChanged -= HandleScoreChanged;
        }

        private void Update()
        {
            // Animate the fill.
            if (!Mathf.Approximately(_displayedFrac, _targetFrac))
            {
                _displayedFrac = Mathf.Lerp(_displayedFrac, _targetFrac, 1f - Mathf.Exp(-FillLerpRate * Time.deltaTime));
                if (Mathf.Abs(_displayedFrac - _targetFrac) < 0.0005f) _displayedFrac = _targetFrac;
                ApplyFill(_displayedFrac);
            }

            // Count the score number up toward the target.
            if (_label != null && !Mathf.Approximately(_displayedScore, _targetScore))
            {
                _displayedScore = Mathf.Lerp(_displayedScore, _targetScore, 1f - Mathf.Exp(-ScoreCountRate * Time.deltaTime));
                if (Mathf.Abs(_displayedScore - _targetScore) < 0.5f) _displayedScore = _targetScore;
                _label.text = Mathf.RoundToInt(_displayedScore).ToString();
            }

            // Decay the scale-pop (back-ease overshoot on change).
            if (_pulse > 0f && _label != null)
            {
                _pulse = Mathf.Max(0f, _pulse - Time.deltaTime * 4f);
                float s = 1f + 0.18f * _pulse;
                _label.transform.localScale = _labelBaseScale * s;
            }
        }

        private void HandleScoreChanged(int score, int comboCount)
        {
            _targetFrac = _maxScore > 0f ? Mathf.Clamp01(score / _maxScore) : 0f;
            if (Mathf.RoundToInt(_displayedScore) != score) _pulse = 1f; // kick the pop
            _targetScore = score;
        }

        // The fill mesh is a capsule built at the FULL usable width; here we scale
        // it DOWN by the fraction (0..1). Scaling down only compresses it - it can
        // never balloon (the reported shape bug came from scaling a small mesh UP).
        // Left edge stays pinned; it grows rightward toward the full capsule at 100%.
        private void ApplyFill(float frac)
        {
            if (_fillFilter == null || _fillMesh == null) return;
            float s = Mathf.Clamp01(Mathf.Max(_minVisibleFrac, frac)); // 0..1 fraction
            float targetRightEdgeX = -_trackWidth * 0.5f + _trackWidth * s;
            
            for (int i = 0; i < _baseVertices.Length; i++)
            {
                Vector3 v = _baseVertices[i];
                if (v.x > targetRightEdgeX)
                    v.x = targetRightEdgeX;
                _workingVertices[i] = v;
            }
            _fillMesh.vertices = _workingVertices;
            _fillMesh.RecalculateBounds();
        }
    }
}
