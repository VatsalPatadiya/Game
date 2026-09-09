using System.Collections;
using System.Collections.Generic;
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
        // Only populated on flight-card instances (see GameSceneBuilder3D's
        // BuildTraySlotPrefab) - disabled by default so the 4 static tray
        // slots never show one; TrayView3D enables it for the duration of a
        // flight to match the reference's motion-blur trail on a moving tile.
        [SerializeField] private TrailRenderer _flightTrail;

        private MeshRendererTint _bodyTint;
        private MeshRendererTint[] _iconTints = new MeshRendererTint[0];
        private MeshRendererTint _emissionTint;
        private Coroutine _clearCoroutine;
        private Coroutine _popInCoroutine;

        // Food-model instances are pooled per prefab (keyed by the shared
        // FoodModels[] reference from TileVisual.FoodModelFor - same tile
        // value always returns the same GameObject, so reference equality is
        // a reliable pool key) instead of Destroy+Instantiate on every fill.
        // A tile tap used to do 3 Instantiate + 2 Destroy calls of
        // mesh-renderer-bearing GameObjects within a ~220ms window (one
        // flight card + its food model, destroyed, then the destination
        // slot's food model instantiated again) - on the Galaxy A50 test
        // device that was the actual cause of the reported animation
        // "hitching", not the (already frame-rate-independent) easing math.
        private readonly Dictionary<GameObject, GameObject> _foodInstances = new Dictionary<GameObject, GameObject>();
        private GameObject _activeFoodInstance;

        private void Awake()
        {
            EnsureTints();
        }

        private void OnDisable()
        {
            if (_content != null) _content.localScale = Vector3.one;
        }

        // Lazily create the material tints. TrayView3D.Initialize instantiates a
        // slot and calls SetEmpty in the SAME frame; if the tray GameObject is
        // INACTIVE at that moment (e.g. a retry triggered from the pause menu,
        // which hid the HUD), Unity does NOT run the new slot's Awake yet, so the
        // tints would be null and SetEmpty threw a NullReferenceException -
        // aborting the slot's size reset and leaving it half-height (the reported
        // retry bug). Initializing here on first use makes the slot correct
        // regardless of activation order - one source of truth.
        private void EnsureTints()
        {
            if (_bodyTint == null) _bodyTint = new MeshRendererTint(_bodyRenderer, "_BaseColor");
            if (_emissionTint == null) _emissionTint = new MeshRendererTint(_bodyRenderer, "_EmissionColor");
        }

        public void SetEmpty()
        {
            EnsureTints();
            // Always restore full size: PopInRoutine animates _content.localScale,
            // and on a rebuild/retry a slot must never inherit a partial scale.
            if (_content != null) _content.localScale = Vector3.one;
            _emissionTint.Color = Color.black;
            // Empty slot shows the warm recess (one of the tray's 4 visible parts).
            if (_bodyRenderer != null)
            {
                _bodyRenderer.enabled = true;
                if (_emptyMaterial != null)
                    _bodyRenderer.sharedMaterial = _emptyMaterial;
            }
            if (_activeFoodInstance != null)
            {
                _activeFoodInstance.SetActive(false); // pooled, not destroyed - see _foodInstances
                _activeFoodInstance = null;
            }
            _iconTints = new MeshRendererTint[0];
        }

        public void SetFilled(GameObject foodModelPrefab)
        {
            EnsureTints();
            _emissionTint.Color = Color.black;
            if (_bodyRenderer != null)
            {
                _bodyRenderer.enabled = true; // show the ivory tile (was hidden while empty)
                if (_filledMaterial != null)
                    _bodyRenderer.sharedMaterial = _filledMaterial; // ivory tile face
            }
            _bodyTint.Color = Color.white; // clear any leftover fade from a prior clear animation
            if (_foodAnchor == null || foodModelPrefab == null) return;

            if (_activeFoodInstance != null) _activeFoodInstance.SetActive(false);

            if (!_foodInstances.TryGetValue(foodModelPrefab, out var foodInstance) || foodInstance == null)
            {
                foodInstance = Instantiate(foodModelPrefab, _foodAnchor);
                foodInstance.transform.localPosition = Vector3.zero;
                foodInstance.transform.localRotation = Quaternion.identity;
                _foodInstances[foodModelPrefab] = foodInstance;
            }
            foodInstance.SetActive(true);
            _activeFoodInstance = foodInstance;

            var renderers = foodInstance.GetComponentsInChildren<MeshRenderer>();
            _iconTints = new MeshRendererTint[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                _iconTints[i] = new MeshRendererTint(renderers[i], "_BaseColor");
                _iconTints[i].Color = Color.white;
            }
        }

        // Called by TrayView3D around a flight (tap-to-tray or reflow) so the
        // trail only ever renders while this instance is actually moving.
        public void SetFlightTrailEnabled(bool enabled)
        {
            if (_flightTrail == null) return;
            if (!enabled) _flightTrail.Clear(); // drop any residual points before disabling, so the next flight doesn't start with a stale trail
            _flightTrail.emitting = enabled;
            _flightTrail.enabled = enabled;
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
