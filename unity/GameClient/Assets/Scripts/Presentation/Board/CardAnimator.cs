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
        // Was 0.15s with ease-out-cubic - felt like a snap even after slowing
        // the door the same way. 0.22s + smoothstep (see FlyAndFadeIn) was
        // still too fast for a near-full-screen-width horizontal glide;
        // 0.35s reads as an unhurried slide instead of a dart across.
        public const float DealInFlyDuration = 0.35f;
        public const float FastFadeDuration = 0.1f;
        public const float HighlightHoldDuration = 0.13f;

        // Tap-to-tray timings, measured from footage: no in-transit frame was
        // catchable even at 10fps sampling, so the whole thing (flash + away)
        // must resolve in well under 150ms; the tray's own pop-in runs
        // concurrently on a separate, slightly longer/overshooting curve.
        public const float TapAwayDuration = 0.1f;
        public const float TrayFlightDuration = 0.22f;
        // Was 0.11s/1.08x on a plain two-segment lerp (0->overshoot, overshoot->1)
        // joined at a hard corner - a velocity discontinuity right at the peak
        // that reads as a mechanical snap instead of a spring settling. Now
        // driven by EaseOutBack (continuous velocity, zero at both ends) with a
        // slightly longer hold so the "give" is actually perceptible.
        public const float TrayPopInDuration = 0.16f;
        public const float TrayPopInOvershoot = 1.7f; // EaseOutBack strength (Penner's standard back constant); peaks around ~1.10x scale
        public const float UndoFlightDuration = 0.38f;
        // The tray's pop-in starts this fraction into the flight (not after it
        // lands), so the tail of the flight and the pop-in's overshoot read as
        // one continuous motion instead of two separate snaps.
        public const float TrayArrivalOverlapFraction = 0.7f;

        public static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        // Robert Penner's "back" ease: overshoots past 1 then settles, with
        // continuous velocity throughout (zero at t=0 and t=1) - unlike a
        // two-segment lerp-to-peak-then-back, there's no corner at the peak.
        public static float EaseOutBack(float t, float overshoot)
        {
            float c3 = overshoot + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + overshoot * x * x;
        }

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
                // Cubic smoothstep: gentle launch AND gentle settle, unlike
                // ease-out-cubic (peak velocity at raw=0) which read as an
                // abrupt snap off the edge even with the deceleration at the
                // other end - same fix as the level-start door slide.
                float t = raw * raw * (3f - 2f * raw);
                target.localPosition = Vector3.Lerp(startLocalPos, endLocalPos, t);
                // Fade-in over the first half of the animation
                float alphaT = Mathf.Clamp01(raw / 0.5f);
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
