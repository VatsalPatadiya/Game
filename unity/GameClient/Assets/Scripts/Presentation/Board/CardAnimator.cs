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
        // 120-150ms per spec (measured from actual gameplay footage); 130ms picked as the midpoint.
        public const float DealInDuration = 0.13f;
        public const float DealInFlyDuration = 0.15f;
        public const float FastFadeDuration = 0.1f;
        public const float HighlightHoldDuration = 0.13f;

        // Tap-to-tray timings, measured from footage: no in-transit frame was
        // catchable even at 10fps sampling, so the whole thing (flash + away)
        // must resolve in well under 150ms; the tray's own pop-in runs
        // concurrently on a separate, slightly longer/overshooting curve.
        public const float TapConfirmFlashDuration = 0.07f;
        public const float TapAwayDuration = 0.1f;
        public const float TrayFlightDuration = 0.22f;
        public const float TrayPopInDuration = 0.11f;
        public const float TrayPopInOvershoot = 1.08f;
        public const float UndoFlightDuration = 0.38f;
        // The tray's pop-in starts this fraction into the flight (not after it
        // lands), so the tail of the flight and the pop-in's overshoot read as
        // one continuous motion instead of two separate snaps.
        public const float TrayArrivalOverlapFraction = 0.7f;

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

        public static IEnumerator FlyAndFadeIn(
            Transform target, ITintable[] renderers, Color[] targetColors,
            Vector3 startLocalPos, Vector3 endLocalPos, float delay, float duration)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            target.localPosition = startLocalPos;
            target.localScale = Vector3.one;
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
                float raw = Mathf.Clamp01(elapsed / duration);
                // Ease-out cubic for smooth deceleration as tile settles
                float t = 1f - (1f - raw) * (1f - raw) * (1f - raw);
                target.localPosition = Vector3.Lerp(startLocalPos, endLocalPos, t);
                // Quick fade-in over the first 40% of the animation
                float alphaT = Mathf.Clamp01(raw / 0.4f);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    var c = targetColors[i];
                    c.a = targetColors[i].a * alphaT;
                    renderers[i].Color = c;
                }
                yield return null;
            }

            target.localPosition = endLocalPos;
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

        // Shrinks to nothing while fading, in place - distinct from
        // ScaleUpAndFadeOut (which grows slightly, used for the more
        // deliberate "these two matched" clear) - this is the quick "tapped
        // away" exit for a single tile heading to the tray.
        public static IEnumerator ScaleDownAndFadeOut(
            Transform target, ITintable[] renderers, float duration, Action onComplete)
        {
            float elapsed = 0f;
            var startScale = target.localScale;
            var startAlphas = new float[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                startAlphas[i] = renderers[i] != null ? renderers[i].Color.a : 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                target.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
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

        public static IEnumerator MoveTransform(
            Transform target, Vector3 fromWorldPos, Vector3 toWorldPos, float duration)
        {
            target.position = fromWorldPos;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOut(Mathf.Clamp01(elapsed / duration));
                target.position = Vector3.Lerp(fromWorldPos, toWorldPos, t);
                yield return null;
            }
            target.position = toWorldPos;
        }

        public static IEnumerator MoveTransformSmooth(
            Transform target, Vector3 fromWorldPos, Vector3 toWorldPos, Quaternion targetRot, float duration)
        {
            target.position = fromWorldPos;
            Quaternion fromRot = target.rotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float raw = Mathf.Clamp01(elapsed / duration);
                // Cubic smoothstep for organic acceleration and gentle deceleration
                float t = raw * raw * (3f - 2f * raw);
                Vector3 pos = Vector3.Lerp(fromWorldPos, toWorldPos, t);
                // Parabolic arc in Z (towards camera) so the tile cleanly hovers over board tiles
                pos.z -= Mathf.Sin(raw * Mathf.PI) * 0.75f;
                target.position = pos;
                target.rotation = Quaternion.Slerp(fromRot, targetRot, t);
                yield return null;
            }
            target.position = toWorldPos;
            target.rotation = targetRot;
        }
    }
}
