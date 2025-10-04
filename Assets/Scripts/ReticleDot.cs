using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class ReticleDot : MonoBehaviour
{
    public Image reticleUI;     // Canvas/Reticle (Raycast Target OFF)
    public ARPlane selectedPlane;

    public bool isOverAnyPlane { get; private set; }
    public ARPlane planeUnderReticle { get; private set; }
    public Pose lastHitPose { get; private set; }

    ARRaycastManager rc;
    readonly List<ARRaycastHit> hits = new();

    void Awake() { rc = GetComponent<ARRaycastManager>(); }

    void Update()
    {
        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        isOverAnyPlane = rc.Raycast(center, hits, TrackableType.PlaneWithinPolygon);
        if (isOverAnyPlane)
        {
            var h = hits[0];
            planeUnderReticle = h.trackable as ARPlane;
            lastHitPose = h.pose;

            if (reticleUI)
            {
                bool onSel = (selectedPlane && planeUnderReticle &&
                              planeUnderReticle.trackableId == selectedPlane.trackableId);
                reticleUI.color = onSel ? Color.cyan : Color.green;
            }
        }
        else
        {
            planeUnderReticle = null;
            if (reticleUI) reticleUI.color = Color.white;
        }
    }
}
