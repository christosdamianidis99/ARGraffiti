using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public enum Phase { Idle, Scanning, PlaneSelected, Painting, Gallery }

public class AppStateControllerPhone : MonoBehaviour
{
    [Header("AR")]
    public ARSession arSession;                 
    public ARPlaneManager planeManager;         
    public ARRaycastManager raycaster;          
    public ARAnchorManager anchorManager;       
    public ARCameraManager cameraManager;       
    public ReticleDot reticle;                  
    public PhonePainter painter;                

    [Header("UI")]
    public Button btnScan;                      
    public Button btnSelectSurface;             
    public Button btnGraffiti;                  
    public Button btnSave;                      
    public Button btnColorPalette;              
    public Button btnPaintBrush;                
    public Button btnGallery;                   
    public Button btnUndo;                      
    public Button btnRedo;                      
    public GameObject panelTop;                 
    public GameObject panelTools;               
    public GameObject panelGraffiti;            
    public TMPro.TMP_Text txtTips;              
    public float notificationSeconds = 2f;     
    [Header("Gallery UI")]
    public GameObject panelGalleryScreen;      
    public Button btnGalleryBack;               
    public Button btnGalleryDelete;             
    public GameObject galleryLoadingIndicator;  

    [Header("Painting")]
    public Transform strokesRoot;               

    [Header("Gallery")]
    public float galleryMaxDistanceMeters = 30f;

    readonly System.Collections.Generic.List<GameObject> _galleryPreviews = new System.Collections.Generic.List<GameObject>();
    readonly System.Collections.Generic.List<ARAnchor> _galleryAnchors = new System.Collections.Generic.List<ARAnchor>();
    readonly Dictionary<GameObject, bool> _galleryHiddenUI = new Dictionary<GameObject, bool>();
    Coroutine _galleryRoutine;
    bool _galleryVisible;
    Phase _phaseBeforeGallery = Phase.Idle;
    bool _planeManagerWasEnabled;
    PlaneDetectionMode _planeManagerPrevDetectionMode = PlaneDetectionMode.None;
    bool _reticleWasActive;
    bool _reticleUIWasEnabled;
    GraffitiRepository _repo;
    GraffitiData _lastSpawnedData; 

  
    Phase _phase = Phase.Idle;
    ARAnchor _currentAnchor;
    bool _lastGalleryEnabled;
    string _lastGalleryOwnerEmail;

    ARPlane _primaryScanPlane;
    double _reticleStableStart = -1;
    const double STABLE_DWELL_SECONDS = 0.20;  
    public PlaneQualityFilter planeFilter;   

    GameObject _frozenBorderGO;
    public float frozenLineWidth = 0.01f;     
    public Color frozenLineColor = new Color(0f, 1f, 0.8f, 0.9f);

    void OnEnable()
    {
        if (planeManager) planeManager.trackablesChanged.AddListener(OnPlanesChanged);
    }
    void OnDisable()
    {
        if (planeManager) planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
    }

    void OnDestroy()
    {
        if (painter)
            painter.StrokeHistoryChanged -= UpdateUndoRedoButtonsVisibility;
    }

    void Awake()
    {
        btnScan.onClick.AddListener(() => {
            CoroutineRunner.Run(ButtonClickFeedback(btnScan));
            CoroutineRunner.Run(RescanRoutine());
        });
        btnSelectSurface.onClick.AddListener(() => {
            CoroutineRunner.Run(ButtonClickFeedback(btnSelectSurface));
            SelectSurfaceUnderReticle();
        });

        btnSave.onClick.AddListener(Save);

        if (btnColorPalette != null)
        {
                btnColorPalette.onClick.AddListener(() => {
                    CoroutineRunner.Run(ButtonClickFeedback(btnColorPalette));
                    ToggleToolPanel();
                });
            }
        else
        {
            GameObject colorPaletteObj = GameObject.Find("Button_ColorPalette");
            if (colorPaletteObj != null)
            {
                btnColorPalette = colorPaletteObj.GetComponent<Button>();
                if (btnColorPalette != null)
                {
                    btnColorPalette.onClick.AddListener(() => {
                        CoroutineRunner.Run(ButtonClickFeedback(btnColorPalette));
                        ToggleToolPanel();
                    });
                }
            }
        }

        PositionSaveButtonAtTopRight();

        if (btnSave) btnSave.gameObject.SetActive(false);

        if (btnSelectSurface)
        {
            btnSelectSurface.gameObject.SetActive(false);
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

        InitializeUndoRedoButtons();

        InitializeGalleryButton();
        InitializeGalleryScreen();

        SetPhase(Phase.Idle);

        HidePanelBackgroundsInRuntime();

        if (painter)
            painter.StrokeHistoryChanged += UpdateUndoRedoButtonsVisibility;

        EnsureRepository();
        UpdateGalleryButtonState();
    }


    void HidePanelBackgroundsInRuntime()
    {
        if (!Application.isPlaying) return;

        GameObject graffitiPanel = panelGraffiti != null ? panelGraffiti : GameObject.Find("Panel_Graffiti");
        if (graffitiPanel != null)
        {
            var image = graffitiPanel.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                var color = image.color;
                color.a = 0f;  
                image.color = color;
            }
        }

        GameObject topPanel = panelTop != null ? panelTop : GameObject.Find("Panel_Top");
        if (topPanel != null)
        {
            var image = topPanel.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
            {
                var color = image.color;
                color.a = 0f;  
                image.color = color;
            }
        }
    }

    IEnumerator ButtonClickFeedback(Button btn)
    {
        if (!btn) yield break;

        RectTransform rect = btn.GetComponent<RectTransform>();
        if (!rect)
        {
            Debug.LogWarning("ButtonClickFeedback: RectTransform not found!");
            yield break;
        }

        Image btnImage = btn.GetComponent<Image>();
        Color originalColor = Color.white;
        if (btnImage != null)
        {
            originalColor = btnImage.color;
        }

        Vector3 originalScale = rect.localScale;
        Vector3 pressedScale = originalScale * 0.75f;  
        float duration = 0.2f;  

        float elapsed = 0f;
        float pressDuration = duration * 0.3f;
        while (elapsed < pressDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pressDuration);
            rect.localScale = Vector3.Lerp(originalScale, pressedScale, t);

            if (btnImage != null)
            {
                Color grayColor = Color.Lerp(originalColor, originalColor * 0.5f, t);
                btnImage.color = grayColor;
            }
            yield return null;
        }

        elapsed = 0f;
        float dazzleDuration = duration * 0.2f;
        Color dazzleColor = originalColor * 1.5f; 
        while (elapsed < dazzleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dazzleDuration);
            if (btnImage != null)
            {
                Color flashColor = Color.Lerp(originalColor * 0.5f, dazzleColor, Mathf.Sin(t * Mathf.PI));
                btnImage.color = flashColor;
            }
            yield return null;
        }

        elapsed = 0f;
        float bounceDuration = duration * 0.5f;
        Vector3 bounceScale = originalScale * 1.1f; 
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            float bounceT = 1f - Mathf.Pow(1f - t, 3f); 
            rect.localScale = Vector3.Lerp(pressedScale, bounceScale, bounceT);

            if (btnImage != null)
            {
                Color restoreColor = Color.Lerp(dazzleColor, originalColor, bounceT);
                btnImage.color = restoreColor;
            }
            yield return null;
        }

        elapsed = 0f;
        float settleDuration = duration * 0.3f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            rect.localScale = Vector3.Lerp(bounceScale, originalScale, t);
            yield return null;
        }

        rect.localScale = originalScale;
        if (btnImage != null)
        {
            btnImage.color = originalColor;
        }
    }

    // ========================= PHASES =========================
    IEnumerator RescanRoutine()
    {
        ExitGalleryIfActive();

        if (cameraManager) cameraManager.autoFocusRequested = true;

        EnablePlaneManager();

        painter.StopPainting(); painter.ClearLock();
        if (reticle) reticle.selectedPlane = null;
        DestroyAnchorIfAny();
        DestroyFrozenBorder();

        ClearGalleryPreviews();

        if (planeFilter)
            planeFilter.ResetFilterForScan();

        if (painter)
        {
            painter.ClearAllStrokes();
        }
        else if (strokesRoot)
        {
            painter.ClearAllStrokes();
        }

        if (strokesRoot) { 
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

        yield return WaitForTrackingReady(3f);

        SetPhase(Phase.Scanning);

        planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;

        yield return null;
        TogglePlaneMesh(true);

        if (reticle) reticle.gameObject.SetActive(true);

        UpdateUndoRedoButtonsVisibility();
    }

    void ExitGalleryIfActive()
    {
        if (_phase != Phase.Gallery && _galleryRoutine == null && !_galleryVisible && _galleryHiddenUI.Count == 0)
            return;

        ClearGalleryPreviews();
        RestoreAfterGallery();
    }

    IEnumerator WaitForTrackingReady(float timeoutSeconds)
    {
        double start = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - start < timeoutSeconds)
        {
            if (ARSession.state == ARSessionState.SessionTracking)
                yield break;
            yield return null;
        }

        Debug.LogWarning($"[WaitForTrackingReady] Timed out ({timeoutSeconds}s) waiting for AR tracking to stabilize.");
    }

    void SetPhase(Phase p)
    {
        _phase = p;


        if (p == Phase.Idle || p == Phase.Scanning)
        {
            if (panelTools) panelTools.SetActive(false);
        }
        btnSelectSurface.interactable = false;
        btnGraffiti.interactable = false;

        switch (_phase)
        {
            case Phase.Idle:
                if (planeManager)
                {
                    planeManager.enabled = true; 
                    planeManager.requestedDetectionMode = PlaneDetectionMode.None;
                }
                TogglePlaneMesh(false);
                if (btnSelectSurface) btnSelectSurface.gameObject.SetActive(false);
                UpdateUndoRedoButtonsVisibility();
                if (btnSave) btnSave.gameObject.SetActive(false);
                SetTip("Press Scan to detect a surface.");
                break;

            case Phase.Scanning:
                if (planeManager) planeManager.enabled = true;
                _primaryScanPlane = null;
                _reticleStableStart = -1;
                TogglePlaneMesh(true);

                if (btnSelectSurface)
                {
                    btnSelectSurface.gameObject.SetActive(false);
                }

                UpdateUndoRedoButtonsVisibility();

                if (btnSave) btnSave.gameObject.SetActive(false);
                SetTip("Move phone. Center dot turns green over a surface.");
                break;

            case Phase.PlaneSelected:
                planeManager.requestedDetectionMode = PlaneDetectionMode.None; 

                ShowOnlySelectedPlaneMesh();
                BuildFrozenBorder();      
                
                if (btnSelectSurface) btnSelectSurface.gameObject.SetActive(false);
                
                UpdateUndoRedoButtonsVisibility();
                
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
            case Phase.Gallery:
                TogglePlaneMesh(false);
                if (btnSelectSurface) btnSelectSurface.gameObject.SetActive(false);
                if (btnUndo) btnUndo.gameObject.SetActive(false);
                if (btnRedo) btnRedo.gameObject.SetActive(false);
                if (reticle && reticle.reticleUI) reticle.reticleUI.enabled = false; 
                if (painter) painter.StopPainting();
                SetTip("Loading gallery...");
                break;
        }

        StyleGraffitiButton(_phase == Phase.Painting);
    }

    void Update()
    {
        UpdateGalleryButtonState();

        
        if (_phase == Phase.PlaneSelected || _phase == Phase.Painting)
        {
            UpdateSaveButtonVisibility();
            UpdateUndoRedoButtonsVisibility();
        }

        if (_phase != Phase.Scanning) return;

        
        UpdateUndoRedoButtonsVisibility();

        
        if (btnSelectSurface)
        {
            bool hasPlane = false;
            if (planeFilter)
                hasPlane = planeFilter.PrimaryIsStable();
            else
                hasPlane = reticle && reticle.isOverAnyPlane;

        
            if (hasPlane && !btnSelectSurface.gameObject.activeSelf)
            {
                btnSelectSurface.gameObject.SetActive(true);
                btnSelectSurface.interactable = true;
        
                Image btnImage = btnSelectSurface.GetComponent<Image>();
                if (btnImage != null)
                {
                    btnImage.raycastTarget = true;
                }
            }
            else if (!hasPlane && btnSelectSurface.gameObject.activeSelf)
            {
        
                btnSelectSurface.gameObject.SetActive(false);
            }
            else if (hasPlane && btnSelectSurface.gameObject.activeSelf)
            {
        
                btnSelectSurface.interactable = true;
            }
        }

        



        if (_primaryScanPlane == null)
        {
            if (planeFilter && planeFilter.PrimaryIsStable())
            {
                _primaryScanPlane = GetRootPlane(planeFilter.PrimaryPlane);
                if (_primaryScanPlane)
                {
                    ShowOnlyPlane(_primaryScanPlane);
                    SetTip("Move phone to grow this surface. Then press Select Surface.");
                }
            }
            else if (reticle.isOverAnyPlane && reticle.planeUnderReticle != null)
            {
                if (_reticleStableStart < 0) _reticleStableStart = Time.realtimeSinceStartupAsDouble;

                if (Time.realtimeSinceStartupAsDouble - _reticleStableStart >= STABLE_DWELL_SECONDS)
                {
                    _primaryScanPlane = GetRootPlane(reticle.planeUnderReticle);

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
                _reticleStableStart = -1; 
            }
        }
        else
        {
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
        if (!plane && reticle) plane = reticle.planeUnderReticle;
        if (!plane) return;

        plane = GetRootPlane(plane);
        reticle.selectedPlane = plane;



        DestroyAnchorIfAny();
        if (anchorManager && raycaster)
        {
            var pose = reticle.lastHitPose;
            _currentAnchor = CreateWorldAnchor(pose);
            if (_currentAnchor && strokesRoot)
                _currentAnchor.transform.SetParent(strokesRoot, worldPositionStays: true);
        }

        HideAllOtherPlanes(plane);
        if (planeManager)
            planeManager.requestedDetectionMode = PlaneDetectionMode.None;
        ShowOnlySelectedPlaneMesh();

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


    public void StartGraffiti()
    {
        if (_phase == Phase.PlaneSelected)
        {
            SetPhase(Phase.Painting);
        }
    }

    public void StopGraffiti()
    {
        if (_phase == Phase.Painting)
        {
            painter.StopPainting();
            SetPhase(Phase.PlaneSelected);
            UpdateSaveButtonVisibility();
            UpdateUndoRedoButtonsVisibility();
        }
    }


    void PositionSaveButtonAtTopRight()
    {
        if (!btnSave || !panelTop) return;

        RectTransform panelRect = panelTop.GetComponent<RectTransform>();
        RectTransform buttonRect = btnSave.GetComponent<RectTransform>();

        if (!panelRect || !buttonRect) return;

        var layoutElement = btnSave.GetComponent<UnityEngine.UI.LayoutElement>();
        if (layoutElement != null)
        {
            layoutElement.ignoreLayout = true;
        }

        if (btnScan)
        {
            RectTransform scanRect = btnScan.GetComponent<RectTransform>();
            if (scanRect)
            {
                buttonRect.sizeDelta = scanRect.sizeDelta;
            }
        }

        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);

        float offsetX = -30f;  
        float offsetY = 0f;
        if (btnScan)
        {
            var scanRect = btnScan.GetComponent<RectTransform>();
            if (scanRect) offsetY = scanRect.anchoredPosition.y;
        }

        buttonRect.anchoredPosition = new Vector2(offsetX, offsetY);

        Canvas.ForceUpdateCanvases();
    }


    void UpdateSaveButtonVisibility()
    {
        if (!btnSave) return;


        bool surfaceSelected = (_phase == Phase.PlaneSelected || _phase == Phase.Painting);

        btnSave.gameObject.SetActive(surfaceSelected);
    }

 
    bool HasGraffitiStrokes()
    {
        if (painter)
        {
            if (painter.HasVisibleStrokes)
                return true;
        }

        if (!strokesRoot)
            return false;

        return strokesRoot.GetComponentInChildren<StrokeMeta>(true) != null;
    }

    void UpdateGalleryButtonState()
    {
        if (!btnGallery) return;

        EnsureRepository();

        string ownerEmail = CurrentOwnerEmail();
        bool hasEntries = GraffitiRepository.I && GraffitiRepository.I.HasForOwner(ownerEmail);

        if (hasEntries == _lastGalleryEnabled && ownerEmail == _lastGalleryOwnerEmail)
            return;

        _lastGalleryEnabled = hasEntries;
        _lastGalleryOwnerEmail = ownerEmail;

        btnGallery.interactable = hasEntries;
        var img = btnGallery.GetComponent<Image>();
        if (img)
        {
            var c = img.color;
            c.a = hasEntries ? 1f : 0.35f;
            img.color = c;
        }
    }

    string CurrentOwnerEmail()
    {
        return string.Empty; 
    }

    void EnsureRepository()
    {
        if (_repo)
            return;

        _repo = GraffitiRepository.I;
        if (_repo)
            return;

        var repoGO = new GameObject("GraffitiRepository");
        _repo = repoGO.AddComponent<GraffitiRepository>();
    }

    void Save()
    {
        CoroutineRunner.Run(ButtonClickFeedback(btnSave));
        CoroutineRunner.Run(SaveGraffitiRoutine());
    }

    void OpenGallery()
    {

        if (_galleryVisible || _galleryRoutine != null)
        {
            HideGalleryPreviews();
            return;
        }

        ShowGalleryInAR();
    }

    IEnumerator SaveGraffitiRoutine()
    {
        if (painter == null || !painter.HasVisibleStrokes)
        {
            SetTip("Draw something before saving.");
            yield break;
        }

        yield return null; 

        if (!painter.TryCaptureStrokeTexture(out var snapshot, out var boundsWorld))
        {
            Debug.LogWarning("[SaveGraffiti] Unable to capture strokes.");
            yield break;
        }

        string id = Guid.NewGuid().ToString("N");

        string ownerEmail = string.Empty;
        string ownerName = "Local";
        string userFolder = string.IsNullOrEmpty(ownerEmail) ? "guest" : SanitizeForPath(ownerEmail);
        string baseDir = Path.Combine(Application.persistentDataPath, "graffiti", userFolder);
        Directory.CreateDirectory(baseDir);

        string pngPath = Path.Combine(baseDir, id + ".png");
        string thumbPath = Path.Combine(baseDir, id + "_thumb.png");

        try
        {
            var bytes = snapshot.EncodeToPNG();
            File.WriteAllBytes(pngPath, bytes);

            var thumb = CreateThumbnail(snapshot, 256);
            File.WriteAllBytes(thumbPath, thumb.EncodeToPNG());
            Destroy(thumb);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveGraffiti] Failed to write files: {ex.Message}");
            yield break;
        }

        var poseSource = _currentAnchor ? _currentAnchor.transform : (painter.lockedPlane ? painter.lockedPlane.transform : null);
        Quaternion rotation = poseSource ? poseSource.rotation : Quaternion.identity;
        Vector3 position = boundsWorld.center;

        
        Vector3 planeRight = rotation * Vector3.right;
        Vector3 planeForward = rotation * Vector3.forward;
        Vector3 ext = boundsWorld.extents;
        float halfWidth = Vector3.Dot(ext, new Vector3(Mathf.Abs(planeRight.x), Mathf.Abs(planeRight.y), Mathf.Abs(planeRight.z)));
        float halfHeight = Vector3.Dot(ext, new Vector3(Mathf.Abs(planeForward.x), Mathf.Abs(planeForward.y), Mathf.Abs(planeForward.z)));
        Vector3 localScale = new Vector3(Mathf.Max(0.1f, halfWidth * 2f), Mathf.Max(0.1f, halfHeight * 2f), 1f);

        var data = new GraffitiData
        {
            id = id,
            title = "",
            pngPath = pngPath,
            thumbPath = thumbPath,
            createdUtcTicks = DateTime.UtcNow.Ticks,
            position = position,
            rotation = rotation,
            localScale = localScale,
            ownerEmail = ownerEmail,
            ownerName = ownerName,
        };

        EnsureRepository();
        if (_repo)
            _repo.AddOrUpdate(data);

        UpdateGalleryButtonState();
        ShowNotification("Saved! Restarting scan...");
        
        CoroutineRunner.Run(RescanRoutine());
    }

    public void ShowGalleryInAR(bool forceCreateAnchors = true)
    {
        Debug.Log("[Gallery] Request to show gallery");
        if (ARSession.state != ARSessionState.SessionTracking)
        {
            SetTip("Move phone to resume tracking before opening gallery.");
            return;
        }
        string ownerEmail = CurrentOwnerEmail();
        EnsureRepository();
        if (_repo == null || !_repo.HasForOwner(ownerEmail))
        {
            SetTip("No saved graffiti yet.");
            return;
        }

        _phaseBeforeGallery = _phase;
        _galleryVisible = false;

        EnsureCameraFeedActive();
        StopGalleryRoutine();
        
        
        if (!btnGalleryBack || !btnGalleryDelete)
        {
            Debug.Log("[Gallery] Buttons not initialized, calling InitializeGalleryScreen...");
            InitializeGalleryScreen();
        }
        
        PauseForGallery();

        if (painter)
            painter.StopPainting();
        TogglePlaneMesh(false); 
        ClearGalleryPreviews();

        SetPhase(Phase.Gallery);

        SetTip("Loading gallery...");
        _galleryRoutine = CoroutineRunner.Run(BuildGalleryRoutine(ownerEmail, forceCreateAnchors));
    }

    public void HideGalleryPreviews()
    {
        StopGalleryRoutine();
        ClearGalleryPreviews();
        RestoreAfterGallery();
        SetTip("Gallery hidden.");
    }

    void ClearGalleryPreviews()
    {
        StopGalleryRoutine();

        foreach (var anchor in _galleryAnchors)
        {
            if (anchor)
                Destroy(anchor.gameObject);
        }
        _galleryAnchors.Clear();

        foreach (var go in _galleryPreviews)
        {
            if (go)
                Destroy(go);
        }
        _galleryPreviews.Clear();
        _lastSpawnedData = null;
        _galleryVisible = false;
        SetGalleryLoading(false);
    }

    void RestoreAfterGallery()
    {
        if (planeManager)
        {
            planeManager.requestedDetectionMode =
                _planeManagerPrevDetectionMode == PlaneDetectionMode.None
                    ? PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical
                    : _planeManagerPrevDetectionMode;
            planeManager.enabled = _planeManagerWasEnabled;
        }

        if (reticle && _reticleWasActive)
        {
            reticle.gameObject.SetActive(true);
            if (reticle.reticleUI)
                reticle.reticleUI.enabled = _reticleUIWasEnabled;
        }

        RestoreGalleryUI();
        ShowGalleryScreen(false);

        
        switch (_phaseBeforeGallery)
        {
            case Phase.Scanning:
                EnablePlaneManager();
                if (planeManager)
                    planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
                if (reticle) reticle.gameObject.SetActive(true);
                SetPhase(Phase.Scanning);
                break;
            case Phase.PlaneSelected:
            case Phase.Painting:
                EnablePlaneManager();
                if (reticle) reticle.gameObject.SetActive(true);
                SetPhase(_phaseBeforeGallery);
                break;
            default:
                SetPhase(Phase.Idle);
                break;
        }
    }

    IEnumerator BuildGalleryRoutine(string ownerEmail, bool forceCreateAnchors)
    {
        
        System.Exception fatalError = null;
        var impl = BuildGalleryRoutineImpl(ownerEmail, forceCreateAnchors);

        while (true)
        {
            bool moveNext;
            try
            {
                moveNext = impl.MoveNext();
            }
            catch (Exception ex)
            {
                fatalError = ex;
                break;
            }

            if (!moveNext) break;
            yield return impl.Current;
        }

        if (fatalError != null)
        {
            Debug.LogError($"[Gallery] Unexpected failure while building gallery: {fatalError}");
            _galleryVisible = false;
            ClearGalleryPreviews();
            SetTip("Gallery unavailable. Returning to AR view.");
            RestoreAfterGallery();
        }

        _galleryRoutine = null;
    }

    IEnumerator BuildGalleryRoutineImpl(string ownerEmail, bool forceCreateAnchors)
    {
        
        EnsureCameraFeedActive();
        yield return WaitForTrackingReady(3f);

        if (ARSession.state != ARSessionState.SessionTracking)
        {
            SetTip("Move phone to resume tracking before opening gallery.");
            RestoreAfterGallery();
            yield break;
        }

        if (_repo == null)
        {
            SetTip("No saved graffiti yet.");
            RestoreAfterGallery();
            yield break;
        }

        IReadOnlyList<GraffitiData> items = null;
        try
        {
            items = _repo.AllForOwner(ownerEmail);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Gallery] Failed to read repository: {ex.Message}");
            SetTip("Gallery unavailable. Returning to AR view.");
            RestoreAfterGallery();
            yield break;
        }

        if (items == null || items.Count == 0)
        {
            Debug.Log("[Gallery] No entries for owner; aborting gallery build.");
            SetTip("No saved graffiti yet.");
            RestoreAfterGallery();
            yield break;
        }

        Debug.Log($"[Gallery] Building {items.Count} previews (forceCreateAnchors={forceCreateAnchors})");

        
        System.Exception failure = null;

        bool createAnchors = forceCreateAnchors && anchorManager != null;
        int spawned = 0;
        bool anySkippedForDistance = false;

        foreach (var data in items)
        {
            try
            {
                if (!IsFinite(data.position) || !IsFinite(data.localScale))
                {
                    Debug.LogWarning($"[Gallery] Skipping {data.id} with invalid transform values");
                    continue;
                }

                if (!IsWithinGalleryRange(data))
                {
                    anySkippedForDistance = true;
                    Debug.Log($"[Gallery] Skipping {data.id} because it is farther than {galleryMaxDistanceMeters:F1}m from the camera.");
                    continue;
                }

                var tex = LoadTextureFromDisk(data.thumbPath, data.pngPath);
                if (tex)
                {
                    var quad = SpawnPreviewQuad(data, tex, parentOverride: null, createAnchor: createAnchors);
                    if (quad)
                    {
                        _galleryPreviews.Add(quad);
                        _lastSpawnedData = data;
                        spawned++;
                    }
                    else
                    {
                        Debug.LogWarning($"[Gallery] SpawnPreviewQuad returned null for {data.id}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[Gallery] Missing texture for {data.id}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Gallery] Failed to spawn preview for {data.id}: {ex.Message}");
                failure = ex;
                break;
            }

        
            yield return null;
        }

        _galleryVisible = _galleryPreviews.Count > 0;

        if (failure != null)
        {
            SetTip("Gallery unavailable. Returning to AR view.");
            RestoreAfterGallery();
            SetGalleryLoading(false);
            yield break;
        }

        if (!_galleryVisible)
        {
            Debug.LogWarning("[Gallery] No previews were created; returning to AR view.");
            if (anySkippedForDistance)
                SetTip("No nearby graffiti found.");
            else
                SetTip("No saved graffiti yet.");
            RestoreAfterGallery();
            yield break;
        }

        Debug.Log($"[Gallery] Spawned {spawned} previews.");
        SetTip("Showing saved graffiti in AR.");
        SetGalleryLoading(false);
    }

    bool IsFinite(Vector3 v)
    {
        return float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }

    bool IsWithinGalleryRange(GraffitiData data)
    {
        if (data == null) return false;
        var cam = Camera.main;
        if (!cam) return true;

        float distance = Vector3.Distance(cam.transform.position, data.position);
        return distance <= galleryMaxDistanceMeters;
    }

    void StopGalleryRoutine()
    {
        if (_galleryRoutine != null)
        {
            CoroutineRunner.Stop(_galleryRoutine);
            _galleryRoutine = null;
        }
    }

    ARAnchor CreateWorldAnchor(Pose pose)
    {
        if (!anchorManager)
        {
            Debug.LogWarning("[Gallery] No ARAnchorManager available to create anchors.");
            return null;
        }
        ARAnchor anchor = null;
       
        var methods = anchorManager.GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        foreach (var method in methods)
        {
            if (!string.Equals(method.Name, "TryAddAnchor", StringComparison.Ordinal))
                continue;

            var parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Pose))
            {
                anchor = method.Invoke(anchorManager, new object[] { pose }) as ARAnchor;
            }
            else if (parameters.Length == 2 &&
                     parameters[0].ParameterType == typeof(Pose) &&
                     parameters[1].ParameterType == typeof(ARAnchor).MakeByRefType())
            {
                object[] args = { pose, null };
                var result = method.Invoke(anchorManager, args);
                var candidate = args[1] as ARAnchor;
                bool success = (result is bool b && b) || candidate != null;
                if (success)
                    anchor = candidate;
            }

            if (anchor)
                break;
        }

        if (!anchor)
        {
            var addAnchor = anchorManager.GetType().GetMethod("AddAnchor", new[] { typeof(Pose) });
            if (addAnchor != null)
                anchor = addAnchor.Invoke(anchorManager, new object[] { pose }) as ARAnchor;
        }
        if (anchor)
        {
            return anchor;
        }

        var go = new GameObject("WorldAnchor");
        go.transform.SetPositionAndRotation(pose.position, pose.rotation);
        go.transform.SetParent(anchorManager.transform, worldPositionStays: true);
        anchor = go.AddComponent<ARAnchor>();

        if (!anchor || !anchor.enabled)
            Debug.LogWarning("[Gallery] Falling back to untracked world anchor.");

        return anchor;
    }

    void EnsureTrackingActiveForGallery()
    {
        if (arSession && !arSession.enabled)
            arSession.enabled = true;

        if (cameraManager && !cameraManager.enabled)
            cameraManager.enabled = true;

        if (planeManager)
        {
            planeManager.enabled = true;
        }
    }

    void EnsureCameraFeedActive()
    {
        if (cameraManager)
        {
            var cam = cameraManager.GetComponent<Camera>();
            if (cam && !cam.enabled) cam.enabled = true;
            var bg = cameraManager.GetComponent<UnityEngine.XR.ARFoundation.ARCameraBackground>();
            if (bg && !bg.enabled) bg.enabled = true;
        }
        EnsureTrackingActiveForGallery();
    }

    void PauseForGallery()
    {
        if (planeManager)
        {
            _planeManagerWasEnabled = planeManager.enabled;
            _planeManagerPrevDetectionMode = planeManager.requestedDetectionMode;
        }

        EnsureTrackingActiveForGallery();

        if (planeManager)
        {
            planeManager.enabled = true;
     
            planeManager.requestedDetectionMode = PlaneDetectionMode.None;
            TogglePlaneMesh(false);
        }

        if (reticle)
        {
            _reticleWasActive = reticle.gameObject.activeSelf;
            _reticleUIWasEnabled = reticle.reticleUI ? reticle.reticleUI.enabled : false;
            if (reticle.reticleUI) reticle.reticleUI.enabled = false;
        }

        _galleryHiddenUI.Clear();
        HideUIForGallery(btnSelectSurface ? btnSelectSurface.gameObject : null);
        HideUIForGallery(btnGraffiti ? btnGraffiti.gameObject : null);
        HideUIForGallery(btnSave ? btnSave.gameObject : null);
        HideUIForGallery(btnColorPalette ? btnColorPalette.gameObject : null);
        HideUIForGallery(btnPaintBrush ? btnPaintBrush.gameObject : null);
        HideUIForGallery(btnGallery ? btnGallery.gameObject : null);
        HideUIForGallery(btnUndo ? btnUndo.gameObject : null);
        HideUIForGallery(btnRedo ? btnRedo.gameObject : null);
        HideUIForGallery(panelTop);
        HideUIForGallery(panelTools);
        HideUIForGallery(panelGraffiti);

        ShowGalleryScreen(true);
    }

    void HideUIForGallery(GameObject go)
    {
        if (!go) return;
       
        if (go.GetComponent<Camera>() || go.GetComponent<ARSession>() || go.GetComponent<ARSessionOrigin>())
            return;

        _galleryHiddenUI[go] = go.activeSelf;
        go.SetActive(false);
    }

    void ShowOnlySelectedPlaneMesh()
    {
        if (!planeManager || !reticle || !reticle.selectedPlane) return;
        var keep = GetRootPlane(reticle.selectedPlane);
        foreach (var p in planeManager.trackables)
        {
            var root = GetRootPlane(p);
            var mr = p.GetComponent<MeshRenderer>();
            bool isKeep = root == keep;
            if (mr) mr.enabled = isKeep;
            p.gameObject.SetActive(isKeep);
        }
    }

    void RestoreGalleryUI()
    {
        foreach (var kvp in _galleryHiddenUI)
        {
            if (kvp.Key)
                kvp.Key.SetActive(kvp.Value);
        }

        _galleryHiddenUI.Clear();
    }

    void DeleteCurrentGalleryItemAndExit()
    {
        if (_lastSpawnedData != null && _repo != null)
        {
            _repo.Delete(_lastSpawnedData.id);
        }

        HideGalleryPreviews();
        CoroutineRunner.Run(RescanRoutine());
        SetTip("Deleted and returning to scan.");
    }

    void EnablePlaneManager()
    {
        if (!planeManager) return;
        if (!planeManager.enabled)
            planeManager.enabled = true;
    }

    Coroutine _notificationRoutine;
    void ShowNotification(string message)
    {
        if (_notificationRoutine != null)
            CoroutineRunner.Stop(_notificationRoutine);
        _notificationRoutine = CoroutineRunner.Run(NotificationRoutine(message));
    }

    IEnumerator NotificationRoutine(string message)
    {
        SetTip(message);
        float elapsed = 0f;
        while (elapsed < notificationSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        _notificationRoutine = null;
    }

    Texture2D LoadTextureFromDisk(string primary, string fallback = null)
    {
        string path = (!string.IsNullOrEmpty(primary) && File.Exists(primary)) ? primary :
            (!string.IsNullOrEmpty(fallback) && File.Exists(fallback) ? fallback : null);

        if (string.IsNullOrEmpty(path)) return null;

        try
        {
            var bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            tex.LoadImage(bytes, markNonReadable: true); 
            return tex;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Gallery] Failed to load texture from {path}: {ex.Message}");
            return null;
        }
    }

    Texture2D CreateThumbnail(Texture2D source, int size)
    {
        var rt = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var thumb = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        thumb.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        thumb.Apply();
        RenderTexture.active = prev;
        rt.Release();
        Destroy(rt);
        return thumb;
    }

    [Header("Preview Rendering")]
    [Tooltip("Material used for in-world graffiti previews; if null, a safe Unlit material is created at runtime.")]
    public Material previewQuadMaterial;

    GameObject SpawnPreviewQuad(GraffitiData data, Texture2D texture, Transform parentOverride = null, bool createAnchor = false)
    {
        if (texture == null) return null;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "GraffitiPreview_" + data.id;

        
        if (painter && painter.strokesRoot)
            quad.layer = painter.strokesRoot.gameObject.layer;

        Transform parent = parentOverride;
        ARAnchor anchor = null;
        if (createAnchor && anchorManager)
        {
            Pose pose = new Pose(data.position, data.rotation);
            anchor = CreateWorldAnchor(pose);

            if (anchor)
            {
                parent = anchor.transform;
                _galleryAnchors.Add(anchor);
            }
        }

        if (!parent)
            parent = _currentAnchor ? _currentAnchor.transform : (painter && painter.lockedPlane ? painter.lockedPlane.transform : null);

        
        if (anchor && anchor.transform)
        {
            quad.transform.SetParent(anchor.transform, false);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.identity;
        }
        else
        {
            if (parent)
                quad.transform.SetParent(parent, worldPositionStays: true);
            quad.transform.position = data.position;
            quad.transform.rotation = data.rotation;
        }

        var targetScale = data.localScale == Vector3.zero ? Vector3.one : data.localScale;
        quad.transform.localScale = targetScale;

        
        if (!anchor && anchorManager && !quad.GetComponent<ARAnchor>())
            quad.AddComponent<ARAnchor>();

        Debug.Log($"[Gallery] Preview {data.id} at {data.position} rot {data.rotation.eulerAngles} scale {data.localScale}");

        var mr = quad.GetComponent<MeshRenderer>();

        
        var collider = quad.GetComponent<Collider>();
        if (collider) Destroy(collider);

        Material mat = null;
        if (previewQuadMaterial && previewQuadMaterial.shader)
        {
            mat = new Material(previewQuadMaterial);
        }
        else
        {
       
            string[] shaderNames =
            {
                "Unlit/Texture",
                "Unlit/Transparent",
                "Sprites/Default",
                "UI/Default",
                "Universal Render Pipeline/Unlit",
                "Standard"
            };

            Shader shader = null;
            foreach (var name in shaderNames)
            {
                shader = Shader.Find(name);
                if (shader) break;
            }

            if (shader)
            {
                mat = new Material(shader);
            }
            else if (mr && mr.sharedMaterial && mr.sharedMaterial.shader)
            {
                mat = new Material(mr.sharedMaterial);
            }
        }

        if (mat)
        {
            mat.mainTexture = texture;
       
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.white);
            }
            mr.material = mat;
        }
        else
        {
            Debug.LogWarning("[SpawnPreviewQuad] Unable to create material for preview quad; using default renderer material.");
            if (mr && mr.material) mr.material.mainTexture = texture;
        }

        return quad;
    }

   
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

        float buttonWidth = selectRect.sizeDelta.x > 0 ? selectRect.sizeDelta.x : 512f;
        float buttonHeight = selectRect.sizeDelta.y > 0 ? selectRect.sizeDelta.y : 80f;
        float spacing = 40f; 


        RectTransform parentRect = selectRect.parent as RectTransform;
        if (parentRect == null)
        {
            Debug.LogWarning("[InitializeUndoRedoButtons] Parent RectTransform not found!");
            return;
        }

        
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
            centerY = selectRect.anchoredPosition.y;
        }
        
        float panelWidth = parentRect.rect.width;
        float selectSurfaceLeftEdge = (panelWidth - buttonWidth) * 0.5f;

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

                UnityEngine.UI.LayoutElement layoutElement = btnUndo.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = btnUndo.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                }
                layoutElement.ignoreLayout = true;


                undoRect.anchorMin = buttonAnchorMin;
                undoRect.anchorMax = buttonAnchorMax;
                undoRect.pivot = buttonPivot;


                undoRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);


                undoRect.anchoredPosition = new Vector2(
                    selectSurfaceLeftEdge - buttonWidth - spacing,
                    centerY
                );


                Image undoImage = btnUndo.GetComponent<Image>();
                if (undoImage != null && undoImage.sprite == null)
                {
                    SetButtonIcon(btnUndo, "undo");
                }


                btnUndo.interactable = true;


                btnUndo.onClick.RemoveAllListeners();
                btnUndo.onClick.AddListener(() => {
                    CoroutineRunner.Run(ButtonClickFeedback(btnUndo));
                    HandleUndoAction();
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
                
                UnityEngine.UI.LayoutElement layoutElement = btnRedo.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = btnRedo.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                }
                layoutElement.ignoreLayout = true;

                
                redoRect.anchorMin = buttonAnchorMin;
                redoRect.anchorMax = buttonAnchorMax;
                redoRect.pivot = buttonPivot;

                
                redoRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);

               
                redoRect.anchoredPosition = new Vector2(
                    selectSurfaceLeftEdge + buttonWidth + spacing,
                    centerY
                );

               
                Image redoImage = btnRedo.GetComponent<Image>();
                if (redoImage != null && redoImage.sprite == null)
                {
                    SetButtonIcon(btnRedo, "redo");
                }

               
                btnRedo.interactable = true;

               
                btnRedo.onClick.RemoveAllListeners(); 
                btnRedo.onClick.AddListener(() => {
                    CoroutineRunner.Run(ButtonClickFeedback(btnRedo));
                    HandleRedoAction();
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

    void InitializeGalleryButton()
    {

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

        galleryRect.anchorMin = new Vector2(0f, 0.5f);
        galleryRect.anchorMax = new Vector2(0f, 0.5f);
        galleryRect.pivot = new Vector2(0.5f, 0.5f);
        galleryRect.sizeDelta = brushRect.sizeDelta;

        
        RectTransform parentRect = panelGraffiti != null ? panelGraffiti.GetComponent<RectTransform>() : null;
        float panelWidth = parentRect != null ? parentRect.rect.width : 0f;


        float minMarginPercent = 0.03f; 
        float buttonSize = brushRect.sizeDelta.x;
        float minMargin = panelWidth > 0 ? panelWidth * minMarginPercent : 30f;


        galleryRect.anchoredPosition = new Vector2(
            minMargin + buttonSize * 0.5f,
            0f
        );

        Image galleryImage = btnGallery.GetComponent<Image>();
        if (galleryImage != null && galleryImage.sprite == null)
        {
            SetButtonIcon(btnGallery, "gallery");
        }

        
        btnGallery.onClick.RemoveAllListeners();
        btnGallery.onClick.AddListener(() => {
            CoroutineRunner.Run(ButtonClickFeedback(btnGallery));
            OpenGallery();
        });

        btnGallery.interactable = false;
    }

    void InitializeGalleryScreen()
    {
        Debug.Log("[Gallery] InitializeGalleryScreen called");
        
        
        if (!panelGalleryScreen)
        {
        
            GameObject go = GameObject.Find("Panel_Gallery");
            if (go == null)
            {
        
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (obj.name == "Panel_Gallery" && obj.scene.isLoaded)
                    {
                        go = obj;
                        break;
                    }
                }
            }
            
            if (go)
            {
                panelGalleryScreen = go;
                Debug.Log($"[Gallery] Found Panel_Gallery in scene: {go.name}, Active: {go.activeSelf}, Scene: {go.scene.name}");
            }
            else
            {
                Debug.LogWarning("[Gallery] Panel_Gallery not found in any loaded scene, will create dynamically if needed");
            }
        }
        
        
        if (!panelGalleryScreen)
        {
            Debug.Log("[Gallery] Creating Panel_Gallery dynamically...");
            panelGalleryScreen = BuildRuntimeGalleryPanel();
        }

        if (!btnGalleryBack && panelGalleryScreen)
        {
            var back = panelGalleryScreen.transform.Find("Button_Back");
            if (back)
            {
                btnGalleryBack = back.GetComponent<Button>();
                Debug.Log($"[Gallery] Found Button_Back in Panel_Gallery: {back.name}, Parent: {back.parent?.name}");
            }
        }

        if (!btnGalleryDelete && panelGalleryScreen)
        {
            var del = panelGalleryScreen.transform.Find("Button_Delete");
            if (del)
            {
                btnGalleryDelete = del.GetComponent<Button>();
                Debug.Log($"[Gallery] Found Button_Delete in Panel_Gallery: {del.name}, Parent: {del.parent?.name}");
            }
        }

        
        if (btnGalleryBack)
        {
            var backIcon = btnGalleryBack.transform.Find("Icon")?.GetComponent<Image>();
            var backText = btnGalleryBack.transform.Find("Text")?.GetComponent<Text>();
            if (backIcon != null)
            {
                SetButtonIconOnImage(backIcon, "back");
                Debug.Log($"[Gallery] Set icon for Button_Back, Icon sprite: {backIcon.sprite?.name ?? "null"}, Icon active: {backIcon.gameObject.activeSelf}");
        
                if (backText != null)
                {
                    backText.gameObject.SetActive(false);
                    Debug.Log("[Gallery] Hid Text for Button_Back (using Icon instead)");
                }
            }
            else
            {
                Debug.LogWarning("[Gallery] Button_Back Icon not found!");
            }
        }

        if (btnGalleryDelete)
        {
            var delIcon = btnGalleryDelete.transform.Find("Icon")?.GetComponent<Image>();
            var delText = btnGalleryDelete.transform.Find("Text")?.GetComponent<Text>();
            if (delIcon != null)
            {
                SetButtonIconOnImage(delIcon, "bin");
                Debug.Log($"[Gallery] Set icon for Button_Delete, Icon sprite: {delIcon.sprite?.name ?? "null"}, Icon active: {delIcon.gameObject.activeSelf}");
        
                if (delText != null)
                {
                    delText.gameObject.SetActive(false);
                    Debug.Log("[Gallery] Hid Text for Button_Delete (using Icon instead)");
                }
            }
            else
            {
                Debug.LogWarning("[Gallery] Button_Delete Icon not found!");
            }
        }

        if (!galleryLoadingIndicator && panelGalleryScreen)
        {
            var loader = panelGalleryScreen.transform.Find("Loading");
            if (loader) galleryLoadingIndicator = loader.gameObject;
        }

        if (btnGalleryBack)
        {
            btnGalleryBack.onClick.RemoveAllListeners();
            btnGalleryBack.onClick.AddListener(() =>
            {
                CoroutineRunner.Run(ButtonClickFeedback(btnGalleryBack));
                HideGalleryPreviews();
            });
        }

        if (btnGalleryDelete)
        {
            btnGalleryDelete.onClick.RemoveAllListeners();
            btnGalleryDelete.onClick.AddListener(() =>
            {
                CoroutineRunner.Run(ButtonClickFeedback(btnGalleryDelete));
                DeleteCurrentGalleryItemAndExit();
            });
        }

        
        ShowGalleryScreen(false);
    }

    void ShowGalleryScreen(bool visible)
    {
        Debug.Log($"[Gallery] ShowGalleryScreen({visible}), panelGalleryScreen: {panelGalleryScreen?.name ?? "null"}, btnGalleryBack: {btnGalleryBack?.name ?? "null"}, btnGalleryDelete: {btnGalleryDelete?.name ?? "null"}");
        if (panelGalleryScreen)
            panelGalleryScreen.SetActive(visible);

        if (btnGalleryBack)
        {
            btnGalleryBack.gameObject.SetActive(visible);
            Debug.Log($"[Gallery] Button_Back set to active: {visible}, Parent: {btnGalleryBack.transform.parent?.name}");
        }
        if (btnGalleryDelete)
        {
            btnGalleryDelete.gameObject.SetActive(visible);
            Debug.Log($"[Gallery] Button_Delete set to active: {visible}, Parent: {btnGalleryDelete.transform.parent?.name}");
        }

        SetGalleryLoading(visible);
    }

    void SetGalleryLoading(bool loading)
    {
        if (galleryLoadingIndicator)
            galleryLoadingIndicator.SetActive(loading);
    }

    GameObject BuildRuntimeGalleryPanel()
    {
        
        Transform parent = panelTop ? panelTop.transform.parent : null;
        if (parent == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas) parent = canvas.transform;
        }

        
        GameObject existingPanel = GameObject.Find("Panel_Gallery");
        if (existingPanel == null)
        {
        
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj.name == "Panel_Gallery" && obj.scene.isLoaded)
                {
                    existingPanel = obj;
                    break;
                }
            }
        }
        
        if (existingPanel != null)
        {
            Debug.Log($"[Gallery] Using existing Panel_Gallery: {existingPanel.name}, Active: {existingPanel.activeSelf}, Scene: {existingPanel.scene.name}, Parent: {existingPanel.transform.parent?.name ?? "None"}");
        
            var existingBack = existingPanel.transform.Find("Button_Back");
            var existingDel = existingPanel.transform.Find("Button_Delete");
            var existingLoading = existingPanel.transform.Find("Loading");

            if (existingBack != null)
            {
                Debug.Log($"[Gallery] Found Button_Back in existing panel: {existingBack.name}, Has Icon: {existingBack.Find("Icon") != null}, Has Text: {existingBack.Find("Text") != null}");
                btnGalleryBack = existingBack.GetComponent<Button>();
               
                var existingBackImg = existingBack.GetComponent<Image>();
                if (existingBackImg != null)
                {
                    existingBackImg.color = new Color(1f, 1f, 1f, 0f); 
                }
                if (btnGalleryBack != null)
                {
                    btnGalleryBack.onClick.RemoveAllListeners();
                    btnGalleryBack.onClick.AddListener(() =>
                    {
                        CoroutineRunner.Run(ButtonClickFeedback(btnGalleryBack));
                        HideGalleryPreviews();
                    });
                }
            }
            else
            {
                Debug.LogWarning("[Gallery] Button_Back not found in existing Panel_Gallery");
            }

            if (existingDel != null)
            {
                Debug.Log($"[Gallery] Found Button_Delete in existing panel: {existingDel.name}, Has Icon: {existingDel.Find("Icon") != null}, Has Text: {existingDel.Find("Text") != null}");
                btnGalleryDelete = existingDel.GetComponent<Button>();
                
                var existingDelImg = existingDel.GetComponent<Image>();
                if (existingDelImg != null)
                {
                    existingDelImg.color = new Color(0.9f, 0.2f, 0.2f, 0f); 
                }
                if (btnGalleryDelete != null)
                {
                    btnGalleryDelete.onClick.RemoveAllListeners();
                    btnGalleryDelete.onClick.AddListener(() =>
                    {
                        CoroutineRunner.Run(ButtonClickFeedback(btnGalleryDelete));
                        DeleteCurrentGalleryItemAndExit();
                    });
                }
            }
            else
            {
                Debug.LogWarning("[Gallery] Button_Delete not found in existing Panel_Gallery");
            }

            if (existingLoading != null)
            {
                galleryLoadingIndicator = existingLoading.gameObject;
            }

            return existingPanel;
        }
        else
        {
            Debug.Log("[Gallery] Panel_Gallery not found, creating dynamically");
        }

        
        var panel = new GameObject("Panel_Gallery", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        if (parent) panel.transform.SetParent(parent, false);

        var img = panel.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.35f); 

        
        var back = new GameObject("Button_Back", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var backRt = back.GetComponent<RectTransform>();
        backRt.sizeDelta = new Vector2(200f, 80f);
        backRt.anchorMin = new Vector2(0f, 1f);
        backRt.anchorMax = new Vector2(0f, 1f);
        backRt.pivot = new Vector2(0f, 1f);
        backRt.anchoredPosition = new Vector2(40f, -40f);
        back.transform.SetParent(panel.transform, false);

        var backImg = back.GetComponent<Image>();
        backImg.color = new Color(1f, 1f, 1f, 0f);
        backImg.type = Image.Type.Simple;
        backImg.preserveAspect = false;
        backImg.useSpriteMesh = false;

        var backTextGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        backTextGO.transform.SetParent(back.transform, false);
        var backText = backTextGO.GetComponent<Text>();
        backText.text = "Back";
        backText.alignment = TextAnchor.MiddleCenter;
        backText.color = Color.black;
        backText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var backTextRt = backTextGO.GetComponent<RectTransform>();
        backTextRt.anchorMin = Vector2.zero;
        backTextRt.anchorMax = Vector2.one;
        backTextRt.offsetMin = Vector2.zero;
        backTextRt.offsetMax = Vector2.zero;

     
        var backIconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var backIconRt = backIconGO.GetComponent<RectTransform>();
        backIconRt.SetParent(back.transform, false);
        backIconRt.anchorMin = Vector2.zero;
        backIconRt.anchorMax = Vector2.one;
        backIconRt.offsetMin = Vector2.zero;
        backIconRt.offsetMax = Vector2.zero;
        var backIconImg = backIconGO.GetComponent<Image>();
        backIconImg.color = Color.white;
        backIconImg.type = Image.Type.Simple;
        backIconImg.useSpriteMesh = true;
        backIconImg.preserveAspect = true;
        
        backTextGO.SetActive(false);

     
        var loading = new GameObject("Loading", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var loadingRt = loading.GetComponent<RectTransform>();
        loadingRt.anchorMin = new Vector2(0.5f, 0.5f);
        loadingRt.anchorMax = new Vector2(0.5f, 0.5f);
        loadingRt.pivot = new Vector2(0.5f, 0.5f);
        loadingRt.anchoredPosition = Vector2.zero;
        loading.transform.SetParent(panel.transform, false);
        var loadingText = loading.GetComponent<Text>();
        loadingText.text = "Loading...";
        loadingText.alignment = TextAnchor.MiddleCenter;
        loadingText.fontSize = 32;
        loadingText.color = Color.white;
        loadingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        panel.SetActive(false);
        galleryLoadingIndicator = loading;
        btnGalleryBack = back.GetComponent<Button>();
     
        var backIconImage = back.transform.Find("Icon")?.GetComponent<Image>();
        if (backIconImage != null)
        {
            SetButtonIconOnImage(backIconImage, "back");
        }
        btnGalleryBack.onClick.RemoveAllListeners();
        btnGalleryBack.onClick.AddListener(() =>
        {
            CoroutineRunner.Run(ButtonClickFeedback(btnGalleryBack));
            HideGalleryPreviews();
        });

     
        var del = new GameObject("Button_Delete", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var delRt = del.GetComponent<RectTransform>();
        delRt.sizeDelta = new Vector2(200f, 80f);
        delRt.anchorMin = new Vector2(1f, 1f);
        delRt.anchorMax = new Vector2(1f, 1f);
        delRt.pivot = new Vector2(1f, 1f);
        delRt.anchoredPosition = new Vector2(-40f, -40f);
        del.transform.SetParent(panel.transform, false);
        var delImg = del.GetComponent<Image>();
        delImg.color = new Color(0.9f, 0.2f, 0.2f, 0f);
        delImg.type = Image.Type.Simple;
        delImg.preserveAspect = false;
        delImg.useSpriteMesh = false;
        var delTextGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        delTextGO.transform.SetParent(del.transform, false);
        var delText = delTextGO.GetComponent<Text>();
        delText.text = "Delete";
        delText.alignment = TextAnchor.MiddleCenter;
        delText.color = Color.white;
        delText.fontSize = 24;
        delText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var delTextRt = delTextGO.GetComponent<RectTransform>();
        delTextRt.anchorMin = Vector2.zero;
        delTextRt.anchorMax = Vector2.one;
        delTextRt.offsetMin = Vector2.zero;
        delTextRt.offsetMax = Vector2.zero;

     
        var delIconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var delIconRt = delIconGO.GetComponent<RectTransform>();
        delIconRt.SetParent(del.transform, false);
        delIconRt.anchorMin = Vector2.zero;
        delIconRt.anchorMax = Vector2.one;
        delIconRt.offsetMin = Vector2.zero;
        delIconRt.offsetMax = Vector2.zero;
        var delIconImg = delIconGO.GetComponent<Image>();
        delIconImg.color = Color.white;
        delIconImg.type = Image.Type.Simple;
        delIconImg.useSpriteMesh = true;
        delIconImg.preserveAspect = true;
        
        delTextGO.SetActive(false);

        btnGalleryDelete = del.GetComponent<Button>();
        
        var delIconImage = del.transform.Find("Icon")?.GetComponent<Image>();
        if (delIconImage != null)
        {
            SetButtonIconOnImage(delIconImage, "bin");
        }
        btnGalleryDelete.onClick.RemoveAllListeners();
        btnGalleryDelete.onClick.AddListener(() =>
        {
            CoroutineRunner.Run(ButtonClickFeedback(btnGalleryDelete));
            DeleteCurrentGalleryItemAndExit();
        });

        return panel;
    }

    
    void SetButtonIcon(Button button, string iconName)
    {
        if (!button) return;

        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage == null)
        {
            Debug.LogWarning($"Button {button.name} does not have an Image component");
            return;
        }

    
        Sprite sprite = Resources.Load<Sprite>($"Textures/{iconName}");

    
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
    
        if (sprite == null)
        {
            string assetPath = $"Assets/Textures/{iconName}.png";
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
    
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
            buttonImage.type = Image.Type.Simple;
            buttonImage.useSpriteMesh = true;
            buttonImage.preserveAspect = true;
            buttonImage.alphaHitTestMinimumThreshold = 0.1f; 
        }
        else
        {
            Debug.LogWarning($"Could not load icon: {iconName}.png. Please ensure the file exists in Assets/Textures/ or Resources/Textures/");
        }
    }

        void SetButtonIconOnImage(Image image, string iconName)
    {
        if (!image) return;

   
        Sprite sprite = Resources.Load<Sprite>($"Textures/{iconName}");

   
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
        
        if (sprite == null)
        {
            string assetPath = $"Assets/Textures/{iconName}.png";
            sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
        
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
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.useSpriteMesh = true;
            image.preserveAspect = true;
            image.color = Color.white;
            image.material = null;
        }
        else
        {
            Debug.LogWarning($"Could not load icon: {iconName}.png. Please ensure the file exists in Assets/Textures/ or Resources/Textures/");
        }
    }

    void UpdateUndoRedoButtonsVisibility()
    {
        bool shouldShow = false;

        bool hasVisibleStrokes = painter ? painter.HasVisibleStrokes : HasGraffitiStrokes();

        if (_phase == Phase.Painting)
        {
            shouldShow = true;
        }
        else if (_phase == Phase.PlaneSelected)
        {
            shouldShow = hasVisibleStrokes;
        }

        bool canUndo = painter ? painter.CanUndo : hasVisibleStrokes;
        bool canRedo = painter ? painter.CanRedo : false;

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

    void HandleUndoAction()
    {
        if (!painter) return;
        painter.UndoLastStroke();
        UpdateUndoRedoButtonsVisibility();
    }

    void HandleRedoAction()
    {
        if (!painter) return;
        painter.RedoStroke();
        UpdateUndoRedoButtonsVisibility();
    }

    
    public void ToggleToolPanel()
    {
        if (panelTools != null)
        {
    
            bool isCurrentlyActive = panelTools.activeSelf;
            panelTools.SetActive(!isCurrentlyActive);
        }
        else
        {
            GameObject toolsObj = GameObject.Find("Panel_Tools");
            if (toolsObj != null)
            {
                bool isCurrentlyActive = toolsObj.activeSelf;
                toolsObj.SetActive(!isCurrentlyActive);
                panelTools = toolsObj; 
            }
        }
    }

    // ========================= PLANE EVENTS/VISUALS =========================
    void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
    {
        if (_phase != Phase.Scanning) return;

        if (_primaryScanPlane)
            ShowOnlyPlane(_primaryScanPlane);
        else
            TogglePlaneMesh(true); 
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

    void HideAllOtherPlanes(ARPlane keep)
    {
        if (!planeManager) return;

        var keepRoot = GetRootPlane(keep);
        foreach (var p in planeManager.trackables)
        {
            var root = GetRootPlane(p);
            if (root == keepRoot)
            {
                var mr = p.GetComponent<MeshRenderer>();
                if (mr) mr.enabled = true;
            }
            else
            {
                p.gameObject.SetActive(false);
            }
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
            if (visible && !p.gameObject.activeSelf)
                p.gameObject.SetActive(true);

            var mr = p.GetComponent<MeshRenderer>();
            if (mr) mr.enabled = visible;
        }
    }

    
    void BuildFrozenBorder()
    {
        DestroyFrozenBorder();
        var plane = reticle.selectedPlane;
        if (!plane) return;

        var boundary = CopyBoundary(plane);
        if (boundary == null || boundary.Length < 3) return;

        _frozenBorderGO = new GameObject("FrozenPlaneBorder");

        
        Transform parentTransform = _currentAnchor ? _currentAnchor.transform : plane.transform;
        _frozenBorderGO.transform.SetParent(parentTransform, worldPositionStays: false);

        
        if (_currentAnchor)
        {
        
            Pose planePoseInWorld = new Pose(plane.transform.position, plane.transform.rotation);
            Pose anchorPoseInWorld = new Pose(_currentAnchor.transform.position, _currentAnchor.transform.rotation);

        
            Quaternion invAnchorRot = Quaternion.Inverse(anchorPoseInWorld.rotation);
            Vector3 invAnchorPos = invAnchorRot * -anchorPoseInWorld.position;
            Pose inverseAnchorPose = new Pose(invAnchorPos, invAnchorRot);

        
            Pose planePoseInAnchorSpace = inverseAnchorPose.Multiply(planePoseInWorld); 

            _frozenBorderGO.transform.localPosition = planePoseInAnchorSpace.position;
            _frozenBorderGO.transform.localRotation = planePoseInAnchorSpace.rotation;
        }
        

        var lr = _frozenBorderGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = frozenLineWidth;

        
        lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); 
        lr.material.color = frozenLineColor;

        
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(frozenLineColor, 0.0f), new GradientColorKey(frozenLineColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(frozenLineColor.a, 0.0f), new GradientAlphaKey(frozenLineColor.a, 1.0f) }
        );
        lr.colorGradient = gradient;


        lr.positionCount = boundary.Length;
        for (int i = 0; i < boundary.Length; i++)
        
            lr.SetPosition(i, new Vector3(boundary[i].x, 0f, boundary[i].y));
    }

    void DestroyFrozenBorder()
    {
        if (_frozenBorderGO) Destroy(_frozenBorderGO);
        _frozenBorderGO = null;
    }

    static string SanitizeForPath(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        foreach (var ch in Path.GetInvalidFileNameChars())
            raw = raw.Replace(ch.ToString(), "_");
        return raw.Replace("@", "_at_");
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
        
        var img = btnGraffiti.GetComponent<Image>();
        if (img) img.color = on ? new Color(0.08f, 0.8f, 0.4f, 0.9f) : new Color(1f, 1f, 1f, 0.25f); 

        
        var txt = btnGraffiti.GetComponentInChildren<TMPro.TMP_Text>();
        if (txt) txt.text = on ? "Graffiti (ON)" : "Graffiti";

        
        var animator = btnGraffiti.GetComponent<Animator>();
        if (animator)
        {
        
            animator.SetBool("IsOn", on);
        
     
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
