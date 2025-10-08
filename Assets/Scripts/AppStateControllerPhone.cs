using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public enum Phase { Idle, Scanning, PlaneSelected, Painting }

public class AppStateControllerPhone : MonoBehaviour
{
    [Header("AR")]
    public ARSession arSession;                 // assign
    public ARPlaneManager planeManager;         // assign (XR Origin)
    public ARRaycastManager raycaster;          // assign (XR Origin)
    public ARAnchorManager anchorManager;       // assign (XR Origin)
    public ARCameraManager cameraManager;       // assign (Main Camera)
    public ReticleDot reticle;                  // assign (XR Origin)
    public PhonePainter painter;                // assign (XR Origin)

    [Header("UI")]
    public Button btnScan;
    public Button btnSelectSurface;
    public Button btnGraffiti;
    public Button btnReselect;
    public GameObject panelTools;
    public TMPro.TMP_Text txtTips; // optional

    [Header("Painting")]
    public Transform strokesRoot;               // assign (StrokesRoot)
    ARAnchor _currentAnchor;

    // Single-plane scanning
    ARPlane _primaryScanPlane;
    double _reticleStableStart = -1;
    const double STABLE_DWELL_SECONDS = 0.20;    // 200ms dwell to avoid flicker

    // Frozen outline after selection
    GameObject _frozenBorderGO;
    public float frozenLineWidth = 0.01f;
    public Color frozenLineColor = new Color(0f, 1f, 0.8f, 0.9f);

    Phase _phase = Phase.Idle;

    void OnEnable() { if (planeManager) planeManager.planesChanged += OnPlanesChanged; }
    void OnDisable() { if (planeManager) planeManager.planesChanged -= OnPlanesChanged; }

    void Awake()
    {
        btnScan.onClick.AddListener(() => StartCoroutine(RescanRoutine()));
        btnSelectSurface.onClick.AddListener(SelectSurfaceUnderReticle);
        btnGraffiti.onClick.AddListener(ToggleGraffiti);
        btnReselect.onClick.AddListener(Reselect);
        SetPhase(Phase.Idle);
    }

    // ------------------- Phases -------------------

    IEnumerator RescanRoutine()
    {
        // performance: make sure autofocus is on
        if (cameraManager) cameraManager.autoFocusRequested = true;

        painter.StopPainting(); painter.ClearLock();
        if (reticle) reticle.selectedPlane = null;
        DestroyAnchorIfAny();
        DestroyFrozenBorder();

        // clear strokes
        if (strokesRoot)
            for (int i = strokesRoot.childCount - 1; i >= 0; i--)
                Destroy(strokesRoot.GetChild(i).gameObject);

        _primaryScanPlane = null;
        _reticleStableStart = -1;

        // reset AR (cleans planes)
        if (arSession) arSession.Reset();
        yield return null;

        SetPhase(Phase.Scanning);

        planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;


        // **Hide all initially** (no clutter). We’ll show only the chosen one.
        TogglePlaneMesh(false);
    }

    void SetPhase(Phase p)
    {
        _phase = p;

        // UI defaults
        if (panelTools) panelTools.SetActive(false);
        btnReselect.gameObject.SetActive(false);
        btnSelectSurface.interactable = false;
        btnGraffiti.interactable = false;

        switch (_phase)
        {
            case Phase.Idle:
                if (planeManager) planeManager.enabled = false;
                TogglePlaneMesh(false);
                SetTip("Press Scan to detect a surface.");
                break;

            case Phase.Scanning:
                if (planeManager) planeManager.enabled = true;
                // meshes hidden until we lock a primary
                _primaryScanPlane = null;
                _reticleStableStart = -1;
                SetTip("Move phone. Center dot turns green over a surface.");
                break;

            case Phase.PlaneSelected:
                // hard lock: stop detection
                planeManager.requestedDetectionMode = PlaneDetectionMode.None;

                // hide all dynamic meshes; draw frozen border only
                TogglePlaneMesh(false);
                BuildFrozenBorder();
                if (panelTools) panelTools.SetActive(true);
                btnReselect.gameObject.SetActive(true);
                btnGraffiti.interactable = true;
                SetTip("Press Graffiti to start/stop painting.");
                break;

            case Phase.Painting:
                TogglePlaneMesh(false);
                if (panelTools) panelTools.SetActive(true);
                btnReselect.gameObject.SetActive(true);
                btnGraffiti.interactable = true;
                painter.StartPainting();
                SetTip("Graffiti ON. Keep the dot on the surface and move phone.");
                break;
        }

        StyleGraffitiButton(_phase == Phase.Painting);
    }

    void Update()
    {
        if (_phase != Phase.Scanning || reticle == null) return;

        // Only enable "Select Surface" when dot is over *some* plane
        btnSelectSurface.interactable = reticle.isOverAnyPlane;

        // We want just ONE plane during scan: pick it when the dot is stably over a plane for 200ms
        if (_primaryScanPlane == null)
        {
            if (reticle.isOverAnyPlane && reticle.planeUnderReticle != null)
            {
                if (_reticleStableStart < 0) _reticleStableStart = Time.realtimeSinceStartupAsDouble;

                if (Time.realtimeSinceStartupAsDouble - _reticleStableStart >= STABLE_DWELL_SECONDS)
                {
                    _primaryScanPlane = GetRootPlane(reticle.planeUnderReticle);

                    // Optional: restrict detection to the alignment we just found (reduces noise & boosts perf)
                    var align = _primaryScanPlane.alignment;
                    planeManager.requestedDetectionMode =
                        (align == PlaneAlignment.HorizontalUp || align == PlaneAlignment.HorizontalDown)
                        ? PlaneDetectionMode.Horizontal
                        : PlaneDetectionMode.Vertical;

                    ShowOnlyPlane(_primaryScanPlane);
                    SetTip("Move phone to grow this surface. Then press Select Surface.");
                }
            }
            else
            {
                _reticleStableStart = -1; // lost hit → reset dwell
            }
        }
        else
        {
            // keep only the primary visible; if it gets merged, follow root
            var root = GetRootPlane(_primaryScanPlane);
            if (root != _primaryScanPlane)
            {
                _primaryScanPlane = root;
                ShowOnlyPlane(_primaryScanPlane);
            }
        }
    }

    // ------------------- Selection / Anchor -------------------

    void SelectSurfaceUnderReticle()
    {
        if (!reticle) return;

        // Use the chosen primary if available, else the current plane under reticle
        var plane = _primaryScanPlane != null ? _primaryScanPlane : reticle.planeUnderReticle;
        if (!plane) return;

        plane = GetRootPlane(plane);
        reticle.selectedPlane = plane;

        DestroyAnchorIfAny();
        if (anchorManager && raycaster)
        {
            var pose = reticle.lastHitPose;
            _currentAnchor = anchorManager.AttachAnchor(plane, pose);
        }

        var boundary = CopyBoundary(plane);
        var anchorRoot = _currentAnchor ? _currentAnchor.transform : null;
        painter.strokesRoot = strokesRoot;
        painter.LockToPlaneStrict(plane, boundary, anchorRoot);

        SetPhase(Phase.PlaneSelected);
    }

    void Reselect()
    {
        painter.StopPainting(); painter.ClearLock();
        if (reticle) reticle.selectedPlane = null;
        DestroyAnchorIfAny();
        DestroyFrozenBorder();

        SetPhase(Phase.Scanning);

        planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;

        _primaryScanPlane = null;
        _reticleStableStart = -1;
        TogglePlaneMesh(false); // hidden until we pick a primary again
    }

    // ------------------- Plane events/visuals -------------------

    void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (_phase != Phase.Scanning) return;

        // keep non-primary meshes hidden
        if (_primaryScanPlane)
        {
            ShowOnlyPlane(_primaryScanPlane);
        }
        else
        {
            // before we pick primary: keep everything hidden (no clutter)
            TogglePlaneMesh(false);
        }
    }

    ARPlane GetRootPlane(ARPlane p)
    {
        while (p && p.subsumedBy != null) p = p.subsumedBy;
        return p;
    }

    void ShowOnlyPlane(ARPlane planeToShow)
    {
        foreach (var p in planeManager.trackables)
        {
            var mr = p.GetComponent<MeshRenderer>();
            if (!mr) continue;
            mr.enabled = (p == planeToShow);
        }
    }

    void TogglePlaneMesh(bool visible)
    {
        foreach (var p in planeManager.trackables)
        {
            var mr = p.GetComponent<MeshRenderer>();
            if (mr) mr.enabled = visible;
        }
    }

    // Build a non-resizing outline from the snapshot boundary
    void BuildFrozenBorder()
    {
        DestroyFrozenBorder();
        var plane = reticle.selectedPlane;
        if (!plane) return;

        var boundary = CopyBoundary(plane);
        if (boundary == null || boundary.Length < 3) return;

        _frozenBorderGO = new GameObject("FrozenPlaneBorder");
        _frozenBorderGO.transform.SetParent(plane.transform, worldPositionStays: false);

        var lr = _frozenBorderGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = frozenLineWidth;
        lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.material.color = frozenLineColor;

        lr.positionCount = boundary.Length;
        for (int i = 0; i < boundary.Length; i++)
            lr.SetPosition(i, new Vector3(boundary[i].x, 0f, boundary[i].y));
    }

    void DestroyFrozenBorder()
    {
        if (_frozenBorderGO) Destroy(_frozenBorderGO);
        _frozenBorderGO = null;
    }

    // ------------------- Helpers -------------------

    static Vector2[] CopyBoundary(ARPlane plane)
    {
        var nat = plane.boundary;
        if (!nat.IsCreated || nat.Length < 3) return null;
        var arr = new Vector2[nat.Length];
        for (int i = 0; i < nat.Length; i++) arr[i] = nat[i];
        return arr;
    }

    void DestroyAnchorIfAny()
    {
        if (_currentAnchor)
        {
            Destroy(_currentAnchor.gameObject);
            _currentAnchor = null;
        }
    }

    void SetTip(string s) { if (txtTips) txtTips.text = s; }
    // Inside AppStateControllerPhone (same class that wires the buttons)
    void ToggleGraffiti()
    {
        if (_phase == Phase.Painting)
        {
            // turn OFF
            painter.StopPainting();
            SetPhase(Phase.PlaneSelected);
        }
        else if (_phase == Phase.PlaneSelected)
        {
            // turn ON
            SetPhase(Phase.Painting);
        }
        // If you style the button, SetPhase already updates the look;
        // if you prefer, you can explicitly restyle here too.
    }

    void StyleGraffitiButton(bool on)
    {
        var img = btnGraffiti.GetComponent<Image>();
        var txt = btnGraffiti.GetComponentInChildren<TMPro.TMP_Text>();
        if (img) img.color = on ? new Color(0.08f, 0.8f, 0.4f, 0.9f) : new Color(1f, 1f, 1f, 0.25f);
        if (txt) txt.text = on ? "Graffiti  (ON)" : "Graffiti";
    }
}
