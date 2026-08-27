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
        [SerializeField] private Transform _foodAnchor;
        [SerializeField] private Color _freeCardColor = new Color(0.969f, 0.957f, 0.922f, 1f);
        [SerializeField] private Color _blockedCardColor = new Color(0.62f, 0.63f, 0.58f, 1f);
        [SerializeField] private Color _highlightEmission = new Color(1f, 0.85f, 0.2f, 1f);

        private const float DragLiftDistance = 1.5f; // pulled toward the camera, in front of every layer
        private const float DragSnapBackDuration = 0.18f;

        private MeshRendererTint _bodyTint;
        private MeshRendererTint[] _iconTints = new MeshRendererTint[0];
        private MeshRendererTint _emissionTint;
        private Vector3 _originalLocalPos;
        private Coroutine _shakeCoroutine;
        private Coroutine _clearCoroutine;
        private Coroutine _fadeCoroutine;
        private Coroutine _dragSnapCoroutine;

        public string SlotId { get; private set; }
        public int Layer { get; private set; }

        // foodModelPrefab replaces the old flat icon+accentColor combo - each
        // tile value gets a distinct food mesh (see TileVisual.FoodModelFor)
        // instead of a shared quad retextured/tinted per value. A food model
        // can have several sub-meshes/renderers (e.g. a burger's bun/patty
        // parts), so every renderer under it gets its own MeshRendererTint -
        // BuildRendererArray folds them all in alongside the card body for
        // the shared fade animations in CardAnimator.
        public void Initialize(string slotId, int layer, GameObject foodModelPrefab)
        {
            SlotId = slotId;
            Layer = layer;

            _bodyTint = new MeshRendererTint(_bodyRenderer, "_BaseColor");
            _emissionTint = new MeshRendererTint(_bodyRenderer, "_EmissionColor");

            _originalLocalPos = transform.localPosition;
            transform.localScale = Vector3.one;

            _iconTints = new MeshRendererTint[0];
            if (_foodAnchor != null)
            {
                for (int i = _foodAnchor.childCount - 1; i >= 0; i--)
                    Destroy(_foodAnchor.GetChild(i).gameObject);

                if (foodModelPrefab != null)
                {
                    var foodInstance = Instantiate(foodModelPrefab, _foodAnchor);
                    foodInstance.transform.localPosition = Vector3.zero;
                    foodInstance.transform.localRotation = Quaternion.identity;

                    var renderers = foodInstance.GetComponentsInChildren<MeshRenderer>();
                    _iconTints = new MeshRendererTint[renderers.Length];
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        _iconTints[i] = new MeshRendererTint(renderers[i], "_BaseColor");
                        _iconTints[i].Color = Color.white;
                    }
                }
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
            var targetColors = new Color[renderers.Length];
            targetColors[0] = _bodyTint.Color;
            for (int i = 0; i < _iconTints.Length; i++)
                targetColors[i + 1] = _iconTints[i].Color;
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
            foreach (var tint in _iconTints)
            {
                var ic = tint.Color; ic.a = 1f; tint.Color = ic;
            }
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

        private ITintable[] BuildRendererArray()
        {
            var result = new ITintable[1 + _iconTints.Length];
            result[0] = _bodyTint;
            for (int i = 0; i < _iconTints.Length; i++)
                result[i + 1] = _iconTints[i];
            return result;
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
