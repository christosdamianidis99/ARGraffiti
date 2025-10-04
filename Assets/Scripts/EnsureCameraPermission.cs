// Assets/Scripts/EnsureCameraPermission.cs
using UnityEngine;

public class EnsureCameraPermission : MonoBehaviour
{
    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var cam = "android.permission.CAMERA";
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(cam))
        {
            UnityEngine.Android.Permission.RequestUserPermission(cam);
        }
#endif
    }
}
