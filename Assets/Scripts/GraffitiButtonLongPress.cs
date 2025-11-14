using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// Handles long press events and scaling animations for Graffiti button.
/// Long press to start graffiti, release to stop graffiti.
/// </summary>
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

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = gameObject.AddComponent<RectTransform>();
        }
        originalScale = rectTransform.localScale;

        // Ensure Image can receive raycasts
        var image = GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
        }

        // Ensure button can receive events
        var button = GetComponent<Button>();
        if (button != null)
        {
            // Ensure button is interactable
            button.interactable = true;
            
            // Disable Button onClick as we use EventTrigger instead
            button.onClick.RemoveAllListeners();
        }
    }

    void Start()
    {
        // Find AppStateControllerPhone in Start (could be not initialized in Awake)
        if (appStateController == null)
        {
            appStateController = FindObjectOfType<AppStateControllerPhone>();
            if (appStateController == null)
            {
                Debug.LogWarning("GraffitiButtonLongPress: AppStateControllerPhone not found in Start!");
            }
        }

        // Find PhonePainter to set shape
        if (painter == null)
        {
            painter = FindObjectOfType<PhonePainter>();
            if (painter == null)
            {
                Debug.LogWarning("GraffitiButtonLongPress: PhonePainter not found in Start!");
            }
        }

        // Set up EventTrigger
        SetupEventTrigger();
    }

    void SetupEventTrigger()
    {
        // Get or add EventTrigger component
        EventTrigger trigger = GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<EventTrigger>();
        }

        // Clear existing events
        trigger.triggers.Clear();

        // Add PointerDown event
        EventTrigger.Entry pointerDownEntry = new EventTrigger.Entry();
        pointerDownEntry.eventID = EventTriggerType.PointerDown;
        pointerDownEntry.callback.AddListener((data) => { OnPointerDown((PointerEventData)data); });
        trigger.triggers.Add(pointerDownEntry);

        // Add PointerUp event
        EventTrigger.Entry pointerUpEntry = new EventTrigger.Entry();
        pointerUpEntry.eventID = EventTriggerType.PointerUp;
        pointerUpEntry.callback.AddListener((data) => { OnPointerUp((PointerEventData)data); });
        trigger.triggers.Add(pointerUpEntry);

        // Add PointerExit event
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

        // Immediately start scale animation (do not wait for coroutine)
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        // Reset to original scale before animating to target scale
        rectTransform.localScale = originalScale;
        scaleCoroutine = StartCoroutine(ScaleAnimation(pressScale));

        // Start long press detection
        if (longPressCoroutine != null)
        {
            StopCoroutine(longPressCoroutine);
        }
        longPressCoroutine = StartCoroutine(LongPressDetection());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;

        // Stop long press detection
        if (longPressCoroutine != null)
        {
            StopCoroutine(longPressCoroutine);
            longPressCoroutine = null;
        }

        // Calculate press duration
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
            // Start enhanced click feedback
            StartCoroutine(EnhancedClickFeedback());
        }
        else
        {
            // Restore scale for long press
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
        // Treat pointer exit as pointer up (consider as release)
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
            // Use easing function for smoother animation
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

    // Enhanced click feedback with gray out and dazzle effect
    IEnumerator EnhancedClickFeedback()
    {
        if (rectTransform == null) yield break;

        // Get Image component for color change
        Image btnImage = GetComponent<Image>();
        Color originalColor = Color.white;
        if (btnImage != null)
        {
            originalColor = btnImage.color;
        }

        Vector3 currentScale = rectTransform.localScale;
        Vector3 pressedScale = originalScale * 0.75f;
        float duration = 0.2f;

        // Phase 1: Press-in effect with gray out
        float elapsed = 0f;
        float pressDuration = duration * 0.3f;
        while (elapsed < pressDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pressDuration);
            rectTransform.localScale = Vector3.Lerp(currentScale, pressedScale, t);
            
            // Gray out effect
            if (btnImage != null)
            {
                Color grayColor = Color.Lerp(originalColor, originalColor * 0.5f, t);
                btnImage.color = grayColor;
            }
            yield return null;
        }

        // Phase 2: Dazzle effect
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

        // Phase 3: Restore with bounce
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

        // Phase 4: Final settle
        elapsed = 0f;
        float settleDuration = duration * 0.3f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / settleDuration);
            rectTransform.localScale = Vector3.Lerp(bounceScale, originalScale, t);
            yield return null;
        }

        // Ensure final state
        rectTransform.localScale = originalScale;
        if (btnImage != null)
        {
            btnImage.color = originalColor;
        }
    }

    void OnDisable()
    {
        // Restore to original state when disabled
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
}
