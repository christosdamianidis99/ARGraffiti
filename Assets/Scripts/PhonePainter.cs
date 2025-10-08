using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public enum BrushShape { Circle, Square }

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
    public BrushShape shape = BrushShape.Circle;
    public Color color = Color.red;

    [Header("State (read-only)")]
    public bool paintingActive;
    public ARPlane lockedPlane;

    // Strict border snapshot (local 2D polygon in plane space)
    Vector2[] _lockedBoundaryLocal;     // set on lock
    Transform _lockedPlaneTransform;    // cache transform

    // Stroke/material management
    public Transform strokesRoot;       // assign StrokesRoot (under XR Origin)
    Transform _anchorRoot;              // set by controller (ARAnchor.transform)
    Transform _strokeParent;            // parent for current stroke (child of anchor or strokesRoot)

    ARRaycastManager _rc;
    readonly List<ARRaycastHit> _hits = new();
    Vector3? _lastPos;

    // Base mats copied from prefabs (never mutated)
    Material _baseCircle, _baseSquare;
    // Current stroke material (changes when color/shape change)
    Material _strokeMat;
    bool _newStrokeOnNextDab = true; // force new stroke on start/color/shape

    void Awake()
    {
        _rc = GetComponent<ARRaycastManager>();

        if (brushDab_CirclePrefab)
        {
            var mr = brushDab_CirclePrefab.GetComponentInChildren<MeshRenderer>();
            if (mr) _baseCircle = new Material(mr.sharedMaterial);
        }
        if (brushDab_SquarePrefab)
        {
            var mr = brushDab_SquarePrefab.GetComponentInChildren<MeshRenderer>();
            if (mr) _baseSquare = new Material(mr.sharedMaterial);
        }
    }

    // --- Public API ---

    // Strict lock with polygon snapshot (controller passes boundary at selection time)
    public void LockToPlaneStrict(ARPlane plane, Vector2[] boundaryLocal, Transform anchorRoot)
    {
        lockedPlane = plane;
        _lockedBoundaryLocal = (boundaryLocal != null && boundaryLocal.Length >= 3) ? boundaryLocal : CopyBoundaryNow(plane);
        _lockedPlaneTransform = plane.transform;
        _anchorRoot = anchorRoot; // may be null; then we use strokesRoot directly
        _lastPos = null;
        _strokeParent = null;
        _newStrokeOnNextDab = true;
    }

    public void ClearLock()
    {
        lockedPlane = null;
        _lockedBoundaryLocal = null;
        _lockedPlaneTransform = null;
        _anchorRoot = null;
        _lastPos = null;
        _strokeParent = null;
        _strokeMat = null;
        _newStrokeOnNextDab = true;
    }

    public void StartPainting() { paintingActive = true; _lastPos = null; _newStrokeOnNextDab = true; }
    public void StopPainting() { paintingActive = false; _strokeParent = null; }

    public void SetBrushSize(float v) { brushSize = Mathf.Clamp(v, 0.02f, 0.2f); }
    public void SetShapeCircle() { shape = BrushShape.Circle; _newStrokeOnNextDab = true; }
    public void SetShapeSquare() { shape = BrushShape.Square; _newStrokeOnNextDab = true; }
    public void SetColor(Color c) { color = c; _newStrokeOnNextDab = true; }

    // --- Main loop ---

    void Update()
    {
        if (!paintingActive || lockedPlane == null) return;

        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (_rc.Raycast(center, _hits, TrackableType.PlaneWithinPolygon))
        {
            var hit = _hits[0];
            if (hit.trackableId != lockedPlane.trackableId) return;

            // STRICT BORDER: check inside initial polygon
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
                EnsureStrokeParentAndMaterial(); // creates new stroke & material if needed

                var prefab = (shape == BrushShape.Square) ? brushDab_SquarePrefab : brushDab_CirclePrefab;
                if (!prefab) return;

                var dab = Instantiate(prefab, pos, Quaternion.identity, _strokeParent);
                dab.transform.forward = n;
                dab.transform.localScale = Vector3.one * brushSize;

                var mr = dab.GetComponentInChildren<MeshRenderer>();
                if (mr && _strokeMat) mr.material = _strokeMat; // assign THIS stroke's mat

                _lastPos = pos;
            }
        }
    }

    // --- Internals ---

    void EnsureStrokeParentAndMaterial()
    {
        if (_strokeParent == null || _newStrokeOnNextDab)
        {
            // New stroke parent under anchor or strokesRoot
            var root = _anchorRoot != null ? _anchorRoot : strokesRoot;
            var go = new GameObject($"Stroke_{shape}_{ColorUtility.ToHtmlStringRGB(color)}");
            _strokeParent = go.transform;
            if (root) _strokeParent.SetParent(root, worldPositionStays: false);

            // New material instance for THIS stroke only
            var baseMat = (shape == BrushShape.Square) ? _baseSquare : _baseCircle;
            _strokeMat = baseMat ? new Material(baseMat) : null;
            if (_strokeMat) _strokeMat.color = color;

            _newStrokeOnNextDab = false;
        }
    }

    static Vector2[] CopyBoundaryNow(ARPlane plane)
    {
        var nat = plane.boundary;
        if (!nat.IsCreated || nat.Length < 3) return null;
        var arr = new Vector2[nat.Length];
        for (int i = 0; i < nat.Length; i++) arr[i] = nat[i];
        return arr;
    }

    // Standard point-in-polygon
    static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var pi = poly[i]; var pj = poly[j];
            bool intersect = ((pi.y > p.y) != (pj.y > p.y)) &&
                             (p.x < (pj.x - pi.x) * (p.y - pi.y) / ((pj.y - pi.y) + 1e-6f) + pi.x);
            if (intersect) inside = !inside;
        }
        return inside;
    }
}
