using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public enum Phase { Idle, Scanning, PlaneSelected, Painting }

public class AppStateControllerPhone : MonoBehaviour
{
    [Header("AR")]
    public ARSession arSession;           // <-- ASSIGN
    public ARPlaneManager planeManager;   // <-- ASSIGN (XR Origin)
    public ReticleDot reticle;            // <-- ASSIGN (XR Origin)
    public PhonePainter painter;          // <-- ASSIGN (XR Origin)

    [Header("UI")]
    public Button btnScan;                // <-- ASSIGN
    public Button btnSelectSurface;       // <-- ASSIGN
    public Button btnGraffiti;            // <-- ASSIGN
    public Button btnReselect;            // <-- ASSIGN
    public GameObject panelTools;         // <-- ASSIGN (Panel_Tools)
    public TMPro.TMP_Text txtTips;        // optional

    [Header("Painting")]
    public Transform strokesRoot;         // <-- ASSIGN (StrokesRoot)

    Phase _phase = Phase.Idle;

    void OnEnable() { if (planeManager) planeManager.planesChanged += OnPlanesChanged; }
    void OnDisable() { if (planeManager) planeManager.planesChanged -= OnPlanesChanged; }

    void Awake()
    {
        btnScan.onClick.AddListener(OnScanPressed);
        btnSelectSurface.onClick.AddListener(SelectSurfaceUnderReticle);
        btnGraffiti.onClick.AddListener(ToggleGraffiti);
        btnReselect.onClick.AddListener(Reselect);
        SetPhase(Phase.Idle);
    }

    public string GetCurrentPhaseName() => _phase.ToString();

    // ========== FLOW ==========
    void OnScanPressed() { StartCoroutine(RescanRoutine()); }

    IEnumerator RescanRoutine()
    {
        // Stop/clear painting
        painter.StopPainting();
        painter.ClearLock();
        if (reticle) reticle.selectedPlane = null;

        // Clear old paint
        if (strokesRoot)
        {
            for (int i = strokesRoot.childCount - 1; i >= 0; i--)
                Destroy(strokesRoot.GetChild(i).gameObject);
        }

        // Reset AR session (clears planes reliably)
        if (arSession) arSession.Reset();
        yield return null; // one frame

        // Enter scanning with plane meshes visible
        SetPhase(Phase.Scanning);

        planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
        TogglePlaneMesh(true);
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
                if (reticle) reticle.selectedPlane = null;
                painter.StopPainting(); painter.ClearLock();
                SetTip("Press Scan to detect surfaces.");
                break;

            case Phase.Scanning:
                if (planeManager) planeManager.enabled = true;
                TogglePlaneMesh(true);
                if (reticle) reticle.selectedPlane = null;
                painter.StopPainting(); painter.ClearLock();
                SetTip("Move phone. Dot turns green over a surface.");
                break;

            case Phase.PlaneSelected:
                ToggleNonSelectedMeshes(false); // hide others
                if (panelTools) panelTools.SetActive(true);
                btnReselect.gameObject.SetActive(true);
                btnGraffiti.interactable = true;
                painter.StopPainting();
                SetTip("Press Graffiti to start/stop painting.");
                break;

            case Phase.Painting:
                ToggleNonSelectedMeshes(false);
                if (panelTools) panelTools.SetActive(true);
                btnReselect.gameObject.SetActive(true);
                btnGraffiti.interactable = true;
                painter.StartPainting();
                SetTip("Graffiti ON. Keep the dot on the surface and move phone.");
                break;
        }

        // Style the Graffiti button as toggle
        StyleGraffitiButton(_phase == Phase.Painting);
    }

    void Update()
    {
        if (_phase == Phase.Scanning && reticle)
            btnSelectSurface.interactable = reticle.isOverAnyPlane;
    }

    void SelectSurfaceUnderReticle()
    {
        if (!reticle || !reticle.isOverAnyPlane || !reticle.planeUnderReticle) return;
        var plane = reticle.planeUnderReticle;

        // Snapshot this plane's initial boundary to enforce strict borders
        var boundaryCopy = CopyBoundary(plane);
        painter.LockToPlaneStrict(plane, boundaryCopy); // <-- NEW strict lock
        reticle.selectedPlane = plane;

        SetPhase(Phase.PlaneSelected);
    }

    void ToggleGraffiti()
    {
        if (_phase == Phase.Painting) SetPhase(Phase.PlaneSelected);
        else if (_phase == Phase.PlaneSelected) SetPhase(Phase.Painting);
    }

    void Reselect()
    {
        painter.ClearLock();
        if (reticle) reticle.selectedPlane = null;
        SetPhase(Phase.Scanning);
    }

    // ========== Helpers ==========
    void TogglePlaneMesh(bool visible)
    {
        if (!planeManager) return;
        foreach (var p in planeManager.trackables)
        {
            var mr = p.GetComponent<MeshRenderer>();
            if (mr) mr.enabled = visible;
        }
    }
    void ToggleNonSelectedMeshes(bool selectedVisible)
    {
        if (!planeManager || !reticle) return;
        var sel = reticle.selectedPlane;
        foreach (var p in planeManager.trackables)
        {
            var mr = p.GetComponent<MeshRenderer>();
            if (!mr) continue;
            mr.enabled = (p == sel) ? selectedVisible : false;
        }
    }
    void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (_phase == Phase.Scanning)
            foreach (var p in args.added) { var mr = p.GetComponent<MeshRenderer>(); if (mr) mr.enabled = true; }
        else
            foreach (var p in args.added) { var mr = p.GetComponent<MeshRenderer>(); if (mr) mr.enabled = false; }
    }
    void SetTip(string s) { if (txtTips) txtTips.text = s; }

    // Copy boundary at selection time for strict painting
    static Vector2[] CopyBoundary(ARPlane plane)
    {
        var nat = plane.boundary;
        if (!nat.IsCreated || nat.Length < 3) return null;
        var arr = new Vector2[nat.Length];
        for (int i = 0; i < nat.Length; i++) arr[i] = nat[i];
        return arr;
    }

    // Simple graffiti toggle style
    void StyleGraffitiButton(bool on)
    {
        var img = btnGraffiti.GetComponent<Image>();
        var txt = btnGraffiti.GetComponentInChildren<TMPro.TMP_Text>();
        if (img) img.color = on ? new Color(0.08f, 0.8f, 0.4f, 0.9f) : new Color(1f, 1f, 1f, 0.25f);
        if (txt) txt.text = on ? "Graffiti  (ON)" : "Graffiti";
    }
}
