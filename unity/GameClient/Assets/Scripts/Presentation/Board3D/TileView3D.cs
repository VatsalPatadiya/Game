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
        private Coroutine _highlightCoroutine;

        public string SlotId { get; private set; }
        public int Layer { get; private set; }

        public void Initialize(string slotId, int layer, Sprite tileSprite)
        {
            SlotId = slotId;
            Layer = layer;

            _bodyTint = new SpriteRendererTint(_bodyRenderer);
            _bodyRenderer.sprite = tileSprite;
            // Initial order, will be correctly set by BoardView after placement
            _bodyRenderer.sortingOrder = layer * 10000;

            if (tileSprite != null)
            {
                // Dynamically scale the sprite so its world width is precisely 0.626f.
                // This perfectly matches the _cellWidth logic in BoardView3D and prevents 
                // the horizontal overlapping seen when sprites are naturally too wide.
                float targetWidth = 0.626f;
                float spriteWidthUnits = tileSprite.bounds.size.x;
                float scale = spriteWidthUnits > 0 ? (targetWidth / spriteWidthUnits) : 1f;
                _bodyRenderer.transform.localScale = new Vector3(scale, scale, 1f);
            }

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

        // Plays a single pulsing glow for ~2.5 seconds then returns to normal.
        // Cancels any in-progress highlight before starting a new one.
        public void Highlight()
        {
            if (_highlightCoroutine != null) StopCoroutine(_highlightCoroutine);
            _highlightCoroutine = StartCoroutine(HintGlowRoutine());
        }

        private const float HintGlowDuration = 2.5f;

        private IEnumerator HintGlowRoutine()
        {
            Color baseColor = _isFree ? _freeCardColor : _blockedCardColor;
            const float rampUpTime   = 0.35f;
            const float holdTime     = 1.6f;
            const float rampDownTime = 0.55f;

            // Ramp up to glow color
            float elapsed = 0f;
            while (elapsed < rampUpTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rampUpTime);
                if (_bodyTint != null) _bodyTint.Color = Color.Lerp(baseColor, _highlightColor, t);
                yield return null;
            }

            if (_bodyTint != null) _bodyTint.Color = _highlightColor;

            // Hold at full glow
            yield return new WaitForSeconds(holdTime);

            // Ramp back down to normal
            elapsed = 0f;
            while (elapsed < rampDownTime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / rampDownTime);
                if (_bodyTint != null) _bodyTint.Color = Color.Lerp(_highlightColor, baseColor, t);
                yield return null;
            }

            _highlightCoroutine = null;
            RefreshCardColor(_isFree);
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (selected)
            {
                if (_bodyTint != null) _bodyTint.Color = _highlightColor;
                if (_bodyRenderer != null) _bodyRenderer.sortingOrder = 32000; // Bring to front while selected
            }
            else
            {
                RefreshCardColor(_isFree);
                UpdateSortingOrder();
            }

            transform.localPosition = selected
                ? _originalLocalPos + new Vector3(0f, 0f, -SelectLift)
                : _originalLocalPos;
        }

        public void UpdateSortingOrder()
        {
            // layer * 10000 ensures higher layers always render in front.
            // -localPosition.y * 100 ensures tiles lower on the screen render in front of tiles higher up.
            int order = (Layer * 10000) - Mathf.RoundToInt(_originalLocalPos.y * 100f);
            if (_bodyRenderer != null) _bodyRenderer.sortingOrder = order;
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
            const float duration = 0.25f;
            float elapsed = 0f;
            Color softRed = new Color(1.0f, 0.6f, 0.6f, 1f);
            
            if (_bodyTint != null) _bodyTint.Color = softRed;
            Quaternion originalRot = transform.localRotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float zAngle = Mathf.Sin(elapsed * 35f) * 6f * (1f - (elapsed / duration));
                transform.localRotation = originalRot * Quaternion.Euler(0, 0, zAngle);
                yield return null;
            }

            transform.localRotation = originalRot;
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
