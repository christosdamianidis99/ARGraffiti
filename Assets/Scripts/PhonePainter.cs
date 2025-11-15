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

    [Header("AR")]
    public ARPlane lockedPlane;
    ARRaycastManager _raycaster;
    readonly List<ARRaycastHit> _hits = new();

    [Header("Stroke Management")]
    public Transform strokesRoot;
    Transform _strokeParent;
    Material _strokeMat;
    bool _newStrokeOnNextDab = true;
    int _nextLayerIndex = 0;

    [Header("Rendering Layers")]
    public bool layeredStrokes = true;
    public float layerEpsilon = 0.0008f; // 0.8 mm
    Material _baseCircle, _baseSquare;

    [Header("Overwrite Erase")]
    public bool enableOverwriteErase = true;
    public float eraseRadius = 0.05f;
    Collider[] _overlapBuf = new Collider[100];
    int _paintMask = 0;

    [Header("State (read-only)")]
    public bool paintingActive;

    // --- Variables needed for AppStateControllerPhone ---
    Vector2[] _lockedBoundaryLocal;     // set on lock
    Transform _lockedPlaneTransform;    // cache transform
    Transform _anchorRoot;              // set by controller (ARAnchor.transform)
    Vector3? _lastPos;
    // --- End ---

    void Awake()
    {
        _raycaster = GetComponent<ARRaycastManager>();

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

    void Start()
    {
        _paintMask = LayerMask.GetMask("PaintDab");
    }


    // --- Public API ---

    // This is the SIMPLE lock method. It's okay, but not used by AppStateControllerPhone.
    public void LockToPlane(ARPlane plane)
    {
        lockedPlane = plane;
        _lockedPlaneTransform = plane ? plane.transform : null;
        _lastPos = null;
    }

    // *** THIS IS THE METHOD AppStateControllerPhone NEEDS ***
    // Strict lock with polygon snapshot (controller passes boundary at selection time)
    public void LockToPlaneStrict(ARPlane plane, Vector2[] boundaryLocal, Transform anchorRoot)
    {
        lockedPlane = plane;
        _lockedBoundaryLocal = (boundaryLocal != null && boundaryLocal.Length >= 3) ? boundaryLocal : CopyBoundaryNow(plane);
        _lockedPlaneTransform = plane ? plane.transform : null; // Cache transform
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
        if (!paintingActive || lockedPlane == null || _lockedPlaneTransform == null) return;

        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (_raycaster.Raycast(center, _hits, TrackableType.PlaneWithinPolygon))
        {
            var hit = _hits[0];
            if (hit.trackableId != lockedPlane.trackableId)
            {
                _lastPos = null; // We are off the locked plane
                return;
            }

            // *** ADDED THIS BOUNDARY CHECK ***
            // STRICT BORDER: check inside initial polygon
            if (_lockedBoundaryLocal != null && _lockedBoundaryLocal.Length >= 3)
            {
                var local = _lockedPlaneTransform.InverseTransformPoint(hit.pose.position);
                var p2 = new Vector2(local.x, local.z);
                if (!PointInPolygon(p2, _lockedBoundaryLocal))
                {
                    _lastPos = null; // Prevent jumping lines if user goes off-surface and back
                    return;
                }
            }
            // *** END BOUNDARY CHECK ***

            // We have a valid hit on our plane, proceed
            TryPaint(hit);
        }
        else
        {
            _lastPos = null; // We are not hitting the plane
        }
    }


    // --- Internals ---

    void TryPaint(ARRaycastHit hit)
    {
        var pose = hit.pose;
        var normal = pose.up;
        float lift = layeredStrokes ? (liftFromPlane + _nextLayerIndex * layerEpsilon) : liftFromPlane;
        var pos = pose.position + normal * lift;

        if (enableOverwriteErase)
        {
            TryErase(pos); // Erase first
        }

        if (_lastPos == null || Vector3.Distance(_lastPos.Value, pos) >= spacing)
        {
            EnsureStrokeParentAndMaterial();

            var prefab = (shape == BrushShape.Square) ? brushDab_SquarePrefab : brushDab_CirclePrefab;
            if (!prefab) return;

            var dab = Instantiate(prefab, pos, Quaternion.identity, _strokeParent);
            dab.transform.forward = normal;
            dab.transform.localScale = Vector3.one * brushSize;

            var mr = dab.GetComponentInChildren<MeshRenderer>();
            if (mr && _strokeMat) mr.material = _strokeMat;

            _lastPos = pos;
        }
    }

    void TryErase(Vector3 worldPos)
    {
        if (!enableOverwriteErase || _paintMask == 0) return;
        RemoveUnderlyingDabs(worldPos, eraseRadius);
    }

    void EnsureStrokeParentAndMaterial()
    {
        if (_strokeParent == null || _newStrokeOnNextDab)
        {
            var root = _anchorRoot != null ? _anchorRoot : strokesRoot;
            var go = new GameObject($"Stroke_{shape}_{ColorUtility.ToHtmlStringRGB(color)}");
            _strokeParent = go.transform;
            if (root) _strokeParent.SetParent(root, worldPositionStays: false);

            var baseMat = (shape == BrushShape.Square) ? _baseSquare : _baseCircle;
            _strokeMat = baseMat ? new Material(baseMat) : null;
            if (_strokeMat)
            {
                _strokeMat.color = color;
                int rq = _strokeMat.renderQueue + (layeredStrokes ? _nextLayerIndex : 0);
                rq = Mathf.Clamp(rq + _nextLayerIndex, 0, 5000); // 5000 is max queue
                _strokeMat.renderQueue = rq;
            }

            var meta = go.AddComponent<StrokeMeta>();
            meta.layerIndex = layeredStrokes ? _nextLayerIndex : 0;
            meta.liftOffset = layeredStrokes ? (liftFromPlane + meta.layerIndex * layerEpsilon) : liftFromPlane;
            meta.strokeMaterial = _strokeMat;

            if (layeredStrokes) _nextLayerIndex++;
            _newStrokeOnNextDab = false;
        }
    }

    void RemoveUnderlyingDabs(Vector3 worldPos, float radius)
    {
        if (_paintMask == 0) return;

        int count = Physics.OverlapSphereNonAlloc(worldPos, radius, _overlapBuf, _paintMask, QueryTriggerInteraction.Collide);
        if (count <= 0) return;

        var curStroke = _strokeParent ? _strokeParent.GetComponent<StrokeMeta>() : null;
        int curLayer = curStroke ? curStroke.layerIndex : int.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var h = _overlapBuf[i];
            if (!h) continue;

            var go = h.transform.gameObject;
            if (_strokeParent && go.transform.parent == _strokeParent) continue;

            var otherStroke = go.transform.parent ? go.transform.parent.GetComponent<StrokeMeta>() : null;
            int otherLayer = otherStroke ? otherStroke.layerIndex : -1;

            if (otherLayer < curLayer)
            {
                Destroy(go);
            }
        }
    }

    // *** HELPER METHODS NEEDED BY AppStateControllerPhone ***
    static Vector2[] CopyBoundaryNow(ARPlane plane)
    {
        if (plane == null) return null;
        var nat = plane.boundary;
        if (!nat.IsCreated || nat.Length < 3) return null;
        var arr = new Vector2[nat.Length];
        for (int i = 0; i < nat.Length; i++) arr[i] = nat[i];
        return arr;
    }

    static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        if (poly == null) return false;
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