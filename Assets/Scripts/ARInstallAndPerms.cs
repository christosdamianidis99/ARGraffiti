using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARInstallAndPerms : MonoBehaviour
{
    IEnumerator Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        var cam = "android.permission.CAMERA";
        // Request permission once and wait for a response so ARCore never starts without a camera.
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(cam))
        {
            UnityEngine.Android.Permission.RequestUserPermission(cam);
            // Wait until the user responds; bail out if still denied.
            while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(cam))
            {
                // A short delay avoids busy-waiting on the UI thread.
                yield return new WaitForSeconds(0.1f);
                // If the request was answered and still denied, stop early to prevent ARCore errors.
                if (UnityEngine.Android.Permission.ShouldShowRequestPermissionRationale(cam) == false)
                    break;
            }

            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(cam))
            {
                Debug.LogError("[ARInstallAndPerms] Camera permission denied. AR session will not start.");
                yield break;
            }
        }
#endif

        if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
            yield return ARSession.CheckAvailability();
        if (ARSession.state == ARSessionState.NeedsInstall)
            yield return ARSession.Install();
    }
}
