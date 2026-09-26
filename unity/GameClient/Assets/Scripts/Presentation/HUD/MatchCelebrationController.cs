using UnityEngine;

namespace GameClient.Presentation.HUD
{
    // Spawns a white particle burst at the tray slot when two tiles match
    // (reference: a screen recording of a similar mahjong game's "white
    // balls" celebration). Purely cosmetic - no score/XP/unlocks are touched
    // here. Two particle systems layer together: a bulk of soft round glow
    // dots (the reference's chunky burst) plus a handful of brighter 4-point
    // sparkle twinkles for a more premium accent.
    public sealed class MatchCelebrationController : MonoBehaviour
    {
        [SerializeField] private Material _glowMaterial;
        [SerializeField] private Material _sparkleMaterial;

        // Tuned from the reference video's burst: chunky, clearly separate
        // pieces rather than fine confetti dust - fewer, bigger particles read
        // better at real gameplay speed on a phone screen than many tiny ones.
        // Tuned for a full-screen magical firefly/fairy dust effect
        private const float BurstDuration = 2.5f;
        private const float MinParticleLifetime = 2.0f;
        private const float MaxParticleLifetime = 2.5f;
        private const float MinStartSpeed = 0.5f;
        private const float MaxStartSpeed = 2.0f;
        private const float MinStartSize = 0.04f;
        private const float MaxStartSize = 0.15f;
        private const float GravityModifier = -0.05f; // Float slightly upwards
        private const int MinBurstCount = 300;
        private const int MaxBurstCount = 450;

        // Sparkle accent
        private const int MinSparkleCount = 150;
        private const int MaxSparkleCount = 240;
        private const float MinSparkleSize = 0.08f;
        private const float MaxSparkleSize = 0.2f;
        private const float MinSparkleSpeed = 1.0f;
        private const float MaxSparkleSpeed = 3.0f;

        public void PlayMatchCelebration(Vector3 worldPosition, bool isCombo)
        {
            // Spawn the particles in front of the camera so they cover the whole screen
            Vector3 spawnPosition = Camera.main != null ? 
                Camera.main.transform.position + Camera.main.transform.forward * 10f : 
                worldPosition;

            SpawnBurst(spawnPosition, _glowMaterial, MinBurstCount, MaxBurstCount,
                MinStartSize, MaxStartSize, MinStartSpeed, MaxStartSpeed);
            SpawnBurst(spawnPosition, _glowMaterial, MinSparkleCount, MaxSparkleCount,
                MinSparkleSize, MaxSparkleSize, MinSparkleSpeed, MaxSparkleSpeed);
        }

        private void SpawnBurst(
            Vector3 worldPosition, Material material, int minCount, int maxCount,
            float minSize, float maxSize, float minSpeed, float maxSpeed)
        {
            if (material == null) return;

            var go = new GameObject("MatchParticleBurst");
            go.transform.position = worldPosition;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = BurstDuration;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(MinParticleLifetime, MaxParticleLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            
            // Plain White Glow
            var colorGradient = new Gradient();
            colorGradient.SetKeys(
                new[] { 
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(colorGradient) { mode = ParticleSystemGradientMode.RandomColor };
            main.gravityModifier = GravityModifier;
            main.maxParticles = maxCount;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)minCount, (short)maxCount, 1, 0f) });

            // Full screen box shape
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(15f, 25f, 5f); // Large enough to cover most phone screens at z=10

            // Noise for firefly floating effect
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.2f;

            // Premium Easing: Fade in and out softly
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 2f),
                new Keyframe(0.2f, 1f, 0f, 0f),
                new Keyframe(0.8f, 1f, 0f, 0f),
                new Keyframe(1f, 0f, -2f, 0f)
            ));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var alphaKeys = new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(1f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            };
            var colorKeys = new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            };
            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            colorOverLifetime.color = gradient;

            var psRenderer = go.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = material;

            ps.Play();
            Destroy(go, MaxParticleLifetime + 0.5f);
        }
    }
}
