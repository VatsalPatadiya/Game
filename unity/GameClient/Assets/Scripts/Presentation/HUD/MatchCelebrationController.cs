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
        private const float BurstDuration = 1f;
        private const float MinParticleLifetime = 0.8f;
        private const float MaxParticleLifetime = 1f;
        private const float MinStartSpeed = 2f;
        private const float MaxStartSpeed = 3.8f;
        private const float MinStartSize = 0.16f;
        private const float MaxStartSize = 0.24f;
        private const float GravityModifier = 1.2f;
        private const int MinBurstCount = 26;
        private const int MaxBurstCount = 34;
        private const float ConeAngleDegrees = 45f;
        private const float ConeRadius = 0.08f;

        // Sparkle accent: fewer, slightly bigger, brighter, and a touch
        // faster so they pop against the bulk of the glow particles.
        private const int MinSparkleCount = 5;
        private const int MaxSparkleCount = 8;
        private const float MinSparkleSize = 0.20f;
        private const float MaxSparkleSize = 0.30f;
        private const float MinSparkleSpeed = 2.6f;
        private const float MaxSparkleSpeed = 4.4f;

        public void PlayMatchCelebration(Vector3 worldPosition, bool isCombo)
        {
            SpawnBurst(worldPosition, _glowMaterial, MinBurstCount, MaxBurstCount,
                MinStartSize, MaxStartSize, MinStartSpeed, MaxStartSpeed);
            SpawnBurst(worldPosition, _sparkleMaterial, MinSparkleCount, MaxSparkleCount,
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
            main.startColor = new Color(1f, 1f, 0.98f, 1f);
            main.gravityModifier = GravityModifier;
            main.maxParticles = maxCount;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)minCount, (short)maxCount, 1, 0f) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = ConeAngleDegrees;
            shape.radius = ConeRadius;

            // Constant size for the whole lifetime (no shrink curve) - the
            // particles disappear via the alpha fade below instead, matching
            // the reference video's look of solid chunks that fade out rather
            // than shrinking to nothing.
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var alphaKeys = new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.7f),
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
            Destroy(go, BurstDuration + 0.5f);
        }
    }
}
