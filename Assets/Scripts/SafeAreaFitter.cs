// Runtime/SafeAreaFitter.cs
using UnityEngine;

[ExecuteAlways]
public class SafeAreaFitter : MonoBehaviour
{
    Rect last; RectTransform rt;
    void OnEnable() { rt = GetComponent<RectTransform>(); Apply(); }
    void Update() { if (Screen.safeArea != last) Apply(); }
    void Apply()
    {
        last = Screen.safeArea;
        var min = last.position; var max = last.position + last.size;
        min.x /= Screen.width; min.y /= Screen.height;
        max.x /= Screen.width; max.y /= Screen.height;
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
