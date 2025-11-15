using UnityEngine;

[ExecuteAlways, RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    Rect _last;
    RectTransform _rt;

    void OnEnable() { _rt = GetComponent<RectTransform>(); Apply(); }
    void Update() { Apply(); }

    void Apply()
    {
        if (!_rt) return;
        var sa = Screen.safeArea;
        if (sa == _last) return;
        _last = sa;

        var min = sa.position;
        var max = sa.position + sa.size;
        var size = new Vector2(Screen.width, Screen.height);

        _rt.anchorMin = new Vector2(min.x / size.x, min.y / size.y);
        _rt.anchorMax = new Vector2(max.x / size.x, max.y / size.y);
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;
    }
}
