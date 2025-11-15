using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Add click feedback effect to button (scale animation)
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonClickFeedback : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Scale ratio when clicked")]
    public float pressedScale = 0.9f;
    [Tooltip("Animation duration (seconds)")]
    public float animationDuration = 0.1f;
    [Tooltip("Enable color flash")]
    public bool enableColorFlash = true;
    [Tooltip("Flash color")]
    public Color flashColor = new Color(0.5f, 0.8f, 1f, 1f);

    private Button button;
    private RectTransform rectTransform;
    private Image buttonImage;
    private Vector3 originalScale;
    private Color originalColor;
    private bool isAnimating = false;

    void Awake()
    {
        button = GetComponent<Button>();
        rectTransform = GetComponent<RectTransform>();
        buttonImage = GetComponent<Image>();
        
        if (rectTransform)
            originalScale = rectTransform.localScale;
        
        if (buttonImage)
            originalColor = buttonImage.color;

        // Add click listener
        button.onClick.AddListener(OnButtonClicked);
    }

    void OnButtonClicked()
    {
        if (isAnimating) return;
        StartCoroutine(ClickAnimation());
    }

    IEnumerator ClickAnimation()
    {
        isAnimating = true;

        // Press effect: scale down
        float elapsed = 0f;
        Vector3 targetScale = originalScale * pressedScale;
        Color targetColor = enableColorFlash ? flashColor : originalColor;

        while (elapsed < animationDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (animationDuration * 0.5f);
            
            if (rectTransform)
                rectTransform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            
            if (buttonImage && enableColorFlash)
                buttonImage.color = Color.Lerp(originalColor, targetColor, t);

            yield return null;
        }

        // Release effect: scale back to original size
        elapsed = 0f;
        while (elapsed < animationDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (animationDuration * 0.5f);
            
            if (rectTransform)
                rectTransform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            
            if (buttonImage && enableColorFlash)
                buttonImage.color = Color.Lerp(targetColor, originalColor, t);

            yield return null;
        }

        // Ensure return to original state
        if (rectTransform)
            rectTransform.localScale = originalScale;
        
        if (buttonImage)
            buttonImage.color = originalColor;

        isAnimating = false;
    }

    void OnDestroy()
    {
        if (button)
            button.onClick.RemoveListener(OnButtonClicked);
    }
}

