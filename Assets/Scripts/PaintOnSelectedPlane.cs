using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class PaintOnSelectedPlane : MonoBehaviour
{
    [Header("Brush Prefabs")]
    public GameObject brushDab_CirclePrefab;
    public GameObject brushDab_SquarePrefab;

    [Header("Brush Settings")]
    [Range(0.02f, 0.2f)] public float brushSize = 0.06f;
    [Range(0.005f, 0.1f)] public float spacing = 0.02f;
    public float liftFromPlane = 0.01f; // 1 cm

    [Header("Runtime")]
    public Color currentColor = Color.red;
    public string currentShape = "Circle"; // "Circle" or "Square"

    ARRaycastManager _raycaster;
    readonly List<ARRaycastHit> _hits = new();
    Vector3? _lastPos;
    ARPlane _lockedPlane;
    Material _circleMatInst, _squareMatInst;

    void Awake()
    {
        _raycaster = GetComponent<ARRaycastManager>();
        if (brushDab_CirclePrefab)
        {
            var mr = brushDab_CirclePrefab.GetComponent<MeshRenderer>();
            if (mr) _circleMatInst = new Material(mr.sharedMaterial);
        }
        if (brushDab_SquarePrefab)
        {
            var mr = brushDab_SquarePrefab.GetComponent<MeshRenderer>();
            if (mr) _squareMatInst = new Material(mr.sharedMaterial);
        }
        ApplyColor();
    }

    public void RestrictToPlane(ARPlane plane)
    {
        _lockedPlane = plane;
        _lastPos = null;
    }
    public void ClearRestriction()
    {
        _lockedPlane = null;
        _lastPos = null;
    }
    public ARPlane GetLockedPlane() => _lockedPlane;

    public void SetBrushSize(float v) { brushSize = Mathf.Clamp(v, 0.02f, 0.2f); }
    public void SetShapeCircle() { currentShape = "Circle"; }
    public void SetShapeSquare() { currentShape = "Square"; }
    public void SetColor(Color c) { currentColor = c; ApplyColor(); }

    void ApplyColor()
    {
        if (_circleMatInst) _circleMatInst.color = currentColor;
        if (_squareMatInst) _squareMatInst.color = currentColor;
    }

    void Update()
    {
        if (_lockedPlane == null) return;
#if UNITY_EDITOR
        if (Input.GetMouseButton(0)) TryPaint(Input.mousePosition);
#else
        if (Input.touchCount == 1) TryPaint(Input.GetTouch(0).position);
#endif
    }

    void TryPaint(Vector2 screenPos)
    {
        if (_raycaster.Raycast(screenPos, _hits, TrackableType.PlaneWithinPolygon))
        {
            var hit = _hits[0];
            if (hit.trackableId != _lockedPlane.trackableId) return; // restrict to selected plane
            var pose = hit.pose;
            var normal = pose.up;
            var pos = pose.position + normal * liftFromPlane;

            if (_lastPos == null || Vector3.Distance(_lastPos.Value, pos) >= spacing)
            {
                var prefab = currentShape == "Square" ? brushDab_SquarePrefab : brushDab_CirclePrefab;
                if (!prefab) return;
                var dab = Instantiate(prefab, pos, Quaternion.identity);
                dab.transform.forward = normal;              // face out from plane
                dab.transform.localScale = Vector3.one * brushSize;

                var mr = dab.GetComponent<MeshRenderer>();
                if (mr)
                {
                    if (currentShape == "Square" && _squareMatInst) mr.material = _squareMatInst;
                    else if (currentShape == "Circle" && _circleMatInst) mr.material = _circleMatInst;
                }
                _lastPos = pos;
            }
        }
    }
}
