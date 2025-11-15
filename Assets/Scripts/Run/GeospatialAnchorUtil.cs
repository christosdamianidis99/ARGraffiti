#if ARCORE_EXTENSIONS_PRESENT
using Google.XR.ARCoreExtensions;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public static class GeospatialAnchorUtil
{
    public static bool TryCreateAnchor(ARAnchorManager anchorManager, double lat, double lon, double alt, out ARGeospatialAnchor anchor)
    {
        anchor = null;
        var earthManager = Object.FindFirstObjectByType<AREarthManager>();
        if (earthManager == null || anchorManager == null) return false;

        var earthState = earthManager.EarthTrackingState;
        if (earthState != TrackingState.Tracking) return false;

        anchor = anchorManager.AddAnchor(lat, lon, alt, Quaternion.identity);
        return anchor != null;
    }
}
#endif
