using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif


public class PanelGraffitiLayout : MonoBehaviour
{
    [Header("Button References")]
    public RectTransform buttonPaintBrush;
    public RectTransform buttonGraffiti;
    public RectTransform buttonColorPalette;

    [Header("Layout Settings")]
    [Tooltip("Minimum margin (percentage of screen width)")]
    [Range(0f, 0.1f)]
    public float minMarginPercent = 0.03f;
    
    [Tooltip("Minimum spacing between buttons (pixels)")]
    public float minSpacing = 60f;
    
    [Tooltip("Whether to use reference resolution instead of actual screen width (recommended for consistent layout)")]
    public bool useReferenceResolution = true;

    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private int lastScreenWidth;
    private int lastScreenHeight;
#if UNITY_EDITOR
    private bool isAdjustmentPending = false; 
#endif

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvasScaler = canvas.GetComponent<CanvasScaler>();
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    void Start()
    {
        StartCoroutine(AdjustPositionNextFrame());
    }

#if UNITY_EDITOR
    void Reset()
    {

        isAdjustmentPending = false;
        ScheduleAdjustment();
    }

    void OnEnable()
    {

        if (!Application.isPlaying && !isAdjustmentPending)
        {
            ScheduleAdjustment();
        }
    }


    void ScheduleAdjustment()
    {
        if (isAdjustmentPending) return; 
        
        isAdjustmentPending = true;
        EditorApplication.delayCall += () => {
            isAdjustmentPending = false;
            if (this != null && gameObject != null)
            {
                AdjustButtonPositions();
        }
        };
    }
#endif

    System.Collections.IEnumerator AdjustPositionNextFrame()
    {
        yield return null; 
        AdjustButtonPositions();
    }

    void OnRectTransformDimensionsChange()
    {
        
        if (Application.isPlaying)
        {
            StartCoroutine(AdjustPositionNextFrame());
        }
#if UNITY_EDITOR
        else
        {
            
            ScheduleAdjustment();
        }
#endif
    }

    void Update()
    {
        
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
        
        if (this == null || gameObject == null)
        {
            return;
        }

        if (transform == null)
        {
            return;
        }

        if (!buttonPaintBrush || !buttonGraffiti || !buttonColorPalette)
        {
            Debug.LogWarning("PanelGraffitiLayout: Button references not set!");
            return;
        }

        RectTransform buttonGallery = null;
        Transform galleryTransform = transform.Find("Button_Gallery");
        if (galleryTransform != null)
        {
            buttonGallery = galleryTransform.GetComponent<RectTransform>();
        }

        RectTransform parentRect = GetComponent<RectTransform>();
        if (parentRect == null) return;

        float panelWidth = parentRect.rect.width;
        
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
                panelWidth = canvasScaler.referenceResolution.x;
            }
            else if (canvas != null && panelWidth <= 0)
            {
                float currentScale = canvas.scaleFactor;
#if UNITY_EDITOR
                if (canvasScaler != null)
                {
                    panelWidth = canvasScaler.referenceResolution.x;
                }
                else
                {
                    panelWidth = 1080f; 
                }
#else
                panelWidth = Screen.width / currentScale;
#endif
            }
            
            if (panelWidth <= 0)
            {
                panelWidth = parentRect.rect.width;
            }
        }

        float buttonSize = 88f;
        float spacing = minSpacing;
        float minMargin = panelWidth * minMarginPercent; 

       
        buttonGraffiti.anchoredPosition = new Vector2(0f, 0f);

       
        buttonPaintBrush.anchorMin = new Vector2(0.5f, 0.5f);
        buttonPaintBrush.anchorMax = new Vector2(0.5f, 0.5f);
        buttonPaintBrush.anchoredPosition = new Vector2(
            -(buttonSize + spacing),
            0f
        );

       
        buttonColorPalette.anchoredPosition = new Vector2(
            -(minMargin + buttonSize * 0.5f),
            0f
        );
    }

#if UNITY_EDITOR
    void OnValidate()
    {
       
        if (buttonPaintBrush && buttonGraffiti && buttonColorPalette && gameObject != null)
        {
            ScheduleAdjustment();
        }
    }
#endif
}

