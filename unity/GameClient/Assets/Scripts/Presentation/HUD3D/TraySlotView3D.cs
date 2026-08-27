using System.Collections;
using GameClient.Presentation.Board;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    public sealed class TraySlotView3D : MonoBehaviour
    {
        [SerializeField] private Transform _content;
        [SerializeField] private MeshRenderer _bodyRenderer;
        [SerializeField] private MeshRenderer _iconRenderer;
        [SerializeField] private Color _highlightEmission = new Color(1f, 0.85f, 0.2f, 1f);

        private MeshRendererTint _bodyTint;
        private MeshRendererTint _iconTint;
        private MeshRendererTint _emissionTint;
        private Coroutine _clearCoroutine;
        private Coroutine _popInCoroutine;

        private void Awake()
        {
            _bodyTint = new MeshRendererTint(_bodyRenderer, "_BaseColor");
            _iconTint = new MeshRendererTint(_iconRenderer, "_BaseColor");
            _emissionTint = new MeshRendererTint(_bodyRenderer, "_EmissionColor");
        }

        public void SetEmpty()
        {
            _emissionTint.Color = Color.black;
            if (_iconRenderer != null) _iconRenderer.enabled = false;
        }

        public void SetFilled(Sprite icon, Color accentColor)
        {
            accentColor.a = 1f;
            _emissionTint.Color = Color.black;
            if (_iconRenderer != null)
            {
                _iconRenderer.sharedMaterial.SetTexture("_BaseMap", icon.texture);
                _iconTint.Color = accentColor;
                _iconRenderer.enabled = true;
            }
        }

        public void PlayPopIn(Sprite icon, Color accentColor)
        {
            SetFilled(icon, accentColor);
            if (_popInCoroutine != null) StopCoroutine(_popInCoroutine);
            _popInCoroutine = StartCoroutine(PopInRoutine());
        }

        private IEnumerator PopInRoutine()
        {
            float duration = CardAnimator.TrayPopInDuration;
            float overshoot = CardAnimator.TrayPopInOvershoot;
            const float overshootFraction = 0.7f;

            _content.localScale = Vector3.zero;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = t < overshootFraction
                    ? Mathf.Lerp(0f, overshoot, t / overshootFraction)
                    : Mathf.Lerp(overshoot, 1f, (t - overshootFraction) / (1f - overshootFraction));
                _content.localScale = Vector3.one * scale;
                yield return null;
            }

            _content.localScale = Vector3.one;
        }

        public void PlayHighlightThenClear(System.Action onComplete)
        {
            if (_clearCoroutine != null) StopCoroutine(_clearCoroutine);
            _clearCoroutine = StartCoroutine(CardAnimator.HighlightThenClear(
                _emissionTint, _highlightEmission, _content, new ITintable[] { _bodyTint, _iconTint },
                () =>
                {
                    _content.localScale = Vector3.one;
                    SetEmpty();
                    onComplete?.Invoke();
                }));
        }
    }
}
