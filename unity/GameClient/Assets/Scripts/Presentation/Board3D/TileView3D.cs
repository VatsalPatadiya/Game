using System.Collections;
using GameClient.Presentation.Board;
using UnityEngine;

namespace GameClient.Presentation.Board3D
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TileView3D : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _bodyRenderer;
        [SerializeField] private BoxCollider _bodyCollider;
        [SerializeField] private MeshRenderer _iconRenderer;
        [SerializeField] private Color _freeCardColor = new Color(0.969f, 0.957f, 0.922f, 1f);
        [SerializeField] private Color _blockedCardColor = new Color(0.62f, 0.63f, 0.58f, 1f);
        [SerializeField] private Color _highlightEmission = new Color(1f, 0.85f, 0.2f, 1f);

        private MeshRendererTint _bodyTint;
        private MeshRendererTint _iconTint;
        private MeshRendererTint _emissionTint;
        private Vector3 _originalLocalPos;
        private Coroutine _shakeCoroutine;
        private Coroutine _clearCoroutine;
        private Coroutine _fadeCoroutine;

        public string SlotId { get; private set; }
        public int Layer { get; private set; }

        public void Initialize(string slotId, int layer, Sprite icon, Color accentColor)
        {
            SlotId = slotId;
            Layer = layer;

            _bodyTint = new MeshRendererTint(_bodyRenderer, "_BaseColor");
            _iconTint = new MeshRendererTint(_iconRenderer, "_BaseColor");
            _emissionTint = new MeshRendererTint(_bodyRenderer, "_EmissionColor");

            _originalLocalPos = transform.localPosition;
            transform.localScale = Vector3.one;

            if (_iconRenderer != null)
            {
                _iconRenderer.sharedMaterial.SetTexture("_BaseMap", icon.texture);
                var c = accentColor;
                c.a = 1f;
                _iconTint.Color = c;
            }

            var noEmission = Color.black;
            _emissionTint.Color = noEmission;

            RefreshCardColor(true);
        }

        public void SetFree(bool isFree) => RefreshCardColor(isFree);

        private void RefreshCardColor(bool isFree)
        {
            _bodyTint.Color = isFree ? _freeCardColor : _blockedCardColor;
        }

        public void Highlight()
        {
            _emissionTint.Color = _highlightEmission;
        }

        public void PlayDealIn(float delaySeconds, System.Action onComplete)
        {
            var renderers = BuildRendererArray();
            var targetColors = new[] { _bodyTint.Color, _iconTint.Color };
            StartCoroutine(DealInRoutine(renderers, targetColors, delaySeconds, onComplete));
        }

        private IEnumerator DealInRoutine(ITintable[] renderers, Color[] targetColors, float delay, System.Action onComplete)
        {
            yield return CardAnimator.ScaleAndFadeIn(transform, renderers, targetColors, delay, CardAnimator.DealInDuration);
            onComplete?.Invoke();
        }

        public void PlayTapAway(System.Action onComplete)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(TapAwayRoutine(onComplete));
        }

        private IEnumerator TapAwayRoutine(System.Action onComplete)
        {
            _emissionTint.Color = _highlightEmission;
            yield return new WaitForSeconds(CardAnimator.TapConfirmFlashDuration);
            _emissionTint.Color = Color.black;

            yield return CardAnimator.ScaleDownAndFadeOut(transform, BuildRendererArray(), CardAnimator.TapAwayDuration, onComplete);
        }

        public void PlayFadeInOnly()
        {
            transform.localScale = Vector3.one;
            var c = _bodyTint.Color; c.a = 1f; _bodyTint.Color = c;
            var ic = _iconTint.Color; ic.a = 1f; _iconTint.Color = ic;
        }

        public void PlayClearAndDestroy()
        {
            if (_clearCoroutine != null) StopCoroutine(_clearCoroutine);
            _clearCoroutine = StartCoroutine(
                CardAnimator.ScaleUpAndFadeOut(transform, BuildRendererArray(), () => Destroy(gameObject)));
        }

        public void PlayShake()
        {
            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            const float duration = 0.2f;
            float elapsed = 0f;
            _bodyTint.Color = Color.red;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float xOffset = Mathf.Sin(elapsed * 40f) * 0.1f;
                transform.localPosition = _originalLocalPos + new Vector3(xOffset, 0, 0);
                yield return null;
            }

            transform.localPosition = _originalLocalPos;
            RefreshCardColor(false);
        }

        private ITintable[] BuildRendererArray() => new ITintable[] { _bodyTint, _iconTint };
    }
}
