using UnityEngine;
using UnityEngine.UI;


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
        if (painter == null)
        {
            painter = FindFirstObjectByType<PhonePainter>();
            if (painter == null)
            {
                Debug.LogWarning($"ShapeButtonHandler ({gameObject.name}): PhonePainter not found!");
            }
        }
        
        if (panelGraffitiOptions == null)
        {
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
            
            if (panelGraffitiOptions == null)
            {
                panelGraffitiOptions = GameObject.Find("PanelGraffitiOptions");
                if (panelGraffitiOptions == null)
                {
                    panelGraffitiOptions = GameObject.Find("GraffitiOptionsPanel");
                    if (panelGraffitiOptions == null)
                    {
                        panelGraffitiOptions = GameObject.Find("Panel_GraffitiOptions");
                    }
                }
            }
        }
        
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.interactable = true;
            
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

    public void OnButtonClick()
    {
        Debug.Log($"ShapeButtonHandler ({gameObject.name}): Button clicked!");
        
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

    public void TriggerClick()
    {
        OnButtonClick();
    }
}

