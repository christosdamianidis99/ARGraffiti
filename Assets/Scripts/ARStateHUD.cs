using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARStateHUD : MonoBehaviour
{
    float _next;
    void OnGUI()
    {
        if (Time.time < _next) return; _next = Time.time + 0.2f;
        var state = ARSession.state;
        var msg = $"ARSession.state = {state}";
#if UNITY_ANDROID && !UNITY_EDITOR
        var cam = "android.permission.CAMERA";
        msg += $"\nCamera perm: {UnityEngine.Android.Permission.HasUserAuthorizedPermission(cam)}";
#endif
        GUI.Label(new Rect(10, 10, 800, 80), msg);
    }
}
