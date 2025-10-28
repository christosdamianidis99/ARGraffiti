using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public enum Phase { Idle, Scanning, PlaneSelected, Painting }

public class AppStateControllerPhone : MonoBehaviour
{
    [Header("AR")]
    public ARSession arSession;                 // (assign) AR Session in scene
    public ARPlaneManager planeManager;         // (assign) on XR Origin
    public ARRaycastManager raycaster;          // (assign) on XR Origin
    public ARAnchorManager anchorManager;       // (assign) on XR Origin
    public ARCameraManager cameraManager;       // (assign) on Main Camera
    public ReticleDot reticle;                  // (assign) on XR Origin
    public PhonePainter painter;                // (assign) on XR Origin

    [Header("UI")]
    public Button btnScan;                      // Panel_Top/Button_Scan
    public Button btnSelectSurface;             // Panel_Top/Button_SelectSurface
    public Button btnGraffiti;                  // Panel_Top/Button_Graffiti
    public Button btnReselect;                  // Panel_Top/Button_Reselect
    public GameObject panelTools;               // Panel_Tools
    public TMPro.TMP_Text txtTips;              // optional tips label

    [Header("Painting")]
    public Transform strokesRoot;               // (assign) StrokesRoot under XR Origin

    // State
    Phase _phase = Phase.Idle;
    ARAnchor _currentAnchor;

    // Single-plane scanning (one plane that grows)
    ARPlane _primaryScanPlane;
    double _reticleStableStart = -1;
    const double STABLE_DWELL_SECONDS = 0.20;   // 200 ms stability before choosing primary
    public PlaneQualityFilter planeFilter;   // (assign) on XR Origin

    // Frozen outline after selection (non-resizing)
    GameObject _frozenBorderGO;
    public float frozenLineWidth = 0.01f;       // 1 cm
    public Color frozenLineColor = new Color(0f, 1f, 0.8f, 0.9f);

    void OnEnable()
    {
        if (planeManager) planeManager.planesChanged += OnPlanesChanged;
    }
    void OnDisable()
    {
        if (planeManager) planeManager.planesChanged -= OnPlanesChanged;
    }

    void Awake()
    {
        btnScan.onClick.AddListener(() => StartCoroutine(RescanRoutine()));
        btnSelectSurface.onClick.AddListener(SelectSurfaceUnderReticle);
        btnGraffiti.onClick.AddListener(ToggleGraffiti);
        btnReselect.onClick.AddListener(Reselect);
        SetPhase(Phase.Idle);
    }

    // ========================= PHASES =========================
    IEnumerator RescanRoutine()
    {
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

        if (arSession) arSession.Reset();
        yield return null;

        SetPhase(Phase.Scanning);

        planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;

        // hide all until we pick a primary plane
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
                _primaryScanPlane = null;
                _reticleStableStart = -1;
                TogglePlaneMesh(false);
                SetTip("Move phone. Center dot turns green over a surface.");
                break;

            case Phase.PlaneSelected:
                planeManager.requestedDetectionMode = PlaneDetectionMode.None; // stop growth

                TogglePlaneMesh(false);   // hide dynamic meshes
                BuildFrozenBorder();      // show frozen outline
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
                SetTip("Graffiti ON. Keep the dot on the surface and move the phone.");
                break;
        }

        StyleGraffitiButton(_phase == Phase.Painting);
    }

    void Update()
    {
        if (_phase != Phase.Scanning) return;

        // Only enable Select when the filter says we have a stable, vetted plane
        if (planeFilter)
            btnSelectSurface.interactable = planeFilter
                   ? planeFilter.PrimaryIsStable()
                   : (reticle && reticle.isOverAnyPlane);
        else
            btnSelectSurface.interactable = reticle && reticle.isOverAnyPlane; // fallback if filter not assigned

        //if (_phase != Phase.Scanning || reticle == null) return;

        

        // Choose ONE primary plane after a short stable dwell
        if (_primaryScanPlane == null)
        {
            if (reticle.isOverAnyPlane && reticle.planeUnderReticle != null)
            {
                if (_reticleStableStart < 0) _reticleStableStart = Time.realtimeSinceStartupAsDouble;

                if (Time.realtimeSinceStartupAsDouble - _reticleStableStart >= STABLE_DWELL_SECONDS)
                {
                    _primaryScanPlane = GetRootPlane(reticle.planeUnderReticle);

                    // Reduce noise: restrict detection to this alignment
                    var align = _primaryScanPlane.alignment;
                    planeManager.requestedDetectionMode =
                        (align == PlaneAlignment.HorizontalUp || align == PlaneAlignment.HorizontalDown)
                        ? PlaneDetectionMode.Horizontal : PlaneDetectionMode.Vertical;
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
            // Keep following merges (subsumed) to avoid flicker
            var root = GetRootPlane(_primaryScanPlane);
            if (root != _primaryScanPlane)
            {
                _primaryScanPlane = root;
                ShowOnlyPlane(_primaryScanPlane);
            }
        }
    }

    // ========================= SELECTION / ANCHOR =========================
    void SelectSurfaceUnderReticle()
    {
        if (!reticle) return;

        ARPlane plane = null;
        if (planeFilter && planeFilter.PrimaryIsStable())
            plane = planeFilter.PrimaryPlane;

        // Fallback: reticle plane if filter not ready
        if (!plane && reticle) plane = reticle.planeUnderReticle;
        if (!plane) return;

        plane = GetRootPlane(plane);
        reticle.selectedPlane = plane;

       

        // Anchor at the last center-hit pose for stability
        DestroyAnchorIfAny();
        if (anchorManager && raycaster)
        {
            var pose = reticle.lastHitPose;
            _currentAnchor = anchorManager.AttachAnchor(plane, pose);
        }

        // Snapshot border now & pass anchor root to painter
        var boundary = CopyBoundary(plane);
        var anchorRoot = _currentAnchor ? _currentAnchor.transform : null;
        painter.strokesRoot = strokesRoot;
        // If you use the "strict polygon" version of PhonePainter, call LockToPlaneStrict:
        // painter.LockToPlaneStrict(plane, boundary, anchorRoot);
        // If you use the simpler version, just lock the plane:
        painter.LockToPlane(plane);

        SetPhase(Phase.PlaneSelected);
    }

    void ToggleGraffiti()
    {
        if (_phase == Phase.Painting) { painter.StopPainting(); SetPhase(Phase.PlaneSelected); }
        else if (_phase == Phase.PlaneSelected) { SetPhase(Phase.Painting); }
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
        TogglePlaneMesh(false); // hidden until primary chosen again
    }

    // ========================= PLANE EVENTS/VISUALS =========================
    void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (_phase != Phase.Scanning) return;

        if (_primaryScanPlane)
            ShowOnlyPlane(_primaryScanPlane);
        else
            TogglePlaneMesh(false); // before primary: keep all hidden
    }

    ARPlane GetRootPlane(ARPlane p)
    {
        while (p && p.subsumedBy != null) p = p.subsumedBy;
        return p;
    }
    void ShowOnlyPlane(ARPlane planeToShow)
    {
        var targetRoot = GetRootPlane(planeToShow);
        foreach (var p in planeManager.trackables)
        {
            var mr = p.GetComponent<MeshRenderer>();
            if (!mr) continue;
            mr.enabled = (GetRootPlane(p) == targetRoot);
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

    // Build a non-resizing outline (LineRenderer) from the snapshot boundary
    void BuildFrozenBorder()
    {
        DestroyFrozenBorder();
        var plane = reticle.selectedPlane; // The plane data is still needed
        if (!plane) return;

        var boundary = CopyBoundary(plane);
        if (boundary == null || boundary.Length < 3) return;

        _frozenBorderGO = new GameObject("FrozenPlaneBorder");

        // Parent to the anchor if it exists
        Transform parentTransform = _currentAnchor ? _currentAnchor.transform : plane.transform;
        _frozenBorderGO.transform.SetParent(parentTransform, worldPositionStays: false);

        // If parenting to the anchor, set the local position/rotation
        if (_currentAnchor)
        {
            // Get world poses
            Pose planePoseInWorld = new Pose(plane.transform.position, plane.transform.rotation);
            Pose anchorPoseInWorld = new Pose(_currentAnchor.transform.position, _currentAnchor.transform.rotation);

            // Calculate inverse of anchor pose MANUALLY
            Quaternion invAnchorRot = Quaternion.Inverse(anchorPoseInWorld.rotation);
            Vector3 invAnchorPos = invAnchorRot * -anchorPoseInWorld.position;
            Pose inverseAnchorPose = new Pose(invAnchorPos, invAnchorRot);

            // Now transform the plane's world pose into the anchor's local space
            Pose planePoseInAnchorSpace = inverseAnchorPose.Multiply(planePoseInWorld); // PoseUtils.Multiply equivalent

            _frozenBorderGO.transform.localPosition = planePoseInAnchorSpace.position;
            _frozenBorderGO.transform.localRotation = planePoseInAnchorSpace.rotation;
        }
        // If not parenting to anchor (fallback), no local adjustment needed as it's directly under plane

        var lr = _frozenBorderGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = false; // Points are relative to the parent
        lr.loop = true;
        lr.widthMultiplier = frozenLineWidth;

        // Use a default material or load one (as suggested previously)
        lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); // Or load your M_Border_Unlit
        lr.material.color = frozenLineColor;

        // Add start/end color keys for consistent color (optional but good practice)
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(frozenLineColor, 0.0f), new GradientColorKey(frozenLineColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(frozenLineColor.a, 0.0f), new GradientAlphaKey(frozenLineColor.a, 1.0f) }
        );
        lr.colorGradient = gradient;


        lr.positionCount = boundary.Length;
        for (int i = 0; i < boundary.Length; i++)
            // Points are relative to the plane's local coords, now the GO's local coords
            lr.SetPosition(i, new Vector3(boundary[i].x, 0f, boundary[i].y));
    }

    void DestroyFrozenBorder()
    {
        if (_frozenBorderGO) Destroy(_frozenBorderGO);
        _frozenBorderGO = null;
    }

    // ========================= HELPERS =========================
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

void StyleGraffitiButton(bool on)
    {
        // Option 1: Still tint Image (simple)
        var img = btnGraffiti.GetComponent<Image>();
        if (img) img.color = on ? new Color(0.08f, 0.8f, 0.4f, 0.9f) : new Color(1f, 1f, 1f, 0.25f); // Keep your colors or adjust

        // Option 2: Change Text (as before)
        var txt = btnGraffiti.GetComponentInChildren<TMPro.TMP_Text>();
        if (txt) txt.text = on ? "Graffiti (ON)" : "Graffiti";

        // Option 3: Trigger Animation (if using Animator transition)
        var animator = btnGraffiti.GetComponent<Animator>();
        if (animator)
        {
            // Assumes you have Boolean parameters named "IsOn" or similar in your Animator Controller
             animator.SetBool("IsOn", on);
             // Or trigger specific states
             // animator.Play(on ? "GraffitiOnState" : "GraffitiOffState");
        }
    }
}
public static class PoseUtils
{
    public static Pose Multiply(this Pose lhs, Pose rhs)
    {
        return new Pose(
            lhs.position + lhs.rotation * rhs.position,
            lhs.rotation * rhs.rotation
        );
    }
}