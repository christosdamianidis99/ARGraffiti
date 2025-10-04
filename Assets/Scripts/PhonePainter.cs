using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class PhonePainter : MonoBehaviour
{
    [Header("Brush Prefabs")]
    public GameObject brushDab_CirclePrefab;
    public GameObject brushDab_SquarePrefab;

    [Header("Brush Settings")]
    [Range(0.02f, 0.2f)] public float brushSize = 0.06f;
    [Range(0.005f, 0.1f)] public float spacing = 0.02f;
    public float liftFromPlane = 0.01f;
    public string shape = "Circle";
    public Color color = Color.red;

    [Header("State (read-only)")]
    public bool paintingActive;
    public ARPlane lockedPlane;

    // Strict border snapshot (local 2D polygon in plane space)
    Vector2[] _lockedBoundaryLocal;   // set on lock
    Transform _lockedPlaneTransform;  // cache transform for fast space conversion

    ARRaycastManager _rc;
    readonly List<ARRaycastHit> _hits = new();
    Vector3? _lastPos;
    Material _matCircle, _matSquare;
    Transform _strokeParent;          // parent for current stroke (under StrokesRoot if assigned)
    public Transform strokesRoot;     // optional, assign StrokesRoot here for organization

    void Awake()
    {
        _rc = GetComponent<ARRaycastManager>();
        if (brushDab_CirclePrefab)
        {
            var mr = brushDab_CirclePrefab.GetComponentInChildren<MeshRenderer>();
            if (mr) _matCircle = new Material(mr.sharedMaterial);
        }
        if (brushDab_SquarePrefab)
        {
            var mr = brushDab_SquarePrefab.GetComponentInChildren<MeshRenderer>();
            if (mr) _matSquare = new Material(mr.sharedMaterial);
        }
        ApplyColor();
    }

    // Called by controller with snapshot polygon
    public void LockToPlaneStrict(ARPlane plane, Vector2[] boundaryLocal)
    {
        lockedPlane = plane;
        _lockedPlaneTransform = plane.transform;
        _lockedBoundaryLocal = boundaryLocal; // may be null if boundary not ready
        _lastPos = null;
        _strokeParent = null;
    }

    // Backwards-compatible if not using strict polygon
    public void LockToPlane(ARPlane plane)
    {
        LockToPlaneStrict(plane, CopyBoundaryNow(plane));
    }

    public void ClearLock()
    {
        lockedPlane = null;
        _lockedPlaneTransform = null;
        _lockedBoundaryLocal = null;
        _lastPos = null;
        _strokeParent = null;
    }

    public void StartPainting() { paintingActive = true; _lastPos = null; }
    public void StopPainting() { paintingActive = false; _strokeParent = null; }

    public void SetBrushSize(float v) { brushSize = Mathf.Clamp(v, 0.02f, 0.2f); }
    public void SetShapeCircle() { shape = "Circle"; }
    public void SetShapeSquare() { shape = "Square"; }
    public void SetColor(Color c) { color = c; ApplyColor(); }
    void ApplyColor() { if (_matCircle) _matCircle.color = color; if (_matSquare) _matSquare.color = color; }

    void Update()
    {
        if (!paintingActive || lockedPlane == null) return;

        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (_rc.Raycast(center, _hits, TrackableType.PlaneWithinPolygon))
        {
            var hit = _hits[0];
            if (hit.trackableId != lockedPlane.trackableId) return;

            // STRICT BORDER: ensure hit is inside the snapshot polygon
            if (_lockedBoundaryLocal != null && _lockedBoundaryLocal.Length >= 3)
            {
                var local = _lockedPlaneTransform.InverseTransformPoint(hit.pose.position);
                var p2 = new Vector2(local.x, local.z);
                if (!PointInPolygon(p2, _lockedBoundaryLocal)) return;
            }

            var n = hit.pose.up;
            var pos = hit.pose.position + n * liftFromPlane;

            if (_lastPos == null || Vector3.Distance(_lastPos.Value, pos) >= spacing)
            {
                // Lazy-create parent for this stroke
                if (_strokeParent == null)
                {
                    var go = new GameObject("Stroke");
                    _strokeParent = go.transform;
                    if (strokesRoot) _strokeParent.SetParent(strokesRoot, worldPositionStays: false);
                }

                var prefab = (shape == "Square") ? brushDab_SquarePrefab : brushDab_CirclePrefab;
                if (!prefab) return;

                var dab = Instantiate(prefab, pos, Quaternion.identity, _strokeParent);
                dab.transform.forward = n;
                dab.transform.localScale = Vector3.one * brushSize;

                var mr = dab.GetComponentInChildren<MeshRenderer>();
                if (mr)
                {
                    if (shape == "Square" && _matSquare) mr.material = _matSquare;
                    else if (shape == "Circle" && _matCircle) mr.material = _matCircle;
                }
                _lastPos = pos;
            }
        }
    }

    // --- Utils ---
    static Vector2[] CopyBoundaryNow(ARPlane plane)
    {
        var nat = plane.boundary;
        if (!nat.IsCreated || nat.Length < 3) return null;
        var arr = new Vector2[nat.Length];
        for (int i = 0; i < nat.Length; i++) arr[i] = nat[i];
        return arr;
    }

    // Standard point-in-polygon (ray casting)
    static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var pi = poly[i]; var pj = poly[j];
            bool intersect = ((pi.y > p.y) != (pj.y > p.y)) &&
                             (p.x < (pj.x - pi.x) * (p.y - pi.y) / (pj.y - pi.y + 1e-6f) + pi.x);
            if (intersect) inside = !inside;
        }
        return inside;
    }
}
