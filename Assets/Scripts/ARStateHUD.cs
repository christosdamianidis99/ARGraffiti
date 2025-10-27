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
        msg += $"\nAndroid Camera: {UnityEngine.Android.Permission.HasUserAuthorizedPermission(cam)}";
#elif UNITY_IOS && !UNITY_EDITOR
        // On iOS, infer camera permission from ARSession state
        msg += $"\niOS Platform";
        if (ARSession.state == ARSessionState.Ready)
            msg += " (Camera OK)";
        else if (ARSession.state == ARSessionState.NotReady)
            msg += " (Camera?)";
#endif
        
        // Add AR availability info
        msg += $"\nAR Available: {ARSession.state != ARSessionState.Unsupported && ARSession.state != ARSessionState.None}";
        
        GUI.Label(new Rect(10, 10, 800, 80), msg);
    }
}
