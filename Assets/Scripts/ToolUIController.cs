using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ToolUIController : MonoBehaviour
{
    [Header("References")]
    public PhonePainter painter;

    [Header("Shapes")]
    public Button btnCircle;
    public Button btnSquare;
    public Image imgCircleBG;
    public Image imgSquareBG;

    [Header("Colors")]
    public Button btnRed, btnGreen, btnBlue, btnYellow, btnWhite, btnBlack;
    public Image highlightRing;

    [Header("Size")]
    public Slider sizeSlider;
    public TMP_Text sizeValue;

    [Header("Overwrite")]
    public Toggle toggleOverwrite;
    public TMP_Text overwriteHint;

    Color _on = new Color(0.14f, 0.88f, 0.60f, 0.95f);
    Color _off = new Color(1f, 1f, 1f, 0.20f);

    void Awake()
    {
        if (!painter)
        {
            Debug.LogError("[ToolUIController] Painter reference missing.");
            return;
        }
        // Shapes
        btnCircle.onClick.AddListener(() => { painter.SetShapeCircle(); UpdateShapeUI(); });
        btnSquare.onClick.AddListener(() => { painter.SetShapeSquare(); UpdateShapeUI(); });

        // Colors
        btnRed.onClick.AddListener(() => { painter.SetColor(Color.red); MoveHighlight(btnRed.transform as RectTransform); });
        btnGreen.onClick.AddListener(() => { painter.SetColor(Color.green); MoveHighlight(btnGreen.transform as RectTransform); });
        btnBlue.onClick.AddListener(() => { painter.SetColor(Color.blue); MoveHighlight(btnBlue.transform as RectTransform); });
        btnYellow.onClick.AddListener(() => { painter.SetColor(Color.yellow); MoveHighlight(btnYellow.transform as RectTransform); });
        btnWhite.onClick.AddListener(() => { painter.SetColor(Color.white); MoveHighlight(btnWhite.transform as RectTransform); });
        btnBlack.onClick.AddListener(() => { painter.SetColor(Color.black); MoveHighlight(btnBlack.transform as RectTransform); });

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
            toggleOverwrite.isOn = painter.enableOverwriteErase;
            toggleOverwrite.onValueChanged.AddListener(v =>
            {
                painter.enableOverwriteErase = v;
#if UNITY_ANDROID && !UNITY_EDITOR
                try { Handheld.Vibrate(); } catch {}
#endif
                if (overwriteHint)
                    overwriteHint.text = v ? "Top color replaces lower paint" : "Colors stack cleanly";
            });
        }

        UpdateShapeUI();
        if (sizeValue) sizeValue.text = $"{painter.brushSize:0.00}";
    }

    void UpdateShapeUI()
    {
        bool isCircle = painter.shape == BrushShape.Circle;
        if (imgCircleBG) imgCircleBG.color = isCircle ? _on : _off;
        if (imgSquareBG) imgSquareBG.color = isCircle ? _off : _on;
    }

    void MoveHighlight(RectTransform target)
    {
        if (!highlightRing || !target) return;
        var ring = highlightRing.rectTransform;
        ring.SetParent(target, false);
        ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
        ring.anchoredPosition = Vector2.zero;
        ring.SetAsLastSibling();
    }
}
