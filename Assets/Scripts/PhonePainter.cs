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
        _lastPos = null;
    }

    public void ClearLock()
    {
        lockedPlane = null;
        _lastPos = null;
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
        if (!paintingActive || lockedPlane == null) return;

        Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
        if (!_raycaster.Raycast(center, _hits, TrackableType.PlaneWithinPolygon)) return;

        var hit = _hits[0];
        if (hit.trackableId != lockedPlane.trackableId) return;

        var n = hit.pose.up;
        var pos = hit.pose.position;

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
            var root = strokesRoot ? strokesRoot : transform;
            var go = new GameObject($"Stroke_{shape}_{ColorUtility.ToHtmlStringRGB(color)}");
            _strokeParent = go.transform;
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

            if (otherLayer <= curLayer)
                Destroy(go);
        }
    }
}

public enum BrushShape { Circle, Square }
