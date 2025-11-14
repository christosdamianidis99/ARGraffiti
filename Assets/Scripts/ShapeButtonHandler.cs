using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles click events for shape buttons (Button_Square and Button_Circle)
/// Sets graffiti shape and hides PanelGraffitiOptions on click
/// </summary>
public class ShapeButtonHandler : MonoBehaviour
{
    [Header("References")]
    [Tooltip("PhonePainter component for setting graffiti shape")]
    public PhonePainter painter;
    
    [Tooltip("PanelGraffitiOptions panel to hide after button click")]
    public GameObject panelGraffitiOptions;
    
    [Header("Shape Type")]
    [Tooltip("Shape type for this button: true=Square, false=Circle")]
    public bool isSquareShape = false;

    void Start()
    {
        // Find PhonePainter if not assigned
        if (painter == null)
        {
            painter = FindObjectOfType<PhonePainter>();
            if (painter == null)
            {
                Debug.LogWarning($"ShapeButtonHandler ({gameObject.name}): PhonePainter not found!");
            }
        }
        
        // Find PanelGraffitiOptions if not assigned
        if (panelGraffitiOptions == null)
        {
            // First try to find in parent hierarchy (under Button_Graffiti)
            Transform parent = transform.parent;
            while (parent != null)
            {
                Transform panelTransform = parent.Find("PanelGraffitiOptions");
                if (panelTransform != null)
                {
                    panelGraffitiOptions = panelTransform.gameObject;
                    break;
                }
                parent = parent.parent;
            }
            
            // If not found, try searching the entire scene
            if (panelGraffitiOptions == null)
            {
                panelGraffitiOptions = GameObject.Find("PanelGraffitiOptions");
                if (panelGraffitiOptions == null)
                {
                    // Compatible with old names
                    panelGraffitiOptions = GameObject.Find("GraffitiOptionsPanel");
                    if (panelGraffitiOptions == null)
                    {
                        panelGraffitiOptions = GameObject.Find("Panel_GraffitiOptions");
                    }
                }
            }
        }
        
        // Bind button click event
        Button button = GetComponent<Button>();
        if (button != null)
        {
            // Ensure button is interactable
            button.interactable = true;
            
            // Ensure Image can receive raycast
            Image buttonImage = GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.raycastTarget = true;
            }
            
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClick);
            Debug.Log($"ShapeButtonHandler ({gameObject.name}): Button click event bound");
        }
        else
        {
            Debug.LogWarning($"ShapeButtonHandler ({gameObject.name}): Button component not found!");
        }
    }
    
    /// <summary>
    /// Handle button click event
    /// </summary>
    public void OnButtonClick()
    {
        Debug.Log($"ShapeButtonHandler ({gameObject.name}): Button clicked!");
        
        // Set graffiti shape
        if (painter != null)
        {
            if (isSquareShape)
            {
                painter.SetShapeSquare();
                Debug.Log($"ShapeButtonHandler ({gameObject.name}): Graffiti shape set to Square");
            }
            else
            {
                painter.SetShapeCircle();
                Debug.Log($"ShapeButtonHandler ({gameObject.name}): Graffiti shape set to Circle");
            }
        }
        else
        {
            Debug.LogError($"ShapeButtonHandler ({gameObject.name}): PhonePainter is null, cannot set shape!");
        }
        
        // Hide PanelGraffitiOptions
        if (panelGraffitiOptions != null)
        {
            panelGraffitiOptions.SetActive(false);
            Debug.Log($"ShapeButtonHandler ({gameObject.name}): PanelGraffitiOptions hidden");
        }
        else
        {
            Debug.LogWarning($"ShapeButtonHandler ({gameObject.name}): PanelGraffitiOptions not found!");
        }
    }
    
    /// <summary>
    /// Manually trigger click (for testing)
    /// </summary>
    public void TriggerClick()
    {
        OnButtonClick();
    }
}

