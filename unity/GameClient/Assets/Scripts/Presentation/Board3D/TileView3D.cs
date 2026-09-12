using System.Collections;
using GameClient.Presentation.Board;
using UnityEngine;

namespace GameClient.Presentation.Board3D
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TileView3D : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _bodyRenderer;
        [SerializeField] private BoxCollider _bodyCollider;
        
        [SerializeField] private Color _freeCardColor = Color.white;
        [SerializeField] private Color _blockedCardColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] private Color _highlightColor = new Color(1f, 0.9f, 0.6f, 1f);

        private const float DragLiftDistance = 1.5f;
        private const float DragSnapBackDuration = 0.18f;
        private const float SelectLift = 0.35f;

        private SpriteRendererTint _bodyTint;
        private Vector3 _originalLocalPos;
        private Transform _dropShadow;
        private Vector3 _shadowBaseScale;
        private Vector3 _shadowBasePos;

        private bool _isFree;
        private bool _isSelected;
        private Coroutine _shakeCoroutine;
        private Coroutine _clearCoroutine;
        private Coroutine _fadeCoroutine;
        private Coroutine _dragSnapCoroutine;

        public string SlotId { get; private set; }
        public int Layer { get; private set; }

        public void Initialize(string slotId, int layer, Sprite tileSprite)
        {
            SlotId = slotId;
            Layer = layer;

            _bodyTint = new SpriteRendererTint(_bodyRenderer);
            _bodyRenderer.sprite = tileSprite;
            _bodyRenderer.sortingOrder = layer;

            _originalLocalPos = transform.localPosition;
            transform.localScale = Vector3.one;
            _isSelected = false;

            if (_dropShadow == null)
            {
                _dropShadow = transform.Find("DropShadow");
                if (_dropShadow != null)
                {
                    _shadowBaseScale = _dropShadow.localScale;
                    _shadowBasePos = _dropShadow.localPosition;
                }
            }

            RefreshCardColor(true);
        }

        public void SetFree(bool isFree)
        {
            _isFree = isFree;
            RefreshCardColor(isFree);
        }

        private void RefreshCardColor(bool isFree)
        {
            if (_isSelected) return;
            if (_bodyTint != null) _bodyTint.Color = isFree ? _freeCardColor : _blockedCardColor;
        }

        public void Highlight()
        {
            if (_bodyTint != null) _bodyTint.Color = _highlightColor;
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (selected)
            {
                if (_bodyTint != null) _bodyTint.Color = _highlightColor;
            }
            else
            {
                RefreshCardColor(_isFree);
            }

            transform.localPosition = selected
                ? _originalLocalPos + new Vector3(0f, 0f, -SelectLift)
                : _originalLocalPos;
        }

        public void PlayDealIn(float delaySeconds, System.Action onComplete)
        {
            var renderers = new ITintable[] { _bodyTint };
            var targetColors = new Color[] { _bodyTint.Color };
            StartCoroutine(CardAnimator.ScaleAndFadeIn(transform, renderers, targetColors, delaySeconds, CardAnimator.DealInDuration));
            StartCoroutine(WaitAndInvoke(delaySeconds + CardAnimator.DealInDuration, onComplete));
        }
        
        private IEnumerator WaitAndInvoke(float delay, System.Action action)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        public void PlayTapAway(System.Action onComplete)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(TapAwayRoutine(onComplete));
        }

        private IEnumerator TapAwayRoutine(System.Action onComplete)
        {
            if (_bodyTint != null) _bodyTint.Color = _highlightColor;
            yield return new WaitForSeconds(CardAnimator.TapConfirmFlashDuration);
            yield return CardAnimator.ScaleDownAndFadeOut(transform, new ITintable[] { _bodyTint }, CardAnimator.TapAwayDuration, onComplete);
        }

        public void PlayFadeInOnly()
        {
            transform.localScale = Vector3.one;
            if (_bodyTint != null)
            {
                var c = _bodyTint.Color; c.a = 1f; _bodyTint.Color = c;
            }
        }

        public void PlayClearAndDestroy()
        {
            if (_clearCoroutine != null) StopCoroutine(_clearCoroutine);
            _clearCoroutine = StartCoroutine(
                CardAnimator.ScaleUpAndFadeOut(transform, new ITintable[] { _bodyTint }, () => Destroy(gameObject)));
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
            if (_bodyTint != null) _bodyTint.Color = Color.red;

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

        public void BeginDrag()
        {
            if (_dragSnapCoroutine != null)
            {
                StopCoroutine(_dragSnapCoroutine);
                _dragSnapCoroutine = null;
            }
        }

        public void UpdateDragOffset(Vector3 worldDeltaXY)
        {
            transform.localPosition = _originalLocalPos + new Vector3(worldDeltaXY.x, worldDeltaXY.y, -DragLiftDistance);
        }

        public void EndDrag()
        {
            if (_dragSnapCoroutine != null) StopCoroutine(_dragSnapCoroutine);
            _dragSnapCoroutine = StartCoroutine(SnapBackRoutine());
        }

        private IEnumerator SnapBackRoutine()
        {
            var start = transform.localPosition;
            float elapsed = 0f;
            while (elapsed < DragSnapBackDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / DragSnapBackDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                transform.localPosition = Vector3.Lerp(start, _originalLocalPos, eased);
                yield return null;
            }
            transform.localPosition = _originalLocalPos;
            _dragSnapCoroutine = null;
        }
    }
}
