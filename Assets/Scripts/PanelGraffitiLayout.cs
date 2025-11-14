using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Dynamically adjusts the positions of buttons in Panel_Graffiti to ensure proper spacing across different screen sizes
/// Adapts to different iOS and Android devices
/// Automatically adjusts in both editor and runtime
/// </summary>
public class PanelGraffitiLayout : MonoBehaviour
{
    [Header("Button References")]
    public RectTransform buttonPaintBrush;
    public RectTransform buttonGraffiti;
    public RectTransform buttonColorPalette;

    [Header("Layout Settings")]
    [Tooltip("Minimum margin (percentage of screen width)")]
    [Range(0f, 0.1f)]
    public float minMarginPercent = 0.03f; // 3% of screen width
    
    [Tooltip("Minimum spacing between buttons (pixels)")]
    public float minSpacing = 60f;
    
    [Tooltip("Whether to use reference resolution instead of actual screen width (recommended for consistent layout)")]
    public bool useReferenceResolution = true;

    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private int lastScreenWidth;
    private int lastScreenHeight;

    void Awake()
    {
        // Get Canvas and CanvasScaler references
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvasScaler = canvas.GetComponent<CanvasScaler>();
        }

        // Record initial screen dimensions
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    void Start()
    {
        // Delay one frame to ensure layout is complete
        StartCoroutine(AdjustPositionNextFrame());
    }

#if UNITY_EDITOR
    void Reset()
    {
        // When component is added or reset, delay position adjustment
        EditorApplication.delayCall += AdjustButtonPositions;
    }

    void OnEnable()
    {
        // In editor mode, also adjust position when component is enabled
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall += AdjustButtonPositions;
        }
    }
#endif

    System.Collections.IEnumerator AdjustPositionNextFrame()
    {
        yield return null; // Wait one frame
        AdjustButtonPositions();
    }

    void OnRectTransformDimensionsChange()
    {
        // Re-adjust when screen dimensions change (e.g., rotation)
        if (Application.isPlaying)
        {
            StartCoroutine(AdjustPositionNextFrame());
        }
#if UNITY_EDITOR
        else
        {
            // Also adjust in editor mode
            EditorApplication.delayCall += AdjustButtonPositions;
        }
#endif
    }

    void Update()
    {
        // Detect screen dimension changes (including rotation)
        if (Application.isPlaying && 
            (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight))
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            AdjustButtonPositions();
        }
    }

    void AdjustButtonPositions()
    {
        if (!buttonPaintBrush || !buttonGraffiti || !buttonColorPalette)
        {
            Debug.LogWarning("PanelGraffitiLayout: Button references not set!");
            return;
        }

        // Find Gallery button
        RectTransform buttonGallery = null;
        Transform galleryTransform = transform.Find("Button_Gallery");
        if (galleryTransform != null)
        {
            buttonGallery = galleryTransform.GetComponent<RectTransform>();
        }

        // Get parent container (Panel_Graffiti) actual width
        RectTransform parentRect = GetComponent<RectTransform>();
        if (parentRect == null) return;

        // Get parent container actual width (already considers SafeArea and scaling)
        float panelWidth = parentRect.rect.width;
        
        // If width is 0 or using reference resolution, calculate appropriate width
        if (panelWidth <= 0 || useReferenceResolution)
        {
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }
            
            if (canvasScaler == null && canvas != null)
            {
                canvasScaler = canvas.GetComponent<CanvasScaler>();
            }

            if (canvasScaler != null && useReferenceResolution)
            {
                // Use reference resolution to ensure consistency across different devices
                panelWidth = canvasScaler.referenceResolution.x;
            }
            else if (canvas != null && panelWidth <= 0)
            {
                // Fallback: use screen width divided by scale factor
                float currentScale = canvas.scaleFactor;
#if UNITY_EDITOR
                // In editor mode, use reference resolution or default value
                if (canvasScaler != null)
                {
                    panelWidth = canvasScaler.referenceResolution.x;
                }
                else
                {
                    panelWidth = 1080f; // Default reference width
                }
#else
                panelWidth = Screen.width / currentScale;
#endif
            }
            
            // If still invalid, use parent container width
            if (panelWidth <= 0)
            {
                panelWidth = parentRect.rect.width;
            }
        }

        // Button size (88 pixels)
        float buttonSize = 88f;
        float spacing = minSpacing; // Spacing between buttons
        float minMargin = panelWidth * minMarginPercent; // Calculate minimum margin

        // Middle button: anchor at center (0.5, 0.5), position is (0, 0)
        buttonGraffiti.anchoredPosition = new Vector2(0f, 0f);

        // Calculate PaintBrush button position - place it to the left of Graffiti button
        // Graffiti button is centered at (0, 0), so its left edge is at -buttonSize/2
        // PaintBrush right edge should be spacing distance to the left of Graffiti left edge
        // PaintBrush center = -buttonSize/2 - spacing - buttonSize/2 = -(buttonSize + spacing)
        buttonPaintBrush.anchorMin = new Vector2(0.5f, 0.5f);
        buttonPaintBrush.anchorMax = new Vector2(0.5f, 0.5f);
        buttonPaintBrush.anchoredPosition = new Vector2(
            -(buttonSize + spacing),
            0f
        );

        // Right button: anchor at right (1, 0.5), position is negative offset relative to right boundary
        buttonColorPalette.anchoredPosition = new Vector2(
            -(minMargin + buttonSize * 0.5f),
            0f
        );
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // In editor, automatically adjust position when values change (whether running or not)
        if (buttonPaintBrush && buttonGraffiti && buttonColorPalette)
        {
            // Delay execution to ensure all references are updated
            EditorApplication.delayCall += AdjustButtonPositions;
        }
    }
#endif
}

