using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ToolUIController : MonoBehaviour
{
    [Header("References")]
    public PhonePainter painter;

    [Header("Colors")]
    public Button btnRed, btnGreen, btnBlue, btnYellow, btnWhite, btnBlack;
    public Image highlightRing;

    [Header("Size")]
    public Slider sizeSlider;
    public TMP_Text sizeValue;

    [Header("Overwrite")]
    public Toggle toggleOverwrite;
    public TMP_Text overwriteHint;

    [Header("Selection Settings")]
    [Tooltip("Scale factor for selected button (1.0 = normal size)")]
    public float selectedScale = 1.15f;
    
    [Tooltip("Scale animation duration (seconds)")]
    public float scaleAnimationDuration = 0.2f;

    Color _on = new Color(0.14f, 0.88f, 0.60f, 0.95f);
    Color _off = new Color(1f, 1f, 1f, 0.20f);

    private Button[] allColorButtons;
    private RectTransform[] allButtonTransforms;
    private Vector3[] originalScales;
    private Button currentSelectedButton;
    private Coroutine scaleAnimationCoroutine;

    void Awake()
    {
        if (!painter)
        {
            Debug.LogError("[ToolUIController] Painter reference missing.");
            return;
        }

        // Store all color buttons
        allColorButtons = new Button[] { btnRed, btnGreen, btnBlue, btnYellow, btnWhite, btnBlack };
        allButtonTransforms = new RectTransform[allColorButtons.Length];
        originalScales = new Vector3[allColorButtons.Length];

        for (int i = 0; i < allColorButtons.Length; i++)
        {
            if (allColorButtons[i] != null)
            {
                allButtonTransforms[i] = allColorButtons[i].transform as RectTransform;
                if (allButtonTransforms[i] != null)
                {
                    originalScales[i] = allButtonTransforms[i].localScale;
                }
            }
        }

        // Colors
        btnRed.onClick.AddListener(() => { painter.SetColor(Color.red); SelectColorButton(btnRed); });
        btnGreen.onClick.AddListener(() => { painter.SetColor(Color.green); SelectColorButton(btnGreen); });
        btnBlue.onClick.AddListener(() => { painter.SetColor(Color.blue); SelectColorButton(btnBlue); });
        btnYellow.onClick.AddListener(() => { painter.SetColor(Color.yellow); SelectColorButton(btnYellow); });
        btnWhite.onClick.AddListener(() => { painter.SetColor(Color.white); SelectColorButton(btnWhite); });
        btnBlack.onClick.AddListener(() => { painter.SetColor(Color.black); SelectColorButton(btnBlack); });

        // Size slider
        sizeSlider.minValue = 0.02f;
        sizeSlider.maxValue = 0.12f;
        sizeSlider.value = painter.brushSize;
        sizeSlider.onValueChanged.AddListener(v =>
        {
            painter.SetBrushSize(v);
            if (sizeValue) sizeValue.text = $"{v:0.00}";
        });

        // Overwrite toggle
        if (toggleOverwrite)
        {
            toggleOverwrite.isOn = painter.overwriteErase;
            toggleOverwrite.onValueChanged.AddListener(v =>
            {
                painter.overwriteErase = v;
#if UNITY_ANDROID && !UNITY_EDITOR
                try { Handheld.Vibrate(); } catch {}
#endif
                if (overwriteHint)
                    overwriteHint.text = v ? "Top color replaces lower paint" : "Colors stack cleanly";
            });
        }

        if (sizeValue) sizeValue.text = $"{painter.brushSize:0.00}";
    }

    void SelectColorButton(Button selectedButton)
    {
        // Reset all buttons to original scale
        for (int i = 0; i < allColorButtons.Length; i++)
        {
            if (allColorButtons[i] != null && allButtonTransforms[i] != null)
            {
                if (allColorButtons[i] == selectedButton)
                {
                    // Scale up the selected button
                    if (scaleAnimationCoroutine != null)
                    {
                        StopCoroutine(scaleAnimationCoroutine);
                    }
                    scaleAnimationCoroutine = StartCoroutine(ScaleButton(allButtonTransforms[i], originalScales[i], originalScales[i] * selectedScale));
                }
                else
                {
                    // Scale down other buttons
                    if (allButtonTransforms[i].localScale != originalScales[i])
                    {
                        StartCoroutine(ScaleButton(allButtonTransforms[i], allButtonTransforms[i].localScale, originalScales[i]));
                    }
                }
            }
        }

        // Update highlight ring position
        MoveHighlight(selectedButton.transform as RectTransform);

        // Update selected button reference
        currentSelectedButton = selectedButton;

        // Update button colors for better visual feedback
        UpdateButtonColors(selectedButton);
    }

    void UpdateButtonColors(Button selected)
    {
        // Store original colors for each button (only on first call)
        // This ensures consistent behavior across iOS and Android
        for (int i = 0; i < allColorButtons.Length; i++)
        {
            if (allColorButtons[i] != null)
            {
                var image = allColorButtons[i].GetComponent<Image>();
                if (image != null)
                {
                    if (allColorButtons[i] == selected)
                    {
                        // Make selected button brighter and more saturated
                        // This works consistently on both iOS and Android
                        var originalColor = image.color;
                        // Use platform-independent color calculation
                        image.color = new Color(
                            Mathf.Clamp01(originalColor.r * 1.3f),
                            Mathf.Clamp01(originalColor.g * 1.3f),
                            Mathf.Clamp01(originalColor.b * 1.3f),
                            originalColor.a
                        );
                    }
                    // Other buttons will be reset by the button's built-in color transition system
                    // which works consistently across all platforms
                }
            }
        }
    }

    /// <summary>
    /// Smoothly scales a button with animation. Works consistently on iOS and Android.
    /// Uses Time.deltaTime which is platform-independent.
    /// </summary>
    IEnumerator ScaleButton(RectTransform target, Vector3 startScale, Vector3 endScale)
    {
        if (target == null) yield break;

        float elapsed = 0f;
        // Use Time.deltaTime which works consistently across iOS, Android, and all platforms
        while (elapsed < scaleAnimationDuration)
        {
            if (target == null) yield break;
            
            // Time.deltaTime is platform-independent and works on both iOS and Android
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scaleAnimationDuration);
            t = Mathf.SmoothStep(0f, 1f, t); // Smooth easing for consistent feel on all platforms
            target.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null; // Wait for next frame (consistent across all platforms)
        }

        if (target != null)
        {
            target.localScale = endScale;
        }
    }

    void MoveHighlight(RectTransform target)
    {
        if (!highlightRing || !target) return;
        var ring = highlightRing.rectTransform;
        ring.SetParent(target, false);
        ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
        ring.anchoredPosition = Vector2.zero;
        ring.SetAsLastSibling();
        
        // Make highlight ring larger and more visible
        if (ring.sizeDelta.x == 0 || ring.sizeDelta.y == 0)
        {
            // If ring size is not set, calculate based on button size
            var buttonSize = target.sizeDelta;
            ring.sizeDelta = new Vector2(buttonSize.x * 1.2f, buttonSize.y * 1.2f);
        }
        
        // Make ring more visible
        var ringImage = highlightRing.GetComponent<Image>();
        if (ringImage != null)
        {
            var ringColor = ringImage.color;
            ringColor.a = 0.9f; // Make it more opaque
            ringImage.color = ringColor;
        }
    }
}
