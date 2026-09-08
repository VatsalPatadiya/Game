using TMPro;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    // Combo meter (Pass D, guidelines s6): a small gold fill that snaps to full on
    // each combo match and drains over the combo window; an "xN" label pops on
    // change. The whole meter hides when no combo is active (drain complete or a
    // lone match), so it only appears during an actual streak.
    public sealed class ComboMeter3D : MonoBehaviour
    {
        [SerializeField] private GameObject _root;   // shown only during a combo
        [SerializeField] private Transform _fill;    // left-pinned drain bar
        [SerializeField] private TextMeshPro _label; // "xN"
        [SerializeField] private GameController _gameController;
        [SerializeField] private float _trackWidth = 1.4f;
        [SerializeField] private float _fillHeight = 0.12f;
        [SerializeField] private float _windowSeconds = 3f;

        private float _remaining;       // seconds left in the current combo window
        private Vector3 _labelBaseScale = Vector3.one;
        private float _pulse;

        private void OnEnable()
        {
            if (_gameController != null)
                _gameController.ComboChanged += HandleCombo;
            if (_label != null) _labelBaseScale = _label.transform.localScale;
            if (_root != null) _root.SetActive(false);
        }

        private void OnDisable()
        {
            if (_gameController != null)
                _gameController.ComboChanged -= HandleCombo;
        }

        private void HandleCombo(int comboCount)
        {
            if (comboCount < 2)
            {
                // A lone match (streak reset) - let any active meter keep draining
                // but don't refill; nothing to show for x1.
                return;
            }

            _remaining = _windowSeconds;
            _pulse = 1f;
            if (_root != null) _root.SetActive(true);
            if (_label != null) _label.text = "x" + comboCount;
            ApplyFill(1f);
        }

        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                _remaining = 0f;
                _root.SetActive(false);
                return;
            }
            ApplyFill(Mathf.Clamp01(_remaining / _windowSeconds));

            if (_pulse > 0f && _label != null)
            {
                _pulse = Mathf.Max(0f, _pulse - Time.deltaTime * 4f);
                _label.transform.localScale = _labelBaseScale * (1f + 0.25f * _pulse);
            }
        }

        private void ApplyFill(float frac)
        {
            if (_fill == null) return;
            float w = _trackWidth * Mathf.Clamp01(frac);
            _fill.localScale = new Vector3(w, _fillHeight, 1f);
            var p = _fill.localPosition;
            p.x = -_trackWidth * 0.5f + w * 0.5f; // pin the left edge
            _fill.localPosition = p;
        }
    }
}
