using System.Collections;
using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Tiny coroutine-based "juice" helpers, mirroring the project's existing
    /// <c>Tweener</c> convention (the codebase ships NO DOTween/Cinemachine — see CLAUDE.md).
    /// Each routine is a static IEnumerator you StartCoroutine on the owning MonoBehaviour.
    /// The DOTween one-liner each effect maps to is noted in a comment, so this can be
    /// swapped for DOTween later without changing call sites.
    /// </summary>
    public static class JuiceTween
    {
        /// <summary>
        /// Scale punch: pops up to baseScale*(1+punch) and eases back, in a single pass.
        /// DOTween equivalent: transform.DOPunchScale(Vector3.one * punch, duration).
        /// </summary>
        public static IEnumerator IE_ScalePunch(Transform target, Vector3 baseScale, float punch, float duration)
        {
            if (target == null || duration <= 0f)
                yield break;

            float t = 0f;
            while (t < duration)
            {
                if (target == null)
                    yield break;

                // sin(0..pi) → 0..1..0 gives a clean up-and-back with no second allocation.
                float k = Mathf.Sin(Mathf.PI * (t / duration));
                target.localScale = baseScale * (1f + punch * k);
                t += Time.deltaTime;
                yield return null;
            }

            target.localScale = baseScale;
        }

        /// <summary>
        /// Vertical "catch" bounce: dips local Y down by <paramref name="dip"/> and returns,
        /// giving the container a sense of weight when it catches a character.
        /// DOTween equivalent: transform.DOPunchPosition(Vector3.down * dip, duration).
        /// </summary>
        public static IEnumerator IE_DipBounce(Transform target, Vector3 baseLocalPos, float dip, float duration)
        {
            if (target == null || duration <= 0f)
                yield break;

            float t = 0f;
            while (t < duration)
            {
                if (target == null)
                    yield break;

                float k = Mathf.Sin(Mathf.PI * (t / duration));
                Vector3 p = baseLocalPos;
                p.y -= dip * k;
                target.localPosition = p;
                t += Time.deltaTime;
                yield return null;
            }

            target.localPosition = baseLocalPos;
        }
    }
}
