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
        [SerializeField] private Material _landingPuffMaterial;

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
                // Calculate physical bounds of the dark tray box, using a 95% margin 
                // to guarantee the sprite NEVER touches or crosses the edges.
                float boxWidth = (_bodyRenderer != null && _bodyRenderer.bounds.size.x > 0f) ? _bodyRenderer.bounds.size.x * 0.95f : 0.6f;
                float boxHeight = (_bodyRenderer != null && _bodyRenderer.bounds.size.y > 0f) ? _bodyRenderer.bounds.size.y * 0.95f : 0.8f;
                
                float spriteWidthUnits = tileSprite.bounds.size.x;
                float spriteHeightUnits = tileSprite.bounds.size.y;
                
                float parentWorldScaleX = (_foodAnchor != null && _foodAnchor.lossyScale.x > 0f) ? _foodAnchor.lossyScale.x : 1f;
                float parentWorldScaleY = (_foodAnchor != null && _foodAnchor.lossyScale.y > 0f) ? _foodAnchor.lossyScale.y : 1f;

                float scaleX = spriteWidthUnits > 0f ? (boxWidth / spriteWidthUnits / parentWorldScaleX) : 1f;
                float scaleY = spriteHeightUnits > 0f ? (boxHeight / spriteHeightUnits / parentWorldScaleY) : 1f;
                
                // Use independent X and Y scales so the tile perfectly fills the tray slot 
                // horizontally AND vertically, stretching slightly if needed to perfectly fit the hole.
                _iconRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);

                // PERFECT CENTERING: We set the icon to exactly the WORLD center of the mesh bounds.
                if (_bodyRenderer != null)
                {
                    _iconRenderer.transform.position = _bodyRenderer.bounds.center;
                    
                    // We push it slightly forward in Z so it doesn't clip into the box
                    _iconRenderer.transform.localPosition = new Vector3(
                        _iconRenderer.transform.localPosition.x, 
                        _iconRenderer.transform.localPosition.y, 
                        -0.1f
                    );
                }
                else 
                {
                    _iconRenderer.transform.localPosition = Vector3.zero;
                }
                
                // Note: We deliberately do NOT add any manual X/Y offsets here, 
                // because offsetting it risks pushing it outside the bounds of the box.
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
            PlayLandingPuff();
        }

        // Soft white puff at the instant a flown tile lands in its slot - gives
        // the landing a bit of weight/impact instead of the tile just quietly
        // arriving (reference: a similar mahjong game's tray-landing dust burst,
        // toned down to a light sparkle-free puff to match this game's calmer look).
        private const float LandingPuffLifetime = 0.4f;

        private void PlayLandingPuff()
        {
            if (_landingPuffMaterial == null) return;

            var go = new GameObject("LandingPuff");
            go.transform.SetParent(_foodAnchor != null ? _foodAnchor : transform, false);
            go.transform.localPosition = Vector3.zero;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = LandingPuffLifetime;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(LandingPuffLifetime * 0.7f, LandingPuffLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = Color.white;
            main.gravityModifier = 0f;
            main.maxParticles = 16;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)10, (short)16, 1, 0f) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 3f),
                new Keyframe(0.25f, 1f, 0f, 0f),
                new Keyframe(1f, 0f, -1.5f, 0f)));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.2f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var psRenderer = go.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = _landingPuffMaterial;
            psRenderer.sortingOrder = 20;

            ps.Play();
            Destroy(go, LandingPuffLifetime + 0.2f);
        }

        private IEnumerator PopInRoutine()
        {
            float duration = CardAnimator.TrayPopInDuration;
            float overshoot = CardAnimator.TrayPopInOvershoot;

            _foodAnchor.localScale = Vector3.zero;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = CardAnimator.EaseOutBack(t, overshoot);
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
