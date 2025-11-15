using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class AuthGateBootstrap : MonoBehaviour
{
    [Header("Optional: existing overlay (CanvasGroup) to show when logged out")]
    public CanvasGroup signedOutOverlay; // can be null; will be created if missing

    [Header("Overlay text (only used if created automatically)")]
    [TextArea] public string signedOutMessage = "Please sign in to start drawing.";

    // caches
    private RequireAuth[] _requireAuth;
    private UIRequireAuth[] _uiRequireAuth;

    void Awake()
    {
        // Ensure AuthState singleton exists (place this in your boot scene or Login scene)
        if (AuthState.I == null)
        {
            var go = new GameObject("AuthState");
            go.AddComponent<AuthState>();
        }
    }

    void OnEnable()
    {
        // Discover gates in the active scene
        _requireAuth = FindObjectsByType<RequireAuth>(FindObjectsSortMode.None);
        _uiRequireAuth = FindObjectsByType<UIRequireAuth>(FindObjectsSortMode.None);

        // Ensure overlay exists
        if (signedOutOverlay == null)
            signedOutOverlay = FindOverlayOrCreate();

        Apply(AuthState.I != null && AuthState.I.IsSignedIn);

        if (AuthState.I != null)
            AuthState.I.OnAuthChanged += Apply;
    }

    void OnDisable()
    {
        if (AuthState.I != null)
            AuthState.I.OnAuthChanged -= Apply;
    }

    void Apply(bool isSignedIn)
    {
        // Gate all behaviours marked with RequireAuth
        foreach (var gate in _requireAuth)
        {
            if (gate == null) continue;
            foreach (var b in gate.ResolveTargets())
                if (b != null) b.enabled = isSignedIn;
        }

        // Gate UI interactability for UIRequireAuth
        foreach (var ui in _uiRequireAuth)
        {
            if (ui == null) continue;
            foreach (var s in ui.Resolve())
                if (s != null) s.interactable = isSignedIn;
        }

        // Overlay visibility
        if (signedOutOverlay != null)
        {
            signedOutOverlay.alpha = isSignedIn ? 0f : 1f;
            signedOutOverlay.blocksRaycasts = !isSignedIn;
            signedOutOverlay.interactable = !isSignedIn;
        }

        Debug.Log($"[AuthGate] SignedIn={isSignedIn}. Gated {(_requireAuth?.Length ?? 0)} objects; UI gated {(_uiRequireAuth?.Length ?? 0)}.");
    }

    CanvasGroup FindOverlayOrCreate()
    {
        var existing = GameObject.Find("AuthOverlay");
        if (existing != null && existing.TryGetComponent<CanvasGroup>(out var cg))
            return cg;

        // Create Canvas
        var canvasGO = new GameObject("AuthOverlay");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        canvasGO.AddComponent<GraphicRaycaster>();

        // Fullscreen Panel
        var panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGO.transform, false);

        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;

        var img = panel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.55f);

        var cgPanel = panel.AddComponent<CanvasGroup>();
        var blocker = panel.AddComponent<TouchBlocker>(); // consume touches while logged out

        // Centered text
        var textGO = new GameObject("Message");
        textGO.transform.SetParent(panel.transform, false);
        var tRect = textGO.AddComponent<RectTransform>();
        tRect.anchorMin = new Vector2(0.1f, 0.4f);
        tRect.anchorMax = new Vector2(0.9f, 0.6f);
        tRect.offsetMin = tRect.offsetMax = Vector2.zero;

        var txt = textGO.AddComponent<Text>();
        txt.text = signedOutMessage;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 18;
        txt.resizeTextMaxSize = 48;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Truncate;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        return cgPanel;
    }
}
