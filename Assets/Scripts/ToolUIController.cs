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

        btnRed.onClick.AddListener(() => { painter.SetColor(Color.red); SelectColorButton(btnRed); });
        btnGreen.onClick.AddListener(() => { painter.SetColor(Color.green); SelectColorButton(btnGreen); });
        btnBlue.onClick.AddListener(() => { painter.SetColor(Color.blue); SelectColorButton(btnBlue); });
        btnYellow.onClick.AddListener(() => { painter.SetColor(Color.yellow); SelectColorButton(btnYellow); });
        btnWhite.onClick.AddListener(() => { painter.SetColor(Color.white); SelectColorButton(btnWhite); });
        btnBlack.onClick.AddListener(() => { painter.SetColor(Color.black); SelectColorButton(btnBlack); });

        sizeSlider.minValue = 0.02f;
        sizeSlider.maxValue = 0.12f;
        sizeSlider.value = painter.brushSize;
        sizeSlider.onValueChanged.AddListener(v =>
        {
            painter.SetBrushSize(v);
            if (sizeValue) sizeValue.text = $"{v:0.00}";
        });

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
        for (int i = 0; i < allColorButtons.Length; i++)
        {
            if (allColorButtons[i] != null && allButtonTransforms[i] != null)
            {
                if (allColorButtons[i] == selectedButton)
                {
                    if (scaleAnimationCoroutine != null)
                    {
                        StopCoroutine(scaleAnimationCoroutine);
                    }
                    scaleAnimationCoroutine = StartCoroutine(ScaleButton(allButtonTransforms[i], originalScales[i], originalScales[i] * selectedScale));
                }
                else
                {
                    if (allButtonTransforms[i].localScale != originalScales[i])
                    {
                        StartCoroutine(ScaleButton(allButtonTransforms[i], allButtonTransforms[i].localScale, originalScales[i]));
                    }
                }
            }
        }

        MoveHighlight(selectedButton.transform as RectTransform);

        currentSelectedButton = selectedButton;

        UpdateButtonColors(selectedButton);
    }

    void UpdateButtonColors(Button selected)
    {

        for (int i = 0; i < allColorButtons.Length; i++)
        {
            if (allColorButtons[i] != null)
            {
                var image = allColorButtons[i].GetComponent<Image>();
                if (image != null)
                {
                    if (allColorButtons[i] == selected)
                    {

                        var originalColor = image.color;
                        image.color = new Color(
                            Mathf.Clamp01(originalColor.r * 1.3f),
                            Mathf.Clamp01(originalColor.g * 1.3f),
                            Mathf.Clamp01(originalColor.b * 1.3f),
                            originalColor.a
                        );
                    }
     
                }
            }
        }
    }


    IEnumerator ScaleButton(RectTransform target, Vector3 startScale, Vector3 endScale)
    {
        if (target == null) yield break;

        float elapsed = 0f;
        while (elapsed < scaleAnimationDuration)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / scaleAnimationDuration);
            t = Mathf.SmoothStep(0f, 1f, t);
            target.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
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

        if (ring.sizeDelta.x == 0 || ring.sizeDelta.y == 0)
        {
            var buttonSize = target.sizeDelta;
            ring.sizeDelta = new Vector2(buttonSize.x * 1.2f, buttonSize.y * 1.2f);
        }

        var ringImage = highlightRing.GetComponent<Image>();
        if (ringImage != null)
        {
            var ringColor = ringImage.color;
            ringColor.a = 0.9f; 
            ringImage.color = ringColor;
        }
    }
}