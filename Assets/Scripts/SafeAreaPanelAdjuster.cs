using System.Collections;
using UnityEngine;
using UnityEngine.UI;


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

        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvasScaler = canvas.GetComponent<CanvasScaler>();
        }

        lastSafeArea = Screen.safeArea;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastOrientation = Screen.orientation;
    }

    void Start()
    {

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(AdjustPositionDelayed());
        }
        else
        {
            AdjustPanelPosition();
        }
    }

    System.Collections.IEnumerator AdjustPositionDelayed()
    {
        yield return null; 
        AdjustPanelPosition();
    }

    void OnRectTransformDimensionsChange()
    {
        if (Application.isPlaying)
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(AdjustPositionDelayed());
            }
            else
            {
                AdjustPanelPosition();
            }
        }
    }

    void Update()
    {
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

        float scaleFactor = canvas.scaleFactor;

        float topInset = (Screen.height - (safeArea.y + safeArea.height)) / scaleFactor;
        float bottomInset = safeArea.y / scaleFactor;

        Vector2 newPosition = originalAnchoredPosition;

        if (isTopPanel)
        {

            float totalTopOffset = topInset + topPadding;
            newPosition = new Vector2(originalAnchoredPosition.x, originalAnchoredPosition.y - totalTopOffset);
        }

        if (isBottomPanel)
        {
            float totalBottomOffset = bottomInset + bottomPadding;
            newPosition = new Vector2(originalAnchoredPosition.x, originalAnchoredPosition.y + totalBottomOffset);
        }

        rectTransform.anchoredPosition = newPosition;
    }
}

