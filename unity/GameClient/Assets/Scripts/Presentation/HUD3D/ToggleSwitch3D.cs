using System;
using System.Collections;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    // Small iOS-style on/off switch: a pill track that recolors by state plus
    // a knob that slides between the two ends. Lives on the same GameObject
    // as the PressScaleButton3D that owns the tap hit-box - this component
    // only owns the visual state, the slide animation, and the on/off value.
    [RequireComponent(typeof(PressScaleButton3D))]
    public sealed class ToggleSwitch3D : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _trackRenderer;
        [SerializeField] private Material _onMaterial;
        [SerializeField] private Material _offMaterial;
        [SerializeField] private Transform _knob;
        [SerializeField] private float _knobOffLocalX;
        [SerializeField] private float _knobOnLocalX;

        public event Action<bool> OnToggled;
        public bool IsOn { get; private set; }

        private Coroutine _slideAnimation;

        private void Awake()
        {
            GetComponent<PressScaleButton3D>().OnClick += HandleClick;
        }

        private void HandleClick()
        {
            SetOn(!IsOn, animate: true);
            OnToggled?.Invoke(IsOn);
        }

        // animate:false is for initial sync from persisted settings (no
        // animation should play before the popup itself has shown).
        public void SetOn(bool on, bool animate)
        {
            IsOn = on;
            if (_trackRenderer != null)
                _trackRenderer.sharedMaterial = on ? _onMaterial : _offMaterial;

            if (_knob == null) return;
            float targetX = on ? _knobOnLocalX : _knobOffLocalX;
            if (_slideAnimation != null) StopCoroutine(_slideAnimation);
            if (animate && gameObject.activeInHierarchy)
            {
                _slideAnimation = StartCoroutine(SlideKnob(targetX));
            }
            else
            {
                var p = _knob.localPosition;
                _knob.localPosition = new Vector3(targetX, p.y, p.z);
            }
        }

        private IEnumerator SlideKnob(float targetX)
        {
            const float duration = 0.12f;
            var start = _knob.localPosition;
            var end = new Vector3(targetX, start.y, start.z);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - t) * (1f - t);
                _knob.localPosition = Vector3.Lerp(start, end, eased);
                yield return null;
            }
            _knob.localPosition = end;
        }
    }
}
