// Assets/Scripts/PaintOnPlane.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class PaintOnPlane : MonoBehaviour
{
    [Header("Brush")]
    public GameObject brushDabPrefab;          // Assign BrushDab prefab
    [Range(0.02f, 0.12f)] public float brushSize = 0.06f; // meters (big for testing)
    [Range(0.005f, 0.1f)] public float spacing = 0.02f;   // meters between dabs
    [Tooltip("Lift the dab off the plane to avoid z-fighting (in meters).")]
    public float liftFromPlane = 0.01f;        // 1 cm

    private ARRaycastManager _raycaster;
    private readonly List<ARRaycastHit> _hits = new();
    private Vector3? _lastPos;
    private int _dabCount;

    void Awake()
    {
        _raycaster = GetComponent<ARRaycastManager>();
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 80, 900, 40),
            $"Dabs: {_dabCount}   Touch: {(Input.touchCount > 0 ? Input.GetTouch(0).phase.ToString() : "none")}");
    }

    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetMouseButton(0))
            TryPaint(Input.mousePosition);
#else
        if (Input.touchCount == 1)
            TryPaint(Input.GetTouch(0).position);
#endif
    }

    private void TryPaint(Vector2 screenPos)
    {
        // Raycast against detected plane polygons
        if (_raycaster.Raycast(screenPos, _hits, TrackableType.PlaneWithinPolygon))
        {
            var hit = _hits[0];
            var pose = hit.pose;

            // IMPORTANT PART:
            // 1) Position: lift slightly along plane normal to avoid z-fighting.
            // 2) Orientation: make the QUAD face OUT from the plane by aligning its FORWARD to plane normal.
            Vector3 planeNormal = pose.up;
            Vector3 spawnPos = pose.position + planeNormal * liftFromPlane;

            if (_lastPos == null || Vector3.Distance(_lastPos.Value, spawnPos) >= spacing)
            {
                var dab = Instantiate(brushDabPrefab, spawnPos, Quaternion.identity);
                // Face the plane
                dab.transform.forward = planeNormal;
                // Scale dab
                dab.transform.localScale = Vector3.one * brushSize;

                _lastPos = spawnPos;
                _dabCount++;
            }
        }
    }
}
