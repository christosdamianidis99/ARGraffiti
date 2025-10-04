using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
[RequireComponent(typeof(ARPlaneManager))]
public class SurfaceSelector : MonoBehaviour
{
    public Action<ARPlane> OnPlaneChosen;
    ARRaycastManager _raycaster;
    ARPlaneManager _planes;
    readonly List<ARRaycastHit> _hits = new();
    bool _enabled;

    void Awake()
    {
        _raycaster = GetComponent<ARRaycastManager>();
        _planes = GetComponent<ARPlaneManager>();
    }

    public void EnableSelection(bool on) => _enabled = on;

    void Update()
    {
        if (!_enabled) return;
#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0)) TryPick(Input.mousePosition);
#else
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began)
            TryPick(Input.GetTouch(0).position);
#endif
    }

    void TryPick(Vector2 screenPos)
    {
        if (_raycaster.Raycast(screenPos, _hits, TrackableType.PlaneWithinPolygon))
        {
            var plane = _hits[0].trackable as ARPlane;
            if (plane != null) OnPlaneChosen?.Invoke(plane);
        }
    }
}
