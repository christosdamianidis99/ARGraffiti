using System.Collections;
using UnityEngine;
using UnityEngine.UI;


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
    public float relativeSpacingPercent = 0.01f; 

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

        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvasScaler = canvas.GetComponent<CanvasScaler>();
        }

        if (panelGraffiti == null)
        {
            GameObject graffitiObj = GameObject.Find("Panel_Graffiti");
            if (graffitiObj != null)
            {
                panelGraffiti = graffitiObj.GetComponent<RectTransform>();
            }
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    void Start()
    {

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(AdjustPositionDelayed());
        }
        else
        {
            AdjustPosition();
        }
    }

    System.Collections.IEnumerator AdjustPositionDelayed()
    {
        yield return null;
        AdjustPosition();
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
                AdjustPosition();
            }
        }
    }

    void Update()
    {
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

        if (Application.isPlaying && panelGraffiti != null && rectTransform != null)
        {
            float graffitiTop = panelGraffiti.anchoredPosition.y + panelGraffiti.sizeDelta.y;
            
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


        float graffitiTop = panelGraffiti.anchoredPosition.y + panelGraffiti.sizeDelta.y;

        float spacing = minSpacing;
        if (useRelativeSpacing && canvas != null)
        {
            float referenceHeight = canvasScaler != null ? canvasScaler.referenceResolution.y : Screen.height;
            spacing = referenceHeight * relativeSpacingPercent;
        }

        float newYPosition = graffitiTop + spacing;

        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, newYPosition);
        
        lastGraffitiTop = graffitiTop;
    }
}
