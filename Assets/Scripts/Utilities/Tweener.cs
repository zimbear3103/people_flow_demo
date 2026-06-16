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
}
