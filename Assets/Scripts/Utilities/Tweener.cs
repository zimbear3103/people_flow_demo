using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Tweener : MonoBehaviour
{
    #region IENumerator
    public static IEnumerator IE_LocalTranslate(Transform obj, Vector3 start, Vector3 end, float duration, System.Action callbacks = null)
    {
        float t = 0;
        while (t < duration)
        {
            if (obj == null)
                yield break;

            obj.localPosition = Vector3.Lerp(start, end, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        obj.localPosition = end;
        callbacks?.Invoke();
    }
    public static IEnumerator IE_GlobalTranslate(Transform obj, Vector3 start, Vector3 end, float duration, System.Action callbacks = null)
    {
        float t = 0;
        while (t < duration)
        {
            if (obj == null)
                yield break;

            obj.position = Vector3.Lerp(start, end, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        obj.position = end;
        callbacks?.Invoke();
    }
    public static IEnumerator IE_LocalRotate(Transform obj, Vector3 start, Vector3 end, float duration, System.Action callbacks = null)
    {
        float t = 0;
        while (t < duration)
        {
            if (obj == null)
                yield break;

            obj.localEulerAngles = Vector3.Lerp(start, end, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        obj.localEulerAngles = end;
        callbacks?.Invoke();
    }
    public static IEnumerator IE_GlobalRotate(Transform obj, Vector3 start, Vector3 end, float duration, System.Action callbacks = null)
    {
        float t = 0;
        while (t < duration)
        {
            if (obj == null)
                yield break;

            obj.eulerAngles = Vector3.Lerp(start, end, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        obj.eulerAngles = end;
        callbacks?.Invoke();
    }
    public static IEnumerator IE_LocalRotate(Transform obj, Quaternion start, Quaternion end, float duration, System.Action callbacks = null)
    {
        float t = 0;
        while (t < duration)
        {
            if (obj == null)
                yield break;

            obj.localRotation = Quaternion.Lerp(start, end, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        obj.localRotation = end;
        callbacks?.Invoke();
    }
    public static IEnumerator IE_GlobalRotate(Transform obj, Quaternion start, Quaternion end, float duration, System.Action callbacks = null)
    {
        float t = 0;
        while (t < duration)
        {
            if (obj == null)
                yield break;

            obj.rotation = Quaternion.Lerp(start, end, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        obj.rotation = end;
        callbacks?.Invoke();
    }
    public static IEnumerator IE_LocalScale(Transform obj, Vector3 start, Vector3 end, float duration, System.Action callbacks = null)
    {
        float t = 0;
        while (t < duration)
        {
            if (obj == null)
                yield break;

            obj.localScale = Vector3.Lerp(start, end, t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        obj.localScale = end;
        callbacks?.Invoke();
    }
    public static IEnumerator IE_TransparencyImage(Image image, float start, float end, float duration, System.Action callbacks = null)
    {
        float t = 0;
        while (t < duration)
        {
            if (image == null)
                yield break;

            image.color = Color.Lerp(new Color(image.color.r, image.color.g, image.color.b, start),
                                     new Color(image.color.r, image.color.g, image.color.b, end),
                                     t / duration);
            t += Time.deltaTime;
            yield return null;
        }
        image.color = new Color(image.color.r, image.color.g, image.color.b, end);
        callbacks?.Invoke();
    }
    public static IEnumerator IE_DelayForAction(float delay, System.Action callback)
    {
        float t = 0;
        while (t < delay)
        {
            t += Time.deltaTime;
            yield return null;
        }

        callback?.Invoke();
    }
    public static IEnumerator IE_DelayForAction(System.Func<bool> condition, System.Action callback)
    {
        yield return new WaitUntil(condition);

        callback?.Invoke();
    }
    #endregion

    #region Merged from TweenUtil
    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = 1.70158f + 1f;
        t -= 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    public static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
    public static float EaseInQuad(float t) => t * t;
    public static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

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

    public static IEnumerator ScaleOut(Transform tr, float duration, System.Action onDone)
    {
        if (tr == null) { onDone?.Invoke(); yield break; }
        Vector3 from = tr.localScale;
        float e = 0f;
        while (e < duration && tr != null)
        {
            e += Time.deltaTime;
            float k = EaseInQuad(Mathf.Clamp01(e / duration));
            tr.localScale = from * (1f - k);
            yield return null;
        }
        if (tr != null) tr.localScale = Vector3.zero;
        onDone?.Invoke();
    }

    public static IEnumerator HopInto(Transform tr, Vector3 to, float height, float duration, System.Action onDone)
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
            p.y += height * Mathf.Sin(k * Mathf.PI);
            tr.position = p;
            tr.localScale = startScale * (1f - EaseInQuad(k));
            yield return null;
        }
        onDone?.Invoke();
    }

    public static IEnumerator HopArc(Transform tr, Vector3 to, float height, float duration, System.Action onDone)
    {
        if (tr == null) { onDone?.Invoke(); yield break; }
        Vector3 from = tr.position;
        float e = 0f;
        while (e < duration && tr != null)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / duration);
            Vector3 p = Vector3.Lerp(from, to, EaseInOutQuad(k));
            p.y += height * Mathf.Sin(k * Mathf.PI);
            tr.position = p;
            yield return null;
        }
        if (tr != null) tr.position = to;
        onDone?.Invoke();
    }
    #endregion
}

