using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamically adjust Panel position to avoid being blocked by safe area (notch, bottom indicator, etc.)
/// Adapts to different iOS and Android devices
/// </summary>
public class SafeAreaPanelAdjuster : MonoBehaviour
{
    [Header("Panel Type")]
    [Tooltip("Whether this is a top panel (needs to avoid notch)")]
    public bool isTopPanel = false;
    
    [Tooltip("Whether this is a bottom panel (needs to avoid bottom indicator)")]
    public bool isBottomPanel = false;

    [Header("Additional Padding")]
    [Tooltip("Additional top padding (in pixels)")]
    public float topPadding = 0f;
    
    [Tooltip("Additional bottom padding (in pixels)")]
    public float bottomPadding = 0f;

    private RectTransform rectTransform;
    private Vector2 originalAnchoredPosition;
    private Vector2 originalSizeDelta;
    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private Rect lastSafeArea;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private ScreenOrientation lastOrientation;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            originalAnchoredPosition = rectTransform.anchoredPosition;
            originalSizeDelta = rectTransform.sizeDelta;
        }

        // Get references to Canvas and CanvasScaler
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvasScaler = canvas.GetComponent<CanvasScaler>();
        }

        // Initialize screen state
        lastSafeArea = Screen.safeArea;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastOrientation = Screen.orientation;
    }

    void Start()
    {
        // Delay one frame to ensure Canvas is initialized
        // Only start coroutine if game object is active
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(AdjustPositionDelayed());
        }
        else
        {
            // If inactive, adjust position directly
            AdjustPanelPosition();
        }
    }

    System.Collections.IEnumerator AdjustPositionDelayed()
    {
        yield return null; // Wait one frame
        AdjustPanelPosition();
    }

    void OnRectTransformDimensionsChange()
    {
        // Re-adjust on screen size change (such as device rotation)
        if (Application.isPlaying)
        {
            // Only start coroutine if game object is active
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(AdjustPositionDelayed());
            }
            else
            {
                // If inactive, adjust position directly
                AdjustPanelPosition();
            }
        }
    }

    void Update()
    {
        // Detect screen size, safe area, or orientation changes
        Rect currentSafeArea = Screen.safeArea;
        bool safeAreaChanged = currentSafeArea != lastSafeArea;
        bool screenSizeChanged = Screen.width != lastScreenWidth || Screen.height != lastScreenHeight;
        bool orientationChanged = Screen.orientation != lastOrientation;

        if (Application.isPlaying && (safeAreaChanged || screenSizeChanged || orientationChanged))
        {
            lastSafeArea = currentSafeArea;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastOrientation = Screen.orientation;
            AdjustPanelPosition();
        }
    }

    void AdjustPanelPosition()
    {
        if (rectTransform == null) return;

        Rect safeArea = Screen.safeArea;
        
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
        }

        if (canvasScaler == null)
        {
            canvasScaler = canvas.GetComponent<CanvasScaler>();
            if (canvasScaler == null) return;
        }

        // Calculate Canvas scale factor
        float scaleFactor = canvas.scaleFactor;

        // Convert screen coordinates to Canvas coordinates
        // topInset: Height blocked at top (notch height)
        // bottomInset: Height blocked at bottom (home indicator height)
        float topInset = (Screen.height - (safeArea.y + safeArea.height)) / scaleFactor;
        float bottomInset = safeArea.y / scaleFactor;

        Vector2 newPosition = originalAnchoredPosition;

        if (isTopPanel)
        {
            // Top panel: Offset downward to avoid notch
            // Since anchor is at top (y: 1), downward movement is negative
            float totalTopOffset = topInset + topPadding;
            newPosition = new Vector2(originalAnchoredPosition.x, originalAnchoredPosition.y - totalTopOffset);
        }

        if (isBottomPanel)
        {
            // Bottom panel: Offset upward to avoid bottom indicator
            // Since anchor is at bottom (y: 0), upward movement is positive
            float totalBottomOffset = bottomInset + bottomPadding;
            newPosition = new Vector2(originalAnchoredPosition.x, originalAnchoredPosition.y + totalBottomOffset);
        }

        rectTransform.anchoredPosition = newPosition;
    }
}

