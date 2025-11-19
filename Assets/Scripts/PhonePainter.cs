using System;
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
    [Range(0.02f, 0.2f)] public float brushSize = 0.04f;
    [Range(0.005f, 0.1f)] public float spacing = 0.02f;
    public float liftFromPlane = 0.01f;
    public BrushShape shape = BrushShape.Circle;
    public Color color = Color.red;

    [Header("AR")]
    public ARPlane lockedPlane;
    ARRaycastManager _raycaster;
    readonly List<ARRaycastHit> _hits = new();
    Vector2[] _lockedBoundaryLocal;
    Transform _lockedPlaneTransform;
    Transform _anchorRoot;

    [Header("Stroke Management")]
    public Transform strokesRoot;
    Transform _strokeParent;
    Material _strokeMat;
    bool _newStrokeOnNextDab = true;
    int _nextLayerIndex = 0;
    readonly List<Transform> _strokeHistory = new();
    int _historyCursor = 0;
    public event Action StrokeHistoryChanged;


    public bool HasVisibleStrokes => _historyCursor > 0;
    public bool CanUndo => _historyCursor > 0;
    public bool CanRedo => _historyCursor < _strokeHistory.Count;

    [Header("Rendering Layers")]
    public bool layeredStrokes = true;
    public float layerEpsilon = 0.0008f; // 0.8 mm
    Material _baseCircle, _baseSquare;

    [Header("Overwrite Erase")]
    public bool overwriteErase = false;
    public float eraseRadiusFactor = 0.55f;
    LayerMask _paintMask;
    readonly Collider[] _overlapBuf = new Collider[64];

    [Header("State")]
    public bool paintingActive;
    Vector3? _lastPos;
    readonly List<Material> _ownedMaterials = new();
    int _paintLayerIndex = -1;
    void Awake()
    {
        _raycaster = GetComponent<ARRaycastManager>();
        _paintLayerIndex = LayerMask.NameToLayer("Paint");
        _paintMask = 1 << LayerMask.NameToLayer("Paint");

        if (_paintMask == 0)
            Debug.LogWarning("[PhonePainter] 'Paint' layer was not found. Overwrite erase will be disabled until you create it (Project Settings → Tags and Layers).");

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
    void OnDestroy()
    {
        // Destroy per-stroke materials we created
        foreach (var m in _ownedMaterials)
            if (m) Destroy(m);

        // Destroy our base copies
        if (_baseCircle) Destroy(_baseCircle);
        if (_baseSquare) Destroy(_baseSquare);
    }
    public void LockToPlane(ARPlane plane)
    {
        lockedPlane = plane;
        _lockedPlaneTransform = plane ? plane.transform : null;
        _lockedBoundaryLocal = null;
        _anchorRoot = null;
        ResetActiveStrokeState();
        _strokeParent = null;
        _lastPos = null;
        _newStrokeOnNextDab = true;
    }

    /// <summary>
    /// Strict lock that remembers the boundary snapshot and anchor transform so strokes stay glued to the selected plane.
    /// </summary>
    public void LockToPlaneStrict(ARPlane plane, Vector2[] boundaryLocal, Transform anchorRoot)
    {
        lockedPlane = plane;
        _lockedPlaneTransform = plane ? plane.transform : null;
        if (boundaryLocal != null && boundaryLocal.Length >= 3)
        {
            _lockedBoundaryLocal = new Vector2[boundaryLocal.Length];
            boundaryLocal.CopyTo(_lockedBoundaryLocal, 0);
        }
        else
        {
            _lockedBoundaryLocal = CopyBoundaryNow(plane);
        }
        _anchorRoot = anchorRoot;
        ResetActiveStrokeState();
        _strokeParent = null;
        _strokeMat = null;
        _lastPos = null;
        _newStrokeOnNextDab = true;
    }

    public void ClearLock()
    {
        lockedPlane = null;
        _lockedBoundaryLocal = null;
        _lockedPlaneTransform = null;
        _anchorRoot = null;
        ResetActiveStrokeState();
        _strokeParent = null;
        _strokeMat = null;
        _lastPos = null;
        _newStrokeOnNextDab = true;
    }

    public void StartPainting()
    {
        paintingActive = true;
        _newStrokeOnNextDab = true;
        _lastPos = null;
        // Ensure brushSize is current (it should already be synced from UI, but make sure)
        // brushSize is already set via SetBrushSize() from ToolUIController's sizeSlider
    }

    public void StopPainting()
    {
        paintingActive = false;
    }

    public bool UndoLastStroke()
    {
        if (!CanUndo) return false;

        _historyCursor--;
        var stroke = _strokeHistory[_historyCursor];
        if (stroke)
            stroke.gameObject.SetActive(false);

        ResetActiveStrokeState();
        StrokeHistoryChanged?.Invoke();
        return true;
    }

    public bool RedoStroke()
    {
        if (!CanRedo) return false;

        var stroke = _strokeHistory[_historyCursor];
        if (stroke)
            stroke.gameObject.SetActive(true);

        _historyCursor++;
        ResetActiveStrokeState();
        StrokeHistoryChanged?.Invoke();
        return true;
    }

    public void ClearAllStrokes()
    {
        for (int i = 0; i < _strokeHistory.Count; i++)
        {
            var stroke = _strokeHistory[i];
            if (stroke)
                Destroy(stroke.gameObject);
        }

        _strokeHistory.Clear();
        _historyCursor = 0;
        _nextLayerIndex = 0;
        ResetActiveStrokeState();

        if (strokesRoot)
        {
            for (int i = strokesRoot.childCount - 1; i >= 0; i--)
            {
                var child = strokesRoot.GetChild(i);
                if (child && child.GetComponent<StrokeMeta>())
                    Destroy(child.gameObject);
            }
        }

        StrokeHistoryChanged?.Invoke();
    }

    public void SetBrushSize(float v) => brushSize = Mathf.Clamp(v, 0.02f, 0.2f);
    public void SetShapeCircle() => shape = BrushShape.Circle;
    public void SetShapeSquare() => shape = BrushShape.Square;

    public void SetColor(Color c)
    {
        color = c;
        _newStrokeOnNextDab = true; // start new stroke next dab so old color remains
    }

    void Update()
    {
        if (!paintingActive || lockedPlane == null || _lockedPlaneTransform == null) return;

        Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
        if (!_raycaster.Raycast(center, _hits, TrackableType.PlaneWithinPolygon)) return;

        var hit = _hits[0];
        if (hit.trackableId != lockedPlane.trackableId) return;

        var n = hit.pose.up;
        var pos = hit.pose.position;

        if (_lockedBoundaryLocal != null && _lockedBoundaryLocal.Length >= 3)
        {
            var local = _lockedPlaneTransform.InverseTransformPoint(pos);
            Vector2 point2D = new Vector2(local.x, local.z);
            if (!PointInPolygon(point2D, _lockedBoundaryLocal))
            {
                _lastPos = null;
                return;
            }
        }

        if (_lastPos == null || Vector3.Distance(_lastPos.Value, pos) >= spacing)
        {
            EnsureStrokeParentAndMaterial();

            var meta = _strokeParent.GetComponent<StrokeMeta>();
            float lift = meta ? meta.liftOffset : liftFromPlane;

            if (overwriteErase)
                RemoveUnderlyingDabs(pos + n * lift, brushSize * eraseRadiusFactor);

            var prefab = shape == BrushShape.Square ? brushDab_SquarePrefab : brushDab_CirclePrefab;
            if (!prefab) return;

            var dab = Instantiate(prefab, pos + n * lift, Quaternion.identity, _strokeParent);
            dab.transform.forward = n;
            // Use current brushSize value - ensure it matches the UI slider value
            // This is updated in real-time via SetBrushSize() from ToolUIController
            dab.transform.localScale = Vector3.one * brushSize;

            // Assign Paint layer only if valid
            if (_paintLayerIndex >= 0) dab.layer = _paintLayerIndex;

            // Collider
            var col = dab.GetComponent<Collider>();
            if (!col)
            {
                var sc = dab.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                // Effective world radius should ≈ brushSize * 0.5
                // Because localScale = brushSize, set base radius to 0.5 so world radius matches.
                sc.radius = 0.5f;
            }
            else col.isTrigger = true;


            var mr = dab.GetComponentInChildren<MeshRenderer>();
            if (mr && meta && meta.strokeMaterial)
                mr.material = meta.strokeMaterial;

            _lastPos = pos;
        }
    }

    void EnsureStrokeParentAndMaterial()
    {
        if (_strokeParent == null || _newStrokeOnNextDab)
        {
            Transform root = _anchorRoot ? _anchorRoot : (strokesRoot ? strokesRoot : transform);
            var go = new GameObject($"Stroke_{shape}_{ColorUtility.ToHtmlStringRGB(color)}");
            _strokeParent = go.transform;
            if (root)
                _strokeParent.SetParent(root, false);

            var baseMat = (shape == BrushShape.Square) ? _baseSquare : _baseCircle;
            _strokeMat = baseMat ? new Material(baseMat) : null;
            if (_strokeMat)
            {
                _ownedMaterials.Add(_strokeMat);           // track for destruction
                _strokeMat.color = color;
                int rq = 3000 + Mathf.Clamp(_nextLayerIndex, 0, 500);
                _strokeMat.renderQueue = rq;
            }


            var meta = go.AddComponent<StrokeMeta>();
            meta.layerIndex = layeredStrokes ? _nextLayerIndex : 0;
            meta.liftOffset = layeredStrokes ? (liftFromPlane + meta.layerIndex * layerEpsilon) : liftFromPlane;
            meta.strokeMaterial = _strokeMat;

            if (layeredStrokes) _nextLayerIndex++;
            _newStrokeOnNextDab = false;

            RegisterStroke(_strokeParent);
        }
    }

    void RegisterStroke(Transform stroke)
    {
        if (!stroke) return;

        if (_historyCursor < _strokeHistory.Count)
        {
            for (int i = _historyCursor; i < _strokeHistory.Count; i++)
            {
                var staleStroke = _strokeHistory[i];
                if (staleStroke)
                    Destroy(staleStroke.gameObject);
            }
            _strokeHistory.RemoveRange(_historyCursor, _strokeHistory.Count - _historyCursor);
        }

        stroke.gameObject.SetActive(true);
        _strokeHistory.Add(stroke);
        _historyCursor = _strokeHistory.Count;
        StrokeHistoryChanged?.Invoke();
    }

    public bool TryGetStrokeBoundsWorld(out Bounds bounds)
    {
        bounds = default;
        var root = strokesRoot ? strokesRoot : transform;
        bool hasBounds = false;

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!r || !r.gameObject.activeInHierarchy) continue;
            if (!r.GetComponentInParent<StrokeMeta>()) continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return hasBounds;
    }

    public bool TryCaptureSnapshot(out Texture2D snapshot, out Bounds boundsWorld, int resolution = 1024, float paddingMeters = 0.05f)
    {
        snapshot = null;
        boundsWorld = default;

        if (!TryGetStrokeBoundsWorld(out boundsWorld))
            return false;

        var normal = _lockedPlaneTransform ? _lockedPlaneTransform.up : Vector3.up;
        var center = boundsWorld.center;

        float maxSize = Mathf.Max(boundsWorld.size.x, boundsWorld.size.z);
        float orthoSize = Mathf.Max(0.05f, maxSize * 0.5f + paddingMeters);
        float camDist = Mathf.Max(boundsWorld.extents.magnitude + paddingMeters * 2f, 0.5f);

        var camGO = new GameObject("StrokeCaptureCamera");
        var cam = camGO.AddComponent<Camera>();
        cam.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = camDist * 4f;
        cam.forceIntoRenderTexture = true;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.cullingMask = _paintLayerIndex >= 0 ? (1 << _paintLayerIndex) : ~0;

        cam.transform.position = center + normal * camDist;
        cam.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);

        var rt = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32);
        rt.Create();

        var prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        RenderTexture.active = rt;
        cam.Render();

        snapshot = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
        snapshot.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        snapshot.Apply();

        cam.targetTexture = null;
        RenderTexture.active = prevActive;

        rt.Release();
        Destroy(rt);
        Destroy(camGO);

        return true;
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

            if (otherLayer <= curLayer)
                Destroy(go);
        }
    }

    static Vector2[] CopyBoundaryNow(ARPlane plane)
    {
        if (!plane) return null;
        var boundary = plane.boundary;
        if (!boundary.IsCreated || boundary.Length < 3) return null;
        var arr = new Vector2[boundary.Length];
        for (int i = 0; i < boundary.Length; i++)
            arr[i] = boundary[i];
        return arr;
    }

    static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        if (poly == null || poly.Length < 3) return true;
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var pi = poly[i];
            var pj = poly[j];
            bool intersect = ((pi.y > p.y) != (pj.y > p.y)) &&
                             (p.x < (pj.x - pi.x) * (p.y - pi.y) / ((pj.y - pi.y) + 1e-6f) + pi.x);
            if (intersect) inside = !inside;
        }
        return inside;
    }

    void ResetActiveStrokeState()
    {
        _strokeParent = null;
        _strokeMat = null;
        _lastPos = null;
        _newStrokeOnNextDab = true;
    }
}

public enum BrushShape { Circle, Square }
