using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameClient.Presentation.Board;

namespace GameClient.Presentation.HUD3D
{
    public sealed class TraySlotView3D : MonoBehaviour
    {
        [SerializeField] private Transform _content;
        [SerializeField] private MeshRenderer _bodyRenderer;
        [SerializeField] private Transform _foodAnchor;
        [SerializeField] private Color _highlightEmission = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Material _emptyMaterial;
        [SerializeField] private Material _filledMaterial;
        [SerializeField] private TrailRenderer _flightTrail;

        private MeshRendererTint _bodyTint;
        private MeshRendererTint _emissionTint;
        private Coroutine _clearCoroutine;
        private Coroutine _popInCoroutine;

        private SpriteRenderer _iconRenderer;
        private SpriteRendererTint _iconTint;

        private void Awake()
        {
            EnsureTints();
        }

        private void OnDisable()
        {
            if (_content != null) _content.localScale = Vector3.one;
        }

        private void EnsureTints()
        {
            if (_bodyTint == null && _bodyRenderer != null) _bodyTint = new MeshRendererTint(_bodyRenderer, "_BaseColor");
            if (_emissionTint == null && _bodyRenderer != null) _emissionTint = new MeshRendererTint(_bodyRenderer, "_EmissionColor");
        }

        public void SetEmpty()
        {
            EnsureTints();
            if (_content != null) _content.localScale = Vector3.one;
            if (_foodAnchor != null) _foodAnchor.localScale = Vector3.one;
            if (_emissionTint != null) _emissionTint.Color = Color.black;
            
            if (_bodyRenderer != null)
            {
                _bodyRenderer.enabled = true;
                if (_emptyMaterial != null)
                    _bodyRenderer.sharedMaterial = _emptyMaterial;
            }
            if (_iconRenderer != null)
            {
                _iconRenderer.enabled = false;
            }
        }

        public void SetFilled(Sprite tileSprite)
        {
            EnsureTints();
            if (_emissionTint != null) _emissionTint.Color = Color.black;
            
            // Keep the recess visible behind the tile as the dark wooden box
            if (_bodyRenderer != null)
            {
                _bodyRenderer.enabled = true;
                if (_emptyMaterial != null)
                    _bodyRenderer.sharedMaterial = _emptyMaterial;
            }

            if (_iconRenderer == null)
            {
                var iconGO = new GameObject("Icon");
                iconGO.transform.SetParent(_foodAnchor, false);
                _iconRenderer = iconGO.AddComponent<SpriteRenderer>();
                _iconRenderer.sortingOrder = 100; 
                _iconTint = new SpriteRendererTint(_iconRenderer);
            }

            // Scale the icon to fill the tray slot box using the body mesh's actual bounds.
            // This avoids guessing world units and works regardless of how the prefab is scaled.
            if (tileSprite != null)
            {
                float boxHeight = (_bodyRenderer != null && _bodyRenderer.bounds.size.y > 0f)
                    ? _bodyRenderer.bounds.size.y * 0.85f  // 85% of box height = snug fit with tiny margin
                    : 0.7f;
                float spriteHeight = tileSprite.bounds.size.y;
                // Divide by the food anchor's lossy (world) Y scale so the local scale is correct
                float parentWorldScale = (_foodAnchor != null && _foodAnchor.lossyScale.y > 0f)
                    ? _foodAnchor.lossyScale.y
                    : 1f;
                float scaleFactor = spriteHeight > 0f ? (boxHeight / spriteHeight / parentWorldScale) : 1f;
                _iconRenderer.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
            }

            if (tileSprite != null)
            {
                _iconRenderer.sprite = tileSprite;
                _iconRenderer.enabled = true;
            }
            if (_iconTint != null) _iconTint.Color = Color.white;
        }

        public void SetFlightTrailEnabled(bool enabled)
        {
            if (_flightTrail == null) return;
            if (!enabled) _flightTrail.Clear();
            _flightTrail.emitting = enabled;
            _flightTrail.enabled = enabled;
        }

        public void PlayPopIn(Sprite tileSprite)
        {
            SetFilled(tileSprite);
            if (_popInCoroutine != null) StopCoroutine(_popInCoroutine);
            _popInCoroutine = StartCoroutine(PopInRoutine());
        }

        private IEnumerator PopInRoutine()
        {
            float duration = CardAnimator.TrayPopInDuration;
            float overshoot = CardAnimator.TrayPopInOvershoot;
            const float overshootFraction = 0.7f;

            _foodAnchor.localScale = Vector3.zero;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = t < overshootFraction
                    ? Mathf.Lerp(0f, overshoot, t / overshootFraction)
                    : Mathf.Lerp(overshoot, 1f, (t - overshootFraction) / (1f - overshootFraction));
                _foodAnchor.localScale = Vector3.one * scale;
                yield return null;
            }

            _foodAnchor.localScale = Vector3.one;
        }

        public void PlayHighlightThenClear(System.Action onComplete)
        {
            if (_clearCoroutine != null) StopCoroutine(_clearCoroutine);
            
            var renderers = new List<ITintable>();
            if (_iconTint != null) renderers.Add(_iconTint);
            
            _clearCoroutine = StartCoroutine(CardAnimator.HighlightThenClear(
                null, _highlightEmission, _foodAnchor, renderers.ToArray(),
                () =>
                {
                    _foodAnchor.localScale = Vector3.one;
                    _content.localScale = Vector3.one;
                    SetEmpty();
                    onComplete?.Invoke();
                }));
        }
    }
}
