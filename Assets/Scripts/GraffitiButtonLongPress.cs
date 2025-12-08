using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;


public class GraffitiButtonLongPress : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Duration of long press (seconds)")]
    public float longPressDuration = 0.3f;
    
    [Tooltip("Scale factor during scale animation")]
    [Range(1.0f, 1.5f)]
    public float pressScale = 1.15f;
    
    [Tooltip("Scale animation duration (seconds)")]
    public float scaleAnimationDuration = 0.2f;

    private RectTransform rectTransform;
    private Vector3 originalScale;
    private bool isPressed = false;
    private bool isLongPressTriggered = false;
    private Coroutine longPressCoroutine;
    private Coroutine scaleCoroutine;
    private AppStateControllerPhone appStateController;
    private PhonePainter painter;
    private float pointerDownTime = 0f;
    private bool hasLongPressed = false;
    
    [Header("Panel Reference")]
    [Tooltip("PanelGraffitiOptions panel, show/hide when button is clicked")]
    public GameObject panelGraffitiOptions;
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = gameObject.AddComponent<RectTransform>();
        }
        originalScale = rectTransform.localScale;

        
        var image = GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
        }

        
        var button = GetComponent<Button>();
        if (button != null)
        {
        
            button.interactable = true;
            
        
            button.onClick.RemoveAllListeners();
        }
    }

    void Start()
    {
       
        if (appStateController == null)
        {
            appStateController = FindFirstObjectByType<AppStateControllerPhone>();
            if (appStateController == null)
            {
                Debug.LogWarning("GraffitiButtonLongPress: AppStateControllerPhone not found in Start!");
            }
        }

       
        if (painter == null)
        {
            painter = FindFirstObjectByType<PhonePainter>();
            if (painter == null)
            {
                Debug.LogWarning("GraffitiButtonLongPress: PhonePainter not found in Start!");
            }
        }
        

        if (panelGraffitiOptions == null)
        {
            Transform childPanel = transform.Find("PanelGraffitiOptions");
            if (childPanel != null)
            {
                panelGraffitiOptions = childPanel.gameObject;
            }
            else
            {
                panelGraffitiOptions = GameObject.Find("PanelGraffitiOptions");
                if (panelGraffitiOptions == null)
                {
                    panelGraffitiOptions = GameObject.Find("PanelGraffitiOptions");
                    if (panelGraffitiOptions == null)
                    {
                        panelGraffitiOptions = GameObject.Find("Panel_GraffitiOptions");
                        if (panelGraffitiOptions == null)
                        {
                            Debug.LogWarning("GraffitiButtonLongPress: PanelGraffitiOptions not found! Please create it in the scene.");
                        }
                    }
                }
            }
        }
        
        if (panelGraffitiOptions != null)
        {
            panelGraffitiOptions.SetActive(false);
        }

        SetupEventTrigger();
    }

    void SetupEventTrigger()
    {
        EventTrigger trigger = GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        trigger.triggers.Clear();

        EventTrigger.Entry pointerDownEntry = new EventTrigger.Entry();
        pointerDownEntry.eventID = EventTriggerType.PointerDown;
        pointerDownEntry.callback.AddListener((data) => { OnPointerDown((PointerEventData)data); });
        trigger.triggers.Add(pointerDownEntry);

        EventTrigger.Entry pointerUpEntry = new EventTrigger.Entry();
        pointerUpEntry.eventID = EventTriggerType.PointerUp;
        pointerUpEntry.callback.AddListener((data) => { OnPointerUp((PointerEventData)data); });
        trigger.triggers.Add(pointerUpEntry);

        EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry();
        pointerExitEntry.eventID = EventTriggerType.PointerExit;
        pointerExitEntry.callback.AddListener((data) => { OnPointerExit((PointerEventData)data); });
        trigger.triggers.Add(pointerExitEntry);
    }

    void OnPointerDown(PointerEventData eventData)
    {
        var button = GetComponent<Button>();
        if (button != null && !button.interactable)
        {
            Debug.LogWarning("GraffitiButtonLongPress: Button is not interactable!");
            return;
        }

        isPressed = true;
        isLongPressTriggered = false;
        hasLongPressed = false;
        pointerDownTime = Time.time;

        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        rectTransform.localScale = originalScale;
        scaleCoroutine = StartCoroutine(ScaleAnimation(pressScale));

        if (longPressCoroutine != null)
        {
            StopCoroutine(longPressCoroutine);
        }
        longPressCoroutine = StartCoroutine(LongPressDetection());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;

        if (longPressCoroutine != null)
        {
            StopCoroutine(longPressCoroutine);
            longPressCoroutine = null;
        }

        float pressDuration = Time.time - pointerDownTime;

        if (isLongPressTriggered && appStateController != null)
        {
            appStateController.StopGraffiti();
        }
        else if (pressDuration < longPressDuration && !hasLongPressed)
        {
            if (painter != null)
            {
                painter.SetShapeCircle();
            }
            TogglePanelGraffitiOptions();
            StartCoroutine(EnhancedClickFeedback());
        }
        else
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }
            scaleCoroutine = StartCoroutine(ScaleAnimation(1.0f));
        }

        isLongPressTriggered = false;
        hasLongPressed = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isPressed)
        {
            OnPointerUp(eventData);
        }
    }

    IEnumerator LongPressDetection()
    {
        yield return new WaitForSeconds(longPressDuration);

        if (isPressed && appStateController != null)
        {
            isLongPressTriggered = true;
            hasLongPressed = true;
            appStateController.StartGraffiti();
        }
        else if (appStateController == null)
        {
            Debug.LogWarning("GraffitiButtonLongPress: Long press detected but AppStateControllerPhone is null!");
        }
    }

    IEnumerator ScaleAnimation(float targetScale)
    {
        if (rectTransform == null) yield break;

        Vector3 startScale = rectTransform.localScale;
        Vector3 endScale = originalScale * targetScale;
        float elapsed = 0f;

        while (elapsed < scaleAnimationDuration)
        {
            if (rectTransform == null) yield break;
            
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scaleAnimationDuration);
            t = Mathf.SmoothStep(0f, 1f, t);
            rectTransform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        if (rectTransform != null)
        {
            rectTransform.localScale = endScale;
        }
        scaleCoroutine = null;
    }

    IEnumerator EnhancedClickFeedback()
    {
        if (rectTransform == null) yield break;

        Image btnImage = GetComponent<Image>();
        Color originalColor = Color.white;
        if (btnImage != null)
        {
            originalColor = btnImage.color;
        }

        Vector3 currentScale = rectTransform.localScale;
        Vector3 pressedScale = originalScale * 0.75f;
        float duration = 0.2f;

        float elapsed = 0f;
        float pressDuration = duration * 0.3f;
        while (elapsed < pressDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pressDuration);
            rectTransform.localScale = Vector3.Lerp(currentScale, pressedScale, t);
            
            if (btnImage != null)
            {
                Color grayColor = Color.Lerp(originalColor, originalColor * 0.5f, t);
                btnImage.color = grayColor;
            }
            yield return null;
        }

        elapsed = 0f;
        float dazzleDuration = duration * 0.2f;
        Color dazzleColor = originalColor * 1.5f;
        while (elapsed < dazzleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dazzleDuration);
            if (btnImage != null)
            {
                Color flashColor = Color.Lerp(originalColor * 0.5f, dazzleColor, Mathf.Sin(t * Mathf.PI));
                btnImage.color = flashColor;
            }
            yield return null;
        }

        elapsed = 0f;
        float bounceDuration = duration * 0.5f;
        Vector3 bounceScale = originalScale * 1.1f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            float bounceT = 1f - Mathf.Pow(1f - t, 3f);
            rectTransform.localScale = Vector3.Lerp(pressedScale, bounceScale, bounceT);
            
            if (btnImage != null)
            {
                Color restoreColor = Color.Lerp(dazzleColor, originalColor, bounceT);
                btnImage.color = restoreColor;
            }
            yield return null;
        }

        elapsed = 0f;
        float settleDuration = duration * 0.3f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            rectTransform.localScale = Vector3.Lerp(bounceScale, originalScale, t);
            yield return null;
        }

        rectTransform.localScale = originalScale;
        if (btnImage != null)
        {
            btnImage.color = originalColor;
        }
    }

    void OnDisable()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale = originalScale;
        }
        isPressed = false;
        isLongPressTriggered = false;
        if (longPressCoroutine != null)
        {
            StopCoroutine(longPressCoroutine);
            longPressCoroutine = null;
        }
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }
    }
    

    void TogglePanelGraffitiOptions()
    {
        if (panelGraffitiOptions != null)
        {
            bool isCurrentlyActive = panelGraffitiOptions.activeSelf;
            panelGraffitiOptions.SetActive(!isCurrentlyActive);
        }
        else
        {
            Transform childPanel = transform.Find("PanelGraffitiOptions");
            GameObject optionsObj = null;
            if (childPanel != null)
            {
                optionsObj = childPanel.gameObject;
            }
            else
            {
                optionsObj = GameObject.Find("PanelGraffitiOptions");
                if (optionsObj == null)
                {
                    optionsObj = GameObject.Find("GraffitiOptionsPanel");
                    if (optionsObj == null)
                    {
                        optionsObj = GameObject.Find("Panel_GraffitiOptions");
                    }
                }
            }
            
            if (optionsObj != null)
            {
                bool isCurrentlyActive = optionsObj.activeSelf;
                optionsObj.SetActive(!isCurrentlyActive);
                panelGraffitiOptions = optionsObj; 
            }
        }
    }
}
