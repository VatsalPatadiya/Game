using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameClient.Presentation.HUD
{
    // Spawns the "you matched!" celebration seen in the reference footage: a
    // particle burst (world-space, rendered by the board camera so it can
    // cascade down over the board) plus tiered praise text (UI-space,
    // screen-anchored). Purely cosmetic - no score/XP/unlocks are touched
    // here, this only decides which label to show.
    public sealed class MatchCelebrationController : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Transform _canvasRoot;

        private static readonly string[] BaseTierMessages = { "Good Eye", "Nice", "Well Spotted" };
        private static readonly string[] ComboTierMessages = { "Perfect", "Great Streak", "Excellent" };
        private static readonly Color PraiseTextColor = new Color(0.82f, 0.68f, 0.35f, 1f); // gold/tan

        private const float ComboTextDelay = 0.18f;
        private const float BaseTextDuration = 0.9f;
        private const float ComboTextDuration = 1.1f;
        private const float ParticleDuration = 1.5f;

        public void PlayMatchCelebration(Vector3 trayScreenPosition, bool isCombo)
        {
            SpawnParticleBurst(trayScreenPosition);
            SpawnPraiseText(BaseTierMessages, trayScreenPosition, 24, BaseTextDuration, 40f);

            if (isCombo)
                StartCoroutine(SpawnComboTextDelayed());
        }

        private IEnumerator SpawnComboTextDelayed()
        {
            yield return new WaitForSeconds(ComboTextDelay);
            var centerScreen = new Vector3(Screen.width / 2f, Screen.height * 0.6f, 0f);
            SpawnPraiseText(ComboTierMessages, centerScreen, 40, ComboTextDuration, 60f);
        }

        private void SpawnParticleBurst(Vector3 screenPosition)
        {
            if (_camera == null) return;

            float depth = -_camera.transform.position.z;
            Vector3 worldPos = _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));

            var go = new GameObject("MatchParticleBurst");
            go.transform.position = worldPos;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = ParticleDuration;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new Color(1f, 1f, 0.98f, 1f);
            main.gravityModifier = 1.4f;
            main.maxParticles = 70;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40, 70, 1, 0f) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.1f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));

            var psRenderer = go.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = new Material(Shader.Find("Sprites/Default"));

            ps.Play();
            Destroy(go, ParticleDuration + 0.5f);
        }

        private void SpawnPraiseText(string[] pool, Vector3 screenPosition, int fontSize, float duration, float riseDistance)
        {
            string message = pool[Random.Range(0, pool.Length)];

            var go = new GameObject("PraiseText", typeof(Text));
            go.transform.SetParent(_canvasRoot, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = PraiseTextColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = message;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(400f, 80f);
            rect.position = screenPosition;

            StartCoroutine(RiseAndFade(rect, text, duration, riseDistance));
        }

        private IEnumerator RiseAndFade(RectTransform rect, Text text, float duration, float riseDistance)
        {
            Vector3 start = rect.position;
            Vector3 end = start + new Vector3(0f, riseDistance, 0f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rect.position = Vector3.Lerp(start, end, t);
                var c = text.color;
                c.a = 1f - t;
                text.color = c;
                yield return null;
            }

            Destroy(rect.gameObject);
        }
    }
}
