using System;
using System.Collections;
using UnityEngine;

namespace GameClient.Presentation.Board
{
    // Shared timing/easing for every card animation in the game. Board tiles
    // and tray slots both drive these coroutines through ITintable so a
    // clear, a highlight, a deal-in, or a flight can never feel different
    // between the board and the tray.
    public static class CardAnimator
    {
        public const float ClearDuration = 0.2f;
        public const float DealInDuration = 0.2f;
        public const float FlightDuration = 0.28f;
        public const float FastFadeDuration = 0.1f;
        public const float HighlightHoldDuration = 0.13f;

        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        public static IEnumerator ScaleAndFadeIn(
            Transform target, ITintable[] renderers, Color[] targetColors, float delay, float duration)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            target.localScale = Vector3.zero;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                var c = targetColors[i];
                c.a = 0f;
                renderers[i].Color = c;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOut(Mathf.Clamp01(elapsed / duration));
                target.localScale = Vector3.one * t;
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    var c = targetColors[i];
                    c.a = targetColors[i].a * t;
                    renderers[i].Color = c;
                }
                yield return null;
            }

            target.localScale = Vector3.one;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].Color = targetColors[i];
            }
        }

        public static IEnumerator ScaleUpAndFadeOut(Transform target, ITintable[] renderers, Action onComplete)
        {
            float elapsed = 0f;
            var startScale = target.localScale;
            var endScale = startScale * 1.15f;
            var startAlphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                startAlphas[i] = renderers[i] != null ? renderers[i].Color.a : 0f;

            while (elapsed < ClearDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / ClearDuration);
                target.localScale = Vector3.Lerp(startScale, endScale, t);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    var c = renderers[i].Color;
                    c.a = startAlphas[i] * (1f - t);
                    renderers[i].Color = c;
                }
                yield return null;
            }

            onComplete?.Invoke();
        }

        public static IEnumerator HighlightThenClear(
            ITintable glow, Color glowColor, Transform target, ITintable[] renderers, Action onComplete)
        {
            if (glow != null)
            {
                var c = glowColor;
                c.a = 1f;
                glow.Color = c;
            }

            yield return new WaitForSeconds(HighlightHoldDuration);

            yield return ScaleUpAndFadeOut(target, renderers, onComplete);
        }

        public static IEnumerator FadeAlpha(ITintable renderer, float fromAlpha, float toAlpha, float duration)
        {
            if (renderer == null) yield break;
            var c = renderer.Color;
            c.a = fromAlpha;
            renderer.Color = c;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                c.a = Mathf.Lerp(fromAlpha, toAlpha, t);
                renderer.Color = c;
                yield return null;
            }

            c.a = toAlpha;
            renderer.Color = c;
        }

        public static IEnumerator MoveRectTransform(
            RectTransform rect, Vector3 fromScreenPos, Vector3 toScreenPos, float duration)
        {
            rect.position = fromScreenPos;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOut(Mathf.Clamp01(elapsed / duration));
                rect.position = Vector3.Lerp(fromScreenPos, toScreenPos, t);
                yield return null;
            }
            rect.position = toScreenPos;
        }
    }
}
