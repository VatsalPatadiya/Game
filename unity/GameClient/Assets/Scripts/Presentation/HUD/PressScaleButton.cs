using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameClient.Presentation.HUD
{
    // Scales the button down slightly on press and back on release, so it
    // visibly responds the instant it's touched rather than only reacting
    // once the click actually fires.
    public sealed class PressScaleButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float _pressedScale = 0.93f;
        [SerializeField] private float _animationDuration = 0.1f;

        private Vector3 _originalScale;
        private Coroutine _scaleCoroutine;

        private void Awake()
        {
            _originalScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData) => AnimateTo(_originalScale * _pressedScale);
        public void OnPointerUp(PointerEventData eventData) => AnimateTo(_originalScale);
        public void OnPointerExit(PointerEventData eventData) => AnimateTo(_originalScale);

        private void AnimateTo(Vector3 target)
        {
            if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = StartCoroutine(ScaleRoutine(target));
        }

        private IEnumerator ScaleRoutine(Vector3 target)
        {
            var start = transform.localScale;
            float elapsed = 0f;
            while (elapsed < _animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);
                transform.localScale = Vector3.Lerp(start, target, t);
                yield return null;
            }
            transform.localScale = target;
        }
    }
}
