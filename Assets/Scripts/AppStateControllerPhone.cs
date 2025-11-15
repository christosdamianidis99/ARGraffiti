using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public enum Phase { Idle, Scanning, PlaneSelected, Painting }

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
    public GameObject panelTools;
    public TMP_Text txtTips;

    [Header("Painting")]
    public Transform strokesRoot;

    // State
    private Phase _phase = Phase.Idle;
    private ARAnchor _currentAnchor;

    // Single-plane scanning
    private ARPlane _primaryScanPlane;
    private double _reticleStableStart = -1;
    private const double STABLE_DWELL_SECONDS = 0.20;

    // Frozen outline
    private GameObject _frozenBorderGO;
    public float frozenLineWidth = 0.01f;
    public Color frozenLineColor = new Color(0f, 1f, 0.8f, 0.9f);

    // Save/Load helpers
    private bool _isSaving = false;
    private readonly List<ARRaycastHit> _loadHits = new();
    void OnEnable()
    {
        if (planeManager) planeManager.trackablesChanged.AddListener(OnPlanesChanged);
    }
    void OnDisable()
    {
        if (planeManager) planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);
    }


    private void Awake()
    {
        btnScan.onClick.AddListener(() => StartCoroutine(RescanRoutine()));
        btnSelectSurface.onClick.AddListener(SelectSurfaceUnderReticle);
        btnGraffiti.onClick.AddListener(ToggleGraffiti);
        btnSave.onClick.AddListener(OnSaveButtonPressed);

        SetPhase(Phase.Idle);
    }

    private IEnumerator Start()
    {
        while (ARSession.state != ARSessionState.SessionTracking)
            yield return null;

        yield return LoadSavedGraffitiCoroutine();
    }

    // -------- Phase & UI
    private void SetPhase(Phase p)
    {
        _phase = p;

        if (panelTools) panelTools.SetActive(false);
        if (btnSave) btnSave.gameObject.SetActive(false);
        if (btnSelectSurface) btnSelectSurface.interactable = false;
        if (btnGraffiti) btnGraffiti.interactable = false;

        if (_phase != Phase.Scanning) TogglePlaneMesh(false);

        switch (_phase)
        {
            case Phase.Idle:
                if (planeManager) planeManager.enabled = false;
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
                if (planeManager)
                {
                    planeManager.requestedDetectionMode = PlaneDetectionMode.None;
                    planeManager.enabled = false;
                }
                BuildFrozenBorder();
                if (panelTools) panelTools.SetActive(true);
                if (btnSave) btnSave.gameObject.SetActive(true);
                if (btnGraffiti) btnGraffiti.interactable = true;
                SetTip("Press Graffiti to start/stop painting.");
                break;
            case Phase.Painting:
                if (panelTools) panelTools.SetActive(true);
                if (btnSave) btnSave.gameObject.SetActive(true);
                if (btnGraffiti) btnGraffiti.interactable = true;
                painter.StartPainting();
                SetTip("Graffiti ON. Keep the dot on the surface and move phone.");
                break;
        }
        StyleGraffitiButton(_phase == Phase.Painting);
    }

    private void Update()
    {
        if (_phase != Phase.Scanning || reticle == null) return;

        if (btnSelectSurface) btnSelectSurface.interactable = reticle.isOverAnyPlane;

        if (_primaryScanPlane == null)
        {
            if (reticle.isOverAnyPlane && reticle.planeUnderReticle != null)
            {
                if (_reticleStableStart < 0) _reticleStableStart = Time.realtimeSinceStartupAsDouble;

                if (Time.realtimeSinceStartupAsDouble - _reticleStableStart >= STABLE_DWELL_SECONDS)
                {
                    _primaryScanPlane = GetRootPlane(reticle.planeUnderReticle);
                    var align = _primaryScanPlane.alignment;
                    planeManager.requestedDetectionMode =
                        (align == PlaneAlignment.HorizontalUp || align == PlaneAlignment.HorizontalDown)
                        ? PlaneDetectionMode.Horizontal
                        : PlaneDetectionMode.Vertical;

                    ShowOnlyPlane(_primaryScanPlane);
                    SetTip("Move phone to grow this surface. Then press Select Surface.");
                }
            }
            else _reticleStableStart = -1;
        }
        else
        {
            var root = GetRootPlane(_primaryScanPlane);
            if (root != _primaryScanPlane) _primaryScanPlane = root;
            ShowOnlyPlane(_primaryScanPlane);
        }
    }

    private IEnumerator RescanRoutine()
    {
        if (cameraManager) cameraManager.autoFocusRequested = true;

        painter.StopPainting();
        painter.ClearLock();
        if (reticle) reticle.selectedPlane = null;
        DestroyAnchorIfAny();
        DestroyFrozenBorder();

        if (strokesRoot)
            for (int i = strokesRoot.childCount - 1; i >= 0; i--)
                Destroy(strokesRoot.GetChild(i).gameObject);

        _primaryScanPlane = null;
        _reticleStableStart = -1;

        if (arSession) arSession.Reset();
        yield return null;

        SetPhase(Phase.Scanning);
        if (planeManager)
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;

        TogglePlaneMesh(false);
    }

    private void SelectSurfaceUnderReticle()
    {
        if (!reticle) return;
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

        if (_currentAnchor == null)
        {
            Debug.LogError("Failed to create ARAnchor.");
            return;
        }

        var boundary = CopyBoundary(plane);
        var anchorRoot = _currentAnchor.transform;

        painter.strokesRoot = strokesRoot;
        painter.LockToPlaneStrict(plane, boundary, anchorRoot);

        SetPhase(Phase.PlaneSelected);
    }

    private void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
    {
        if (_phase != Phase.Scanning) return;
        if (_primaryScanPlane) ShowOnlyPlane(_primaryScanPlane);
        else TogglePlaneMesh(false);
    }

    private ARPlane GetRootPlane(ARPlane p)
    {
        while (p && p.subsumedBy != null) p = p.subsumedBy;
        return p;
    }

    private void ShowOnlyPlane(ARPlane planeToShow)
    {
        foreach (var p in planeManager.trackables)
        {
            var lr = p.GetComponent<LineRenderer>();
            if (lr) lr.enabled = (p == planeToShow);
            else { var mr = p.GetComponent<MeshRenderer>(); if (mr) mr.enabled = (p == planeToShow); }
        }
    }

    private void TogglePlaneMesh(bool visible)
    {
        foreach (var p in planeManager.trackables)
        {
            var lr = p.GetComponent<LineRenderer>();
            if (lr) lr.enabled = visible;
            else { var mr = p.GetComponent<MeshRenderer>(); if (mr) mr.enabled = visible; }
        }
    }

    private void BuildFrozenBorder()
    {
        DestroyFrozenBorder();
        var plane = reticle ? reticle.selectedPlane : null;
        if (!plane) return;
        if (!_currentAnchor) { Debug.LogError("Cannot build frozen border: _currentAnchor is null."); return; }

        var boundary = CopyBoundary(plane);
        if (boundary == null || boundary.Length < 3) return;

        _frozenBorderGO = new GameObject("FrozenPlaneBorder");
        _frozenBorderGO.transform.SetParent(_currentAnchor.transform, false);

        Pose planePoseInWorld = new Pose(plane.transform.position, plane.transform.rotation);
        Pose anchorPoseInWorld = new Pose(_currentAnchor.transform.position, _currentAnchor.transform.rotation);

        Quaternion invAnchorRot = Quaternion.Inverse(anchorPoseInWorld.rotation);
        Vector3 invAnchorPos = invAnchorRot * -anchorPoseInWorld.position;
        Pose inverseAnchorPose = new Pose(invAnchorPos, invAnchorRot);

        Pose planePoseInAnchorSpace = inverseAnchorPose.Multiply(planePoseInWorld);

        _frozenBorderGO.transform.localPosition = planePoseInAnchorSpace.position;
        _frozenBorderGO.transform.localRotation = planePoseInAnchorSpace.rotation;

        var lr = _frozenBorderGO.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = frozenLineWidth;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.material = mat;
        if (lr.material.HasProperty("_BaseColor")) lr.material.SetColor("_BaseColor", frozenLineColor);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        lr.positionCount = boundary.Length;
        for (int i = 0; i < boundary.Length; i++)
            lr.SetPosition(i, new Vector3(boundary[i].x, 0f, boundary[i].y));
    }

    private void DestroyFrozenBorder()
    {
        if (_frozenBorderGO) Destroy(_frozenBorderGO);
        _frozenBorderGO = null;
    }

    private static Vector2[] CopyBoundary(ARPlane plane)
    {
        var nat = plane.boundary;
        if (!nat.IsCreated || nat.Length < 3) return null;
        var arr = new Vector2[nat.Length];
        for (int i = 0; i < nat.Length; i++) arr[i] = nat[i];
        return arr;
    }

    private void DestroyAnchorIfAny()
    {
        if (_currentAnchor) { Destroy(_currentAnchor.gameObject); _currentAnchor = null; }
    }

    private void SetTip(string s) { if (txtTips) txtTips.text = s; }

    private void ToggleGraffiti()
    {
        if (_phase == Phase.Painting) { painter.StopPainting(); SetPhase(Phase.PlaneSelected); }
        else if (_phase == Phase.PlaneSelected) { SetPhase(Phase.Painting); }
    }

    private void StyleGraffitiButton(bool on)
    {
        if (!btnGraffiti) return;
        var img = btnGraffiti.GetComponent<Image>();
        var txt = btnGraffiti.GetComponentInChildren<TMP_Text>();
        if (img) img.color = on ? new Color(0.08f, 0.8f, 0.4f, 0.9f) : new Color(1f, 1f, 1f, 0.25f);
        if (txt) txt.text = on ? "Graffiti  (ON)" : "Graffiti";
    }

    // ===================== Save & Load via REST =====================

    private void OnSaveButtonPressed()
    {
        if (_isSaving)
        {
            SetTip("Already saving...");
            return;
        }

        if (_currentAnchor == null)
        {
            SetTip("Please select a surface first.");
            return;
        }

        if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn)
        {
            SetTip("Please sign in first.");
            return;
        }

        StartCoroutine(SaveGraffitiSequence());
    }
    private IEnumerator SaveGraffitiSequence()
    {
        _isSaving = true;
        SetTip("Saving...");

        bool toolsPrev = panelTools && panelTools.activeSelf;
        if (panelTools) panelTools.SetActive(false);
        bool savePrev = btnSave && btnSave.gameObject.activeSelf;
        if (btnSave) btnSave.gameObject.SetActive(false);

        yield return new WaitForEndOfFrame();

        var shot = ScreenCapture.CaptureScreenshotAsTexture();
        byte[] png = shot.EncodeToPNG();
        Destroy(shot);

        if (btnSave) btnSave.gameObject.SetActive(savePrev);
        if (panelTools) panelTools.SetActive(toolsPrev);

        string filename = $"graffiti_{DateTime.Now:yyyyMMdd_HHmmss}.png";

        var fileReq = new UnityWebRequest(Back4AppRest.ServerUrl.TrimEnd('/') + "/files/" + filename, "POST");

        fileReq.uploadHandler = new UploadHandlerRaw(png);
        fileReq.downloadHandler = new DownloadHandlerBuffer();
        fileReq.SetRequestHeader("X-Parse-Application-Id", Back4AppRest.AppId);
        fileReq.SetRequestHeader("X-Parse-REST-API-Key", Back4AppRest.RestKey);

        fileReq.SetRequestHeader("Content-Type", "image/png");

        yield return fileReq.SendWebRequest();
        if (fileReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("File upload failed: " + fileReq.error + " // " + fileReq.downloadHandler.text);
            SetTip("Save failed (upload).");
            fileReq.Dispose();
            _isSaving = false;
            yield break;
        }

        var fileJson = fileReq.downloadHandler.text;
        fileReq.Dispose();

        var nameKey = "\"name\":\"";
        var urlKey = "\"url\":\"";
        string parseFileName = ExtractJsonString(fileJson, nameKey);
        string parseFileUrl = ExtractJsonString(fileJson, urlKey);

        var t = _currentAnchor.transform;
        var body = new
        {
            image = new { __type = "File", name = parseFileName },
            posX = t.position.x,
            posY = t.position.y,
            posZ = t.position.z,
            rotX = t.rotation.x,
            rotY = t.rotation.y,
            rotZ = t.rotation.z,
            rotW = t.rotation.w,
            imageUrl = parseFileUrl
        };

        string json = JsonUtility.ToJson(body);
        byte[] payload = System.Text.Encoding.UTF8.GetBytes(json);

        var req = Back4AppRest.NewRequest("POST", "/classes/Graffiti", null, payload);
        yield return Back4AppRest.Send(req, (code, text) =>
        {
            if (code >= 200 && code < 300)
            {
                SetTip("Graffiti saved.");
            }
            else
            {
                Debug.LogError($"Create Graffiti failed {code}: {text}");
                SetTip("Save failed.");
            }
        });

        _isSaving = false;
    }

    private IEnumerator LoadSavedGraffitiCoroutine()
    {
        if (AuthManager.Instance == null || !AuthManager.Instance.IsLoggedIn)
            yield break;

        SetTip("Loading saved graffiti...");

        string path = "/classes/Graffiti?order=-createdAt&limit=50";
        var req = Back4AppRest.NewRequest("GET", path, null);

        string response = null;
        yield return Back4AppRest.Send(req, (code, text) =>
        {
            if (code >= 200 && code < 300)
                response = text;
            else
                Debug.LogError($"Query failed {code}: {text}");
        });

        if (string.IsNullOrEmpty(response))
        {
            SetTip("");
            yield break;
        }

        var resultsKey = "\"results\":";
        int idx = response.IndexOf(resultsKey, System.StringComparison.Ordinal);
        if (idx < 0)
        {
            SetTip("");
            yield break;
        }

        int arrStart = response.IndexOf('[', idx);
        int arrEnd = response.LastIndexOf(']');
        if (arrStart < 0 || arrEnd < 0 || arrEnd <= arrStart)
        {
            SetTip("");
            yield break;
        }

        string arr = response.Substring(arrStart + 1, arrEnd - arrStart - 1);

        var chunks = arr.Split(new string[] { "},{" }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var chunkRaw in chunks)
        {
            string chunk = chunkRaw;
            if (!chunk.StartsWith("{")) chunk = "{" + chunk;
            if (!chunk.EndsWith("}")) chunk = chunk + "}";

            string url = ExtractJsonString(chunk, "\"url\":\"");
            if (string.IsNullOrEmpty(url))
                url = ExtractJsonString(chunk, "\"imageUrl\":\"");

            string sx = ExtractJsonNumber(chunk, "\"posX\":");
            string sy = ExtractJsonNumber(chunk, "\"posY\":");
            string sz = ExtractJsonNumber(chunk, "\"posZ\":");
            string rx = ExtractJsonNumber(chunk, "\"rotX\":");
            string ry = ExtractJsonNumber(chunk, "\"rotY\":");
            string rz = ExtractJsonNumber(chunk, "\"rotZ\":");
            string rw = ExtractJsonNumber(chunk, "\"rotW\":");

            if (string.IsNullOrEmpty(url))
                continue;

            float px = ToF(sx), py = ToF(sy), pz = ToF(sz);
            float qx = ToF(rx), qy = ToF(ry), qz = ToF(rz), qw = ToF(rw);
            Pose savedPose = new Pose(new Vector3(px, py, pz), new Quaternion(qx, qy, qz, qw));

            if (raycaster.Raycast(new Ray(savedPose.position + Vector3.up * 0.1f, Vector3.down),
                                  _loadHits, TrackableType.PlaneWithinBounds))
            {
                var foundPlane = planeManager.GetPlane(_loadHits[0].trackableId);
                if (foundPlane != null)
                {
                    var loadedAnchor = anchorManager.AttachAnchor(foundPlane, savedPose);
                    if (loadedAnchor != null)
                    {
                        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        quad.transform.SetParent(loadedAnchor.transform, false);
                        quad.transform.localPosition = Vector3.zero;

                        var renderer = quad.GetComponent<Renderer>();
                        renderer.enabled = false;
                        StartCoroutine(DownloadAndApplyTexture(url, renderer));
                    }
                }
            }
        }

        SetTip("");
    }

    private static float ToF(string s) { if (float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) return v; return 0f; }

    private static string ExtractJsonString(string src, string key)
    {
        int i = src.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return null;
        int start = i + key.Length;
        int end = src.IndexOf('"', start);
        if (end < 0) return null;
        return src.Substring(start, end - start);
    }

    private static string ExtractJsonNumber(string src, string key)
    {
        int i = src.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return "0";
        int start = i + key.Length;
        int end = start;
        while (end < src.Length && "0123456789+-.eE".IndexOf(src[end]) >= 0) end++;
        return src.Substring(start, end - start).Trim();
    }

    private IEnumerator DownloadAndApplyTexture(string url, Renderer targetRenderer)
    {
        using var req = UnityWebRequestTexture.GetTexture(url);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            Texture2D texture = DownloadHandlerTexture.GetContent(req);
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.mainTexture = texture;
            targetRenderer.material = mat;

            float aspect = (float)texture.width / texture.height;
            targetRenderer.transform.localScale = new Vector3(aspect, 1f, 1f);
            targetRenderer.enabled = true;
        }
        else Debug.LogError("Image download failed: " + req.error);
    }
}
