using UnityEngine;

/// <summary>
/// Cross-platform Safe Area Adapter
/// Automatically adapts to notch devices (iPhone X series, Android punch-holes, etc.)
/// No adjustment is made for devices without a notch.
/// </summary>
public class SafeAreaAdapter : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea = new Rect(0, 0, 0, 0);
    private bool needsAdaptation = false;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    void Update()
    {
        // Only update if adaptation is needed and safe area has changed (e.g., device rotation)
        if (needsAdaptation && lastSafeArea != Screen.safeArea)
        {
            ApplySafeArea();
        }
    }

    void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;

        // Check if adaptation is actually needed
        // If the safe area is equal to the screen size, there is no notch, so no adjustment needed
        bool hasSafeAreaInsets =
            safeArea.x > 0 ||
            safeArea.y > 0 ||
            safeArea.width < Screen.width ||
            safeArea.height < Screen.height;

        if (!hasSafeAreaInsets)
        {
            // No notch, keep default layout (fill the whole screen)
            needsAdaptation = false;
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            return;
        }

        // Notch detected, adaptation needed
        needsAdaptation = true;

        // Convert safe area to anchors (normalized 0-1)
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;

        // Reset offsets so content fully fills the safe area
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
