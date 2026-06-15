using System;
using System.Collections;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Tiny coroutine-based tween helpers (the project has no DOTween). Provides the easings and
    /// the two motions this game needs: a "pop" spawn scale and an arc hop into a hole.
    /// </summary>
    public static class TweenUtil
    {
        // ---- easings --------------------------------------------------------
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = 1.70158f + 1f;
            t -= 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float EaseInQuad(float t) => t * t;
        public static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        /// <summary>Pop a transform from 0 → target scale with an over-shoot, on spawn.</summary>
        public static IEnumerator ScalePop(Transform tr, Vector3 target, float duration = 0.25f)
        {
            if (tr == null) yield break;
            float e = 0f;
            tr.localScale = Vector3.zero;
            while (e < duration && tr != null)
            {
                e += Time.deltaTime;
                float k = EaseOutBack(Mathf.Clamp01(e / duration));
                tr.localScale = target * k;
                yield return null;
            }
            if (tr != null) tr.localScale = target;
        }

        /// <summary>
        /// Move a transform from its current position to <paramref name="to"/> along a parabolic
        /// arc of the given peak height, shrinking to zero scale by the end (the "jump in" hop).
        /// </summary>
        public static IEnumerator HopInto(Transform tr, Vector3 to, float height, float duration, Action onDone)
        {
            if (tr == null) { onDone?.Invoke(); yield break; }
            Vector3 from = tr.position;
            Vector3 startScale = tr.localScale;
            float e = 0f;
            while (e < duration && tr != null)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / duration);
                Vector3 p = Vector3.Lerp(from, to, EaseInOutQuad(k));
                p.y += height * Mathf.Sin(k * Mathf.PI); // parabola: 0 → peak → 0
                tr.position = p;
                tr.localScale = startScale * (1f - EaseInQuad(k)); // shrink as it drops in
                yield return null;
            }
            onDone?.Invoke();
        }
    }
}
