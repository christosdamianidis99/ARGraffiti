using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ensure Panel_Tools is always above Panel_Graffiti to avoid overlap.
/// Adapts to different screen sizes for iOS and Android devices.
/// </summary>
public class PanelToolsPositionAdjuster : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The RectTransform of Panel_Graffiti, used for relative positioning.")]
    public RectTransform panelGraffiti;

    [Header("Settings")]
    [Tooltip("Minimum spacing in pixels between Panel_Tools and Panel_Graffiti.")]
    public float minSpacing = 20f;
    
    [Tooltip("Use relative spacing (based on a percentage of screen height).")]
    public bool useRelativeSpacing = false;
    
    [Tooltip("Relative spacing between panels (as percentage of screen height, only used when useRelativeSpacing is true).")]
    [Range(0f, 0.05f)]
    public float relativeSpacingPercent = 0.01f; // 1% of screen height

    private RectTransform rectTransform;
    private float originalYPosition;
    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastGraffitiTop = float.MinValue;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            originalYPosition = rectTransform.anchoredPosition.y;
        }

        // Get references to Canvas and CanvasScaler
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvasScaler = canvas.GetComponent<CanvasScaler>();
        }

        // If not set in Inspector, try to find by name
        if (panelGraffiti == null)
        {
            GameObject graffitiObj = GameObject.Find("Panel_Graffiti");
            if (graffitiObj != null)
            {
                panelGraffiti = graffitiObj.GetComponent<RectTransform>();
            }
        }

        // Record initial screen size
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    void Start()
    {
        // Delay one frame to ensure all layouts are done
        // Only start coroutine if game object is active
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(AdjustPositionDelayed());
        }
        else
        {
            // If inactive, adjust position directly
            AdjustPosition();
        }
    }

    System.Collections.IEnumerator AdjustPositionDelayed()
    {
        yield return null; // Wait one frame
        AdjustPosition();
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
                // If inactive, adjust position directly without coroutine
                AdjustPosition();
            }
        }
    }

    void Update()
    {
        // Detect screen size change (including rotation)
        if (Application.isPlaying && 
            (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight))
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            AdjustPosition();
        }
    }

    void LateUpdate()
    {
        // Adjust in LateUpdate to ensure it's done after SafeAreaPanelAdjuster
        // Only adjust when needed (to avoid running every frame)
        if (Application.isPlaying && panelGraffiti != null && rectTransform != null)
        {
            float graffitiTop = panelGraffiti.anchoredPosition.y + panelGraffiti.sizeDelta.y;
            
            // Only update if position has actually changed
            if (Mathf.Abs(graffitiTop - lastGraffitiTop) > 0.1f)
            {
                lastGraffitiTop = graffitiTop;
                AdjustPosition();
            }
        }
    }

    void AdjustPosition()
    {
        if (rectTransform == null || panelGraffiti == null) return;

        // Calculate the top position of Panel_Graffiti.
        // Panel_Graffiti's anchor is at the bottom (y: 0),
        // thus its top = anchoredPosition.y + sizeDelta.y
        float graffitiTop = panelGraffiti.anchoredPosition.y + panelGraffiti.sizeDelta.y;

        // Calculate spacing
        float spacing = minSpacing;
        if (useRelativeSpacing && canvas != null)
        {
            // Use relative spacing (reference resolution or screen height)
            float referenceHeight = canvasScaler != null ? canvasScaler.referenceResolution.y : Screen.height;
            spacing = referenceHeight * relativeSpacingPercent;
        }

        // The bottom of Panel_Tools should be above the top of Panel_Graffiti by 'spacing'
        // Panel_Tools anchor is also at bottom (y: 0), so its anchoredPosition.y = graffitiTop + spacing
        float newYPosition = graffitiTop + spacing;

        // Update Panel_Tools position
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, newYPosition);
        
        lastGraffitiTop = graffitiTop;
    }
}
