using System.Collections;
using GameClient.Presentation.Board;
using UnityEngine;

namespace GameClient.Presentation.HUD3D
{
    public sealed class TraySlotView3D : MonoBehaviour
    {
        [SerializeField] private Transform _content;
        [SerializeField] private MeshRenderer _bodyRenderer;
        [SerializeField] private Transform _foodAnchor;
        [SerializeField] private Color _highlightEmission = new Color(1f, 0.85f, 0.2f, 1f);
        // Empty slot shows the dark wood recess; a filled slot swaps to the ivory
        // tile face so a collected tile reads as a real white tile (like the
        // reference), not a symbol floating on wood.
        [SerializeField] private Material _emptyMaterial;
        [SerializeField] private Material _filledMaterial;

        private MeshRendererTint _bodyTint;
        private MeshRendererTint[] _iconTints = new MeshRendererTint[0];
        private MeshRendererTint _emissionTint;
        private Coroutine _clearCoroutine;
        private Coroutine _popInCoroutine;

        private void Awake()
        {
            _bodyTint = new MeshRendererTint(_bodyRenderer, "_BaseColor");
            _emissionTint = new MeshRendererTint(_bodyRenderer, "_EmissionColor");
        }

        public void SetEmpty()
        {
            _emissionTint.Color = Color.black;
            // Empty slot shows the warm recess (one of the tray's 4 visible parts).
            if (_bodyRenderer != null)
            {
                _bodyRenderer.enabled = true;
                if (_emptyMaterial != null)
                    _bodyRenderer.sharedMaterial = _emptyMaterial;
            }
            if (_foodAnchor != null)
            {
                for (int i = _foodAnchor.childCount - 1; i >= 0; i--)
                    Destroy(_foodAnchor.GetChild(i).gameObject);
            }
            _iconTints = new MeshRendererTint[0];
        }

        public void SetFilled(GameObject foodModelPrefab)
        {
            _emissionTint.Color = Color.black;
            if (_bodyRenderer != null)
            {
                _bodyRenderer.enabled = true; // show the ivory tile (was hidden while empty)
                if (_filledMaterial != null)
                    _bodyRenderer.sharedMaterial = _filledMaterial; // ivory tile face
            }
            _bodyTint.Color = Color.white; // clear any leftover fade from a prior clear animation
            if (_foodAnchor == null || foodModelPrefab == null) return;

            for (int i = _foodAnchor.childCount - 1; i >= 0; i--)
                Destroy(_foodAnchor.GetChild(i).gameObject);

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

        public void PlayPopIn(GameObject foodModelPrefab)
        {
            SetFilled(foodModelPrefab);
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
            var renderers = new ITintable[1 + _iconTints.Length];
            renderers[0] = _bodyTint;
            for (int i = 0; i < _iconTints.Length; i++)
                renderers[i + 1] = _iconTints[i];
            _clearCoroutine = StartCoroutine(CardAnimator.HighlightThenClear(
                _emissionTint, _highlightEmission, _content, renderers,
                () =>
                {
                    _content.localScale = Vector3.one;
                    SetEmpty();
                    onComplete?.Invoke();
                }));
        }
    }
}
