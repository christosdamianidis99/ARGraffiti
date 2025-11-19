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
    public Button btnSave;                      // Panel_Top/Button_Save (positioned at top-right)
    public Button btnColorPalette;              // Panel_Graffiti/Button_ColorPalette
    public Button btnPaintBrush;                // Panel_Graffiti/Button_PaintBrush
    public Button btnGallery;                   // Panel_Graffiti/Button_Gallery (positioned to the left of btnPaintBrush)
    public Button btnUndo;                      // Panel_Top/Button_Undo (same position as btnSelectSurface)
    public Button btnRedo;                      // Panel_Top/Button_Redo (same position as btnSelectSurface)
    public GameObject panelTop;                 // Panel_Top
    public GameObject panelTools;               // Panel_Tools
    public GameObject panelGraffiti;            // Panel_Graffiti
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
        btnScan.onClick.AddListener(() => {
            StartCoroutine(ButtonClickFeedback(btnScan));
            StartCoroutine(RescanRoutine());
        });
        btnSelectSurface.onClick.AddListener(() => {
            StartCoroutine(ButtonClickFeedback(btnSelectSurface));
            SelectSurfaceUnderReticle();
        });
        // btnGraffiti now uses the GraffitiButtonLongPress component to handle long press events
        // btnGraffiti.onClick.AddListener(ToggleGraffiti); // Removed, using long press instead
        btnSave.onClick.AddListener(Save);

        // Bind ColorPalette button to toggle tool panel
        if (btnColorPalette != null)
        {
            btnColorPalette.onClick.AddListener(() => {
                StartCoroutine(ButtonClickFeedback(btnColorPalette));
                ToggleToolPanel();
            });
        }
        else
        {
            // Try to find by name if not assigned
            GameObject colorPaletteObj = GameObject.Find("Button_ColorPalette");
            if (colorPaletteObj != null)
            {
                btnColorPalette = colorPaletteObj.GetComponent<Button>();
                if (btnColorPalette != null)
                {
                    btnColorPalette.onClick.AddListener(() => {
                        StartCoroutine(ButtonClickFeedback(btnColorPalette));
                        ToggleToolPanel();
                    });
                }
            }
        }

        // Position save button at top-right of panelTop
        PositionSaveButtonAtTopRight();

        // Initially hide save button - will show when surface is selected
        if (btnSave) btnSave.gameObject.SetActive(false);

        // Initially hide select_surface button, will show after clicking scan
        if (btnSelectSurface)
        {
            btnSelectSurface.gameObject.SetActive(false);
            // Make btnSelectSurface the same size as btnScan
            if (btnScan)
            {
                RectTransform scanRect = btnScan.GetComponent<RectTransform>();
                RectTransform selectRect = btnSelectSurface.GetComponent<RectTransform>();
                if (scanRect && selectRect)
                {
                    selectRect.sizeDelta = scanRect.sizeDelta;
                }
            }
        }

        // Initialize Undo/Redo buttons - same position as select_surface button, hidden by default
        InitializeUndoRedoButtons();

        // Initialize Gallery button - positioned to the left of brush button
        InitializeGalleryButton();

        SetPhase(Phase.Idle);

        // At runtime, set background colors transparent; backgrounds only visible in editor
        HidePanelBackgroundsInRuntime();
    }


    void HidePanelBackgroundsInRuntime()
    {
        // Only execute at runtime
        if (!Application.isPlaying) return;

        // At runtime, set Panel_Graffiti background to transparent; only visible in editor
        GameObject graffitiPanel = panelGraffiti != null ? panelGraffiti : GameObject.Find("Panel_Graffiti");
        if (graffitiPanel != null)
        {
            var image = graffitiPanel.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                var color = image.color;
                color.a = 0f;  // Make fully transparent
                image.color = color;
            }
        }

        // At runtime, set Panel_Top background to transparent; only visible in editor
        GameObject topPanel = panelTop != null ? panelTop : GameObject.Find("Panel_Top");
        if (topPanel != null)
        {
            var image = topPanel.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                var color = image.color;
                color.a = 0f;  // Make fully transparent
                image.color = color;
            }
        }
    }

    // Button click feedback animation with gray out and dazzle effect
    IEnumerator ButtonClickFeedback(Button btn)
    {
        if (!btn) yield break;

        RectTransform rect = btn.GetComponent<RectTransform>();
        if (!rect)
        {
            Debug.LogWarning("ButtonClickFeedback: RectTransform not found!");
            yield break;
        }

        // Get Image component for color change
        Image btnImage = btn.GetComponent<Image>();
        Color originalColor = Color.white;
        if (btnImage != null)
        {
            originalColor = btnImage.color;
        }

        Vector3 originalScale = rect.localScale;
        Vector3 pressedScale = originalScale * 0.75f;  // Shrink to 75% for more obvious feedback
        float duration = 0.2f;  // Animation duration

        // Phase 1: Press-in effect with gray out
        float elapsed = 0f;
        float pressDuration = duration * 0.3f;
        while (elapsed < pressDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pressDuration);
            rect.localScale = Vector3.Lerp(originalScale, pressedScale, t);

            // Gray out effect (reduce brightness)
            if (btnImage != null)
            {
                Color grayColor = Color.Lerp(originalColor, originalColor * 0.5f, t);
                btnImage.color = grayColor;
            }
            yield return null;
        }

        // Phase 2: Dazzle effect (bright flash)
        elapsed = 0f;
        float dazzleDuration = duration * 0.2f;
        Color dazzleColor = originalColor * 1.5f; // Bright white flash
        while (elapsed < dazzleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dazzleDuration);
            // Flash from gray to bright white
            if (btnImage != null)
            {
                Color flashColor = Color.Lerp(originalColor * 0.5f, dazzleColor, Mathf.Sin(t * Mathf.PI));
                btnImage.color = flashColor;
            }
            yield return null;
        }

        // Phase 3: Restore with bounce
        elapsed = 0f;
        float bounceDuration = duration * 0.5f;
        Vector3 bounceScale = originalScale * 1.1f;  // Bounce to 110%
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            // Smooth bounce using easing
            float bounceT = 1f - Mathf.Pow(1f - t, 3f); // Ease out cubic
            rect.localScale = Vector3.Lerp(pressedScale, bounceScale, bounceT);

            // Restore color
            if (btnImage != null)
            {
                Color restoreColor = Color.Lerp(dazzleColor, originalColor, bounceT);
                btnImage.color = restoreColor;
            }
            yield return null;
        }

        // Phase 4: Final settle
        elapsed = 0f;
        float settleDuration = duration * 0.3f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            rect.localScale = Vector3.Lerp(bounceScale, originalScale, t);
            yield return null;
        }

        // Ensure final state
        rect.localScale = originalScale;
        if (btnImage != null)
        {
            btnImage.color = originalColor;
        }
    }

    // ========================= PHASES =========================
    IEnumerator RescanRoutine()
    {
        if (cameraManager) cameraManager.autoFocusRequested = true;

        painter.StopPainting(); painter.ClearLock();
        if (reticle) reticle.selectedPlane = null;
        DestroyAnchorIfAny();
        DestroyFrozenBorder();

        if (planeFilter)
            planeFilter.ResetFilterForScan();

        if (strokesRoot)
        {
            for (int i = strokesRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(strokesRoot.GetChild(i).gameObject);
            }
        }

        _primaryScanPlane = null;
        _reticleStableStart = -1;

        if (btnUndo) btnUndo.gameObject.SetActive(false);
        if (btnRedo) btnRedo.gameObject.SetActive(false);

        if (arSession) arSession.Reset();
        yield return null;

        SetPhase(Phase.Scanning);

        planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;

        yield return null;
        TogglePlaneMesh(true);

        UpdateUndoRedoButtonsVisibility();
    }

    void SetPhase(Phase p)
    {
        _phase = p;

        // UI defaults
        // Note: panelTools visibility is now controlled by ToggleToolPanel(), 
        // so we don't force it to false here. It will be shown/hidden based on user interaction.
        // Only set to false when transitioning to Idle or Scanning phases
        if (p == Phase.Idle || p == Phase.Scanning)
        {
            if (panelTools) panelTools.SetActive(false);
        }
        // Save button is positioned at top-right of panelTop
        btnSelectSurface.interactable = false;
        btnGraffiti.interactable = false;

        switch (_phase)
        {
            case Phase.Idle:
                if (planeManager) planeManager.enabled = false;
                TogglePlaneMesh(false);
                // Hide select_surface button in Idle phase
                if (btnSelectSurface) btnSelectSurface.gameObject.SetActive(false);
                // Hide Undo/Redo buttons in Idle phase
                UpdateUndoRedoButtonsVisibility();
                // Hide save button in Idle phase
                if (btnSave) btnSave.gameObject.SetActive(false);
                SetTip("Press Scan to detect a surface.");
                break;

            case Phase.Scanning:
                if (planeManager) planeManager.enabled = true;
                _primaryScanPlane = null;
                _reticleStableStart = -1;
                // Show all planes immediately when entering scanning phase
                TogglePlaneMesh(true);
                // Don't show select_surface button immediately - wait for plane detection in Update()
                if (btnSelectSurface)
                {
                    btnSelectSurface.gameObject.SetActive(false);
                }
                // Hide Undo/Redo buttons in Scanning phase
                UpdateUndoRedoButtonsVisibility();
                // Hide save button in Scanning phase
                if (btnSave) btnSave.gameObject.SetActive(false);
                SetTip("Move phone. Center dot turns green over a surface.");
                break;

            case Phase.PlaneSelected:
                planeManager.requestedDetectionMode = PlaneDetectionMode.None; // stop growth

                TogglePlaneMesh(false);   // hide dynamic meshes
                BuildFrozenBorder();      // show frozen outline
                // Panel_Tools is now controlled by ToggleToolPanel() - don't auto-show
                // if (panelTools) panelTools.SetActive(true);
                // Save button is positioned at top-right of panelTop
                // Hide select_surface button after selecting surface
                if (btnSelectSurface) btnSelectSurface.gameObject.SetActive(false);
                // Update Undo/Redo buttons visibility - show only if graffiti exists
                UpdateUndoRedoButtonsVisibility();
                // Show save button only if there's graffiti
                UpdateSaveButtonVisibility();
                btnGraffiti.interactable = true;
                SetTip("Press Graffiti to start/stop painting.");
                break;

            case Phase.Painting:
                TogglePlaneMesh(false);
                if (btnSelectSurface) btnSelectSurface.gameObject.SetActive(false);
                UpdateUndoRedoButtonsVisibility();
                UpdateSaveButtonVisibility();
                btnGraffiti.interactable = true;
                painter.StartPainting();
                SetTip("Graffiti ON. Keep the dot on the surface and move the phone.");
                break;
        }

        StyleGraffitiButton(_phase == Phase.Painting);
    }

    void Update()
    {
        // Update save button and undo/redo buttons visibility when surface is selected
        if (_phase == Phase.PlaneSelected || _phase == Phase.Painting)
        {
            UpdateSaveButtonVisibility();
            UpdateUndoRedoButtonsVisibility();
        }

        if (_phase != Phase.Scanning) return;

        // In Scanning phase, ensure undo/redo buttons are hidden (no surface selected yet)
        UpdateUndoRedoButtonsVisibility();

        // Show/hide select_surface button based on plane detection
        if (btnSelectSurface)
        {
            bool hasPlane = false;
            if (planeFilter)
                hasPlane = planeFilter.PrimaryIsStable();
            else
                hasPlane = reticle && reticle.isOverAnyPlane;

            // Show button only when plane is detected
            if (hasPlane && !btnSelectSurface.gameObject.activeSelf)
            {
                btnSelectSurface.gameObject.SetActive(true);
                btnSelectSurface.interactable = true;
                // Ensure button can receive raycasts
                Image btnImage = btnSelectSurface.GetComponent<Image>();
                if (btnImage != null)
                {
                    btnImage.raycastTarget = true;
                }
            }
            else if (!hasPlane && btnSelectSurface.gameObject.activeSelf)
            {
                // Hide button if plane is lost
                btnSelectSurface.gameObject.SetActive(false);
            }
            else if (hasPlane && btnSelectSurface.gameObject.activeSelf)
            {
                // Keep button enabled when plane is available
                btnSelectSurface.interactable = true;
            }
        }

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
            if (_currentAnchor && strokesRoot)
                _currentAnchor.transform.SetParent(strokesRoot, worldPositionStays: true);
        }

        // Snapshot border now & pass anchor root to painter
        var boundary = CopyBoundary(plane);
        var anchorRoot = _currentAnchor ? _currentAnchor.transform : null;
        if (painter)
        {
            painter.strokesRoot = strokesRoot;
            painter.LockToPlaneStrict(plane, boundary, anchorRoot);
        }

        SetPhase(Phase.PlaneSelected);
    }

    void ToggleGraffiti()
    {
        if (_phase == Phase.Painting) { painter.StopPainting(); SetPhase(Phase.PlaneSelected); }
        else if (_phase == Phase.PlaneSelected) { SetPhase(Phase.Painting); }
    }

    /// <summary>
    /// Start graffiti (called by long press button)
    /// </summary>
    public void StartGraffiti()
    {
        if (_phase == Phase.PlaneSelected)
        {
            SetPhase(Phase.Painting);
        }
    }

    /// <summary>
    /// Stop graffiti (called when button is released)
    /// </summary>
    public void StopGraffiti()
    {
        if (_phase == Phase.Painting)
        {
            painter.StopPainting();
            SetPhase(Phase.PlaneSelected);
            // Update save button and undo/redo buttons visibility after stopping graffiti
            UpdateSaveButtonVisibility();
            UpdateUndoRedoButtonsVisibility();
        }
    }

    /// <summary>
    /// Position the save button at the top-right corner of panelTop using HorizontalLayoutGroup spacing
    /// </summary>
    void PositionSaveButtonAtTopRight()
    {
        if (!btnSave || !panelTop) return;

        RectTransform panelRect = panelTop.GetComponent<RectTransform>();
        RectTransform buttonRect = btnSave.GetComponent<RectTransform>();

        if (!panelRect || !buttonRect) return;

        // Ensure button ignores layout to position independently
        var layoutElement = btnSave.GetComponent<UnityEngine.UI.LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = true;
        }

        // Sync size with scan button
        if (btnScan)
        {
            RectTransform scanRect = btnScan.GetComponent<RectTransform>();
            if (scanRect)
            {
                buttonRect.sizeDelta = scanRect.sizeDelta;
            }
        }

        // Set anchor to right-center to align vertically with other buttons
        // Other buttons use y: 0.5 (vertical center) with Y position 0, so we align to that
        buttonRect.anchorMin = new Vector2(1f, 0.5f);  // Right-center anchor
        buttonRect.anchorMax = new Vector2(1f, 0.5f);  // Right-center anchor
        buttonRect.pivot = new Vector2(1f, 0.5f);      // Pivot at right-center

        // Position with offset from right-center
        // X: distance from right edge, Y: 0 to align with other buttons vertically
        float offsetX = -60f;  // 60 pixels from right edge
        float offsetY = 0f;    // 0 pixels vertically (aligned with scan/select_surface buttons)
        buttonRect.anchoredPosition = new Vector2(offsetX, offsetY);

        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// Update save button visibility - show when surface is selected
    /// </summary>
    void UpdateSaveButtonVisibility()
    {
        if (!btnSave) return;

        // Show save button when surface is selected (Phase.PlaneSelected or Phase.Painting)
        // No need to check for graffiti - button is available once surface is fixed
        bool surfaceSelected = (_phase == Phase.PlaneSelected || _phase == Phase.Painting);

        btnSave.gameObject.SetActive(surfaceSelected);
    }

    /// <summary>
    /// Check if there are any graffiti strokes on the surface
    /// </summary>
    bool HasGraffitiStrokes()
    {
        if (!strokesRoot)
        {
            return false;
        }

        return strokesRoot.GetComponentInChildren<StrokeMeta>(true) != null;
    }

    void Save()
    {
        StartCoroutine(ButtonClickFeedback(btnSave));
    }

    /// <summary>
    /// Initialize Undo/Redo buttons - ensure they are properly configured
    /// </summary>
    void InitializeUndoRedoButtons()
    {
        if (!btnSelectSurface)
        {
            Debug.LogWarning("[InitializeUndoRedoButtons] btnSelectSurface is NULL, skipping initialization");
            return;
        }

        RectTransform selectRect = btnSelectSurface.GetComponent<RectTransform>();
        if (!selectRect)
        {
            Debug.LogWarning("[InitializeUndoRedoButtons] btnSelectSurface RectTransform is NULL, skipping initialization");
            return;
        }

        // Find undo button if not assigned
        if (!btnUndo)
        {
            GameObject topPanel = panelTop != null ? panelTop : GameObject.Find("Panel_Top");
            if (topPanel != null)
            {
                Transform undoTransform = topPanel.transform.Find("Button_Undo");
                if (undoTransform != null)
                {
                    btnUndo = undoTransform.GetComponent<Button>();
                }
                else
                {
                    Debug.LogWarning("[InitializeUndoRedoButtons] Button_Undo not found in Panel_Top");
                }
            }
            else
            {
                Debug.LogWarning("[InitializeUndoRedoButtons] Panel_Top not found, cannot locate Button_Undo");
            }
        }

        // Find redo button if not assigned
        if (!btnRedo)
        {
            GameObject topPanel = panelTop != null ? panelTop : GameObject.Find("Panel_Top");
            if (topPanel != null)
            {
                Transform redoTransform = topPanel.transform.Find("Button_Redo");
                if (redoTransform != null)
                {
                    btnRedo = redoTransform.GetComponent<Button>();
                }
                else
                {
                    Debug.LogWarning("[InitializeUndoRedoButtons] Button_Redo not found in Panel_Top");
                }
            }
            else
            {
                Debug.LogWarning("[InitializeUndoRedoButtons] Panel_Top not found, cannot locate Button_Redo");
            }
        }

        // Button size and spacing
        // Use actual size from selectRect, or fallback to default size if 0 (when using HorizontalLayoutGroup)
        float buttonWidth = selectRect.sizeDelta.x > 0 ? selectRect.sizeDelta.x : 512f;
        float buttonHeight = selectRect.sizeDelta.y > 0 ? selectRect.sizeDelta.y : 80f;
        float spacing = 40f; // Spacing between buttons

        // Get Panel_Top (parent) rect to calculate center position
        RectTransform parentRect = selectRect.parent as RectTransform;
        if (parentRect == null)
        {
            Debug.LogWarning("[InitializeUndoRedoButtons] Parent RectTransform not found!");
            return;
        }

        // Get scan button's Y position for horizontal alignment
        // Since all buttons are in the same Panel_Top, they should use the same Y coordinate
        float centerY = 0f;
        if (btnScan)
        {
            RectTransform scanRect = btnScan.GetComponent<RectTransform>();
            if (scanRect)
            {
                centerY = scanRect.anchoredPosition.y;
            }
        }
        else
        {
            // Fallback: use select_surface button's Y position
            centerY = selectRect.anchoredPosition.y;
        }

        // Calculate center position where select_surface_btn will be
        // select_surface_btn uses left-bottom anchor (0, 0) and is centered by HorizontalLayoutGroup
        // Since buttons use left-bottom anchor, the center position is where the button's left edge should be
        float panelWidth = parentRect.rect.width;
        float selectSurfaceLeftEdge = (panelWidth - buttonWidth) * 0.5f;

        // Get scan button's anchor settings for alignment
        Vector2 buttonAnchorMin = selectRect.anchorMin;
        Vector2 buttonAnchorMax = selectRect.anchorMax;
        Vector2 buttonPivot = selectRect.pivot;
        if (btnScan)
        {
            RectTransform scanRect = btnScan.GetComponent<RectTransform>();
            if (scanRect)
            {
                buttonAnchorMin = scanRect.anchorMin;
                buttonAnchorMax = scanRect.anchorMax;
                buttonPivot = scanRect.pivot;
            }
        }

        if (btnUndo)
        {
            RectTransform undoRect = btnUndo.GetComponent<RectTransform>();
            if (undoRect)
            {
                // Disable HorizontalLayoutGroup influence on undo button
                UnityEngine.UI.LayoutElement layoutElement = btnUndo.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = btnUndo.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                }
                layoutElement.ignoreLayout = true;

                // Use same anchor as scan button for horizontal alignment
                undoRect.anchorMin = buttonAnchorMin;
                undoRect.anchorMax = buttonAnchorMax;
                undoRect.pivot = buttonPivot;

                // Ensure size matches select_surface button
                undoRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);

                // Position: to the left of select_surface_btn
                // Undo button left edge should be at: select_surface left edge - buttonWidth - spacing
                undoRect.anchoredPosition = new Vector2(
                    selectSurfaceLeftEdge - buttonWidth - spacing,
                    centerY
                );

                // Set icon if not already set (runtime fallback)
                Image undoImage = btnUndo.GetComponent<Image>();
                if (undoImage != null && undoImage.sprite == null)
                {
                    SetButtonIcon(btnUndo, "undo");
                }

                // Ensure button is interactable (not grayed out)
                btnUndo.interactable = true;

                // Bind click event with same animation effect as scan button
                btnUndo.onClick.RemoveAllListeners(); // Remove existing listeners to avoid duplicates
                btnUndo.onClick.AddListener(() => {
                    StartCoroutine(ButtonClickFeedback(btnUndo));
                });

#if UNITY_EDITOR
                btnUndo.gameObject.SetActive(true);
#else
                btnUndo.gameObject.SetActive(false);
#endif
            }
            else
            {
                Debug.LogWarning("[InitializeUndoRedoButtons] btnUndo RectTransform is NULL");
            }
        }
        else
        {
            Debug.LogWarning("[InitializeUndoRedoButtons] btnUndo is NULL! Button will not be available.");
        }

        if (btnRedo)
        {
            RectTransform redoRect = btnRedo.GetComponent<RectTransform>();
            if (redoRect)
            {
                // Disable HorizontalLayoutGroup influence on redo button
                UnityEngine.UI.LayoutElement layoutElement = btnRedo.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = btnRedo.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                }
                layoutElement.ignoreLayout = true;

                // Use same anchor as scan button for horizontal alignment
                redoRect.anchorMin = buttonAnchorMin;
                redoRect.anchorMax = buttonAnchorMax;
                redoRect.pivot = buttonPivot;

                // Ensure size matches select_surface button
                redoRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);

                // Position: to the right of select_surface_btn
                // Redo button left edge should be at: select_surface right edge + spacing
                // select_surface right edge = select_surface left edge + buttonWidth
                redoRect.anchoredPosition = new Vector2(
                    selectSurfaceLeftEdge + buttonWidth + spacing,
                    centerY
                );

                // Set icon if not already set (runtime fallback)
                Image redoImage = btnRedo.GetComponent<Image>();
                if (redoImage != null && redoImage.sprite == null)
                {
                    SetButtonIcon(btnRedo, "redo");
                }

                // Ensure button is interactable (not grayed out)
                btnRedo.interactable = true;

                // Bind click event with same animation effect as scan button
                btnRedo.onClick.RemoveAllListeners(); // Remove existing listeners to avoid duplicates
                btnRedo.onClick.AddListener(() => {
                    StartCoroutine(ButtonClickFeedback(btnRedo));
                });

#if UNITY_EDITOR
                btnRedo.gameObject.SetActive(true);
#else
                btnRedo.gameObject.SetActive(false);
#endif
            }
            else
            {
                Debug.LogWarning("[InitializeUndoRedoButtons] btnRedo RectTransform is NULL");
            }
        }
        else
        {
            Debug.LogWarning("[InitializeUndoRedoButtons] btnRedo is NULL! Button will not be available.");
        }
    }

    /// <summary>
    /// Initialize Gallery button - positioned to the left of brush button, same style and size
    /// </summary>
    void InitializeGalleryButton()
    {
        // Find paintbrush button if not assigned
        if (!btnPaintBrush && panelGraffiti)
        {
            Transform brushTransform = panelGraffiti.transform.Find("Button_PaintBrush");
            if (brushTransform != null)
            {
                btnPaintBrush = brushTransform.GetComponent<Button>();
            }
        }

        if (!btnPaintBrush)
        {
            Debug.LogWarning("InitializeGalleryButton: btnPaintBrush not found. Gallery button initialization skipped.");
            return;
        }

        RectTransform brushRect = btnPaintBrush.GetComponent<RectTransform>();
        if (!brushRect)
        {
            Debug.LogWarning("InitializeGalleryButton: btnPaintBrush RectTransform not found.");
            return;
        }

        // Find gallery button if not assigned
        if (!btnGallery && panelGraffiti)
        {
            Transform galleryTransform = panelGraffiti.transform.Find("Button_Gallery");
            if (galleryTransform != null)
            {
                btnGallery = galleryTransform.GetComponent<Button>();
            }
        }

        if (!btnGallery)
        {
            Debug.LogWarning("InitializeGalleryButton: btnGallery not found. Please create Button_Gallery in Panel_Graffiti.");
            return;
        }

        RectTransform galleryRect = btnGallery.GetComponent<RectTransform>();
        if (!galleryRect)
        {
            Debug.LogWarning("InitializeGalleryButton: btnGallery RectTransform not found.");
            return;
        }

        // Set anchor to left-center (same as brush button)
        galleryRect.anchorMin = new Vector2(0f, 0.5f);
        galleryRect.anchorMax = new Vector2(0f, 0.5f);
        galleryRect.pivot = new Vector2(0.5f, 0.5f);
        galleryRect.sizeDelta = brushRect.sizeDelta;

        // Position at the leftmost position
        // Get parent panel width to calculate margin
        RectTransform parentRect = panelGraffiti != null ? panelGraffiti.GetComponent<RectTransform>() : null;
        float panelWidth = parentRect != null ? parentRect.rect.width : 0f;

        // Use same margin calculation as PanelGraffitiLayout
        float minMarginPercent = 0.03f; // 3% of screen width
        float buttonSize = brushRect.sizeDelta.x;
        float minMargin = panelWidth > 0 ? panelWidth * minMarginPercent : 30f; // Fallback to 30px if width is 0

        // Position gallery button at the leftmost position
        // Position = left margin + button width / 2
        galleryRect.anchoredPosition = new Vector2(
            minMargin + buttonSize * 0.5f,
            0f
        );

        // Set icon (gallery.png) if not already set
        Image galleryImage = btnGallery.GetComponent<Image>();
        if (galleryImage != null && galleryImage.sprite == null)
        {
            SetButtonIcon(btnGallery, "gallery");
        }

        // Bind click event (TODO)
        btnGallery.onClick.RemoveAllListeners(); // Remove existing listeners to avoid duplicates
        btnGallery.onClick.AddListener(() => {
            StartCoroutine(ButtonClickFeedback(btnGallery));
        });
    }

    /// <summary>
    /// Set button icon from texture file
    /// </summary>
    void SetButtonIcon(Button button, string iconName)
    {
        if (!button) return;

        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage == null)
        {
            Debug.LogWarning($"Button {button.name} does not have an Image component");
            return;
        }

        // Try loading from Resources folder first (runtime)
        Sprite sprite = Resources.Load<Sprite>($"Textures/{iconName}");

        // If not found in Resources, try loading as Texture2D and convert to Sprite
        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>($"Textures/{iconName}");
            if (texture != null)
            {
                sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );
            }
        }

#if UNITY_EDITOR
        // Try loading directly from Assets path (editor only)
        if (sprite == null)
        {
            string assetPath = $"Assets/Textures/{iconName}.png";
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                // Try loading as Texture2D and convert
                Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture != null)
                {
                    sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );
                }
            }
        }
#endif

        if (sprite != null)
        {
            buttonImage.sprite = sprite;
        }
        else
        {
            Debug.LogWarning($"Could not load icon: {iconName}.png. Please ensure the file exists in Assets/Textures/ or Resources/Textures/");
        }
    }

    /// <summary>
    /// Update Undo/Redo buttons visibility and interactable state - show when:
    /// 1. Surface is selected (Phase.PlaneSelected or Phase.Painting) AND graffiti exists
    /// 2. OR when actively painting (Phase.Painting) - show immediately when starting to paint
    /// Hide when: no surface selected or no graffiti exists
    /// Buttons are enabled/disabled based on whether there are strokes to undo/redo
    /// </summary>
    void UpdateUndoRedoButtonsVisibility()
    {
        bool shouldShow = false;

        if (_phase == Phase.Painting)
        {
            shouldShow = true;
        }
        else if (_phase == Phase.PlaneSelected)
        {
            shouldShow = HasGraffitiStrokes();
        }

        // Check if there are strokes to undo/redo
        bool canUndo = HasGraffitiStrokes();
        bool canRedo = false; // TODO: Implement redo stack tracking

#if UNITY_EDITOR
        if (btnUndo != null)
        {
            btnUndo.gameObject.SetActive(shouldShow);
            btnUndo.interactable = canUndo;
        }
        if (btnRedo != null)
        {
            btnRedo.gameObject.SetActive(shouldShow);
            btnRedo.interactable = canRedo;
        }
#else
        if (btnUndo != null)
        {
            btnUndo.gameObject.SetActive(shouldShow);
            btnUndo.interactable = canUndo;
        }
        else
        {
            Debug.LogWarning("[UpdateUndoRedoButtonsVisibility] btnUndo is NULL! Cannot update visibility.");
        }
        
        if (btnRedo != null)
        {
            btnRedo.gameObject.SetActive(shouldShow);
            btnRedo.interactable = canRedo;
        }
        else
        {
            Debug.LogWarning("[UpdateUndoRedoButtonsVisibility] btnRedo is NULL! Cannot update visibility.");
        }
#endif
    }

    /// <summary>
    /// Toggle the visibility of Panel_Tools when ColorPalette button is clicked
    /// </summary>
    public void ToggleToolPanel()
    {
        if (panelTools != null)
        {
            // Toggle visibility: if currently active, hide it; if hidden, show it
            bool isCurrentlyActive = panelTools.activeSelf;
            panelTools.SetActive(!isCurrentlyActive);
        }
        else
        {
            // Try to find Panel_Tools by name if not assigned
            GameObject toolsObj = GameObject.Find("Panel_Tools");
            if (toolsObj != null)
            {
                bool isCurrentlyActive = toolsObj.activeSelf;
                toolsObj.SetActive(!isCurrentlyActive);
                panelTools = toolsObj; // Cache the reference
            }
        }
    }

    // ========================= PLANE EVENTS/VISUALS =========================
    void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        if (_phase != Phase.Scanning) return;

        if (_primaryScanPlane)
            ShowOnlyPlane(_primaryScanPlane);
        else
            TogglePlaneMesh(true); // before primary: show all detected planes
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
        if (planeFilter)
        {
            if (visible) planeFilter.RefreshVisibility();
            else planeFilter.ForceHideAllMeshes();
            return;
        }

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