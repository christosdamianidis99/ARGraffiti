// Runtime/BootFade.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]
public class BootFade : MonoBehaviour
{
    public float delay = 0.1f, duration = 0.25f;
    IEnumerator Start()
    {
        var img = GetComponent<Image>();
        var c = img.color; c.a = 1f; img.color = c;
        yield return new WaitForSecondsRealtime(delay);
        float t = 0;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            c.a = 1f - Mathf.Clamp01(t / duration);
            img.color = c;
            yield return null;
        }
        gameObject.SetActive(false);
    }
}
