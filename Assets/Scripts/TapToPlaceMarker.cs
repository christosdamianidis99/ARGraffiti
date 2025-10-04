using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class TapToPlaceMarker : MonoBehaviour
{
    public GameObject markerPrefab;

    private ARRaycastManager _raycastManager;
    private readonly List<ARRaycastHit> _hits = new();

    void Awake()
    {
        _raycastManager = GetComponent<ARRaycastManager>();
    }

    void Update()
    {
#if UNITY_EDITOR
        // Mouse support for Editor testing (simulated)
        if (Input.GetMouseButtonDown(0))
        {
            TryPlaceAt(Input.mousePosition);
        }
#else
        if (Input.touchCount == 1)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                TryPlaceAt(touch.position);
            }
        }
#endif
    }

    private void TryPlaceAt(Vector2 screenPos)
    {
        if (_raycastManager.Raycast(screenPos, _hits, TrackableType.PlaneWithinPolygon))
        {
            var hitPose = _hits[0].pose;

            // Instantiate or move a single marker instance
            if (_markerInstance == null)
            {
                _markerInstance = Instantiate(markerPrefab, hitPose.position, hitPose.rotation);
            }
            else
            {
                _markerInstance.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
            }

            // Align marker to the plane (optional: keep upright)
            var up = _hits[0].pose.up; // plane normal
            _markerInstance.transform.up = up;
        }
    }

    private GameObject _markerInstance;
}
