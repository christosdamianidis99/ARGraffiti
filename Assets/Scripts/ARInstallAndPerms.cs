using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARInstallAndPerms : MonoBehaviour
{
    IEnumerator Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var cam = "android.permission.CAMERA";
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(cam))
            UnityEngine.Android.Permission.RequestUserPermission(cam);
#endif
        if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
            yield return ARSession.CheckAvailability();
        if (ARSession.state == ARSessionState.NeedsInstall)
            yield return ARSession.Install();
    }
}
