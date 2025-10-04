using UnityEngine;
using UnityEngine.UI;

public class ToolUIController : MonoBehaviour
{
    public PhonePainter painter;
    public Slider sizeSlider;
    public Button btnCircle, btnSquare;
    public Button btnRed, btnGreen, btnBlue, btnYellow, btnWhite, btnBlack;

    void Awake()
    {
        sizeSlider.minValue = 0.02f; sizeSlider.maxValue = 0.12f;
        sizeSlider.value = painter.brushSize;
        sizeSlider.onValueChanged.AddListener(painter.SetBrushSize);

        btnCircle.onClick.AddListener(painter.SetShapeCircle);
        btnSquare.onClick.AddListener(painter.SetShapeSquare);

        btnRed.onClick.AddListener(() => painter.SetColor(Color.red));
        btnGreen.onClick.AddListener(() => painter.SetColor(Color.green));
        btnBlue.onClick.AddListener(() => painter.SetColor(Color.blue));
        btnYellow.onClick.AddListener(() => painter.SetColor(Color.yellow));
        btnWhite.onClick.AddListener(() => painter.SetColor(Color.white));
        btnBlack.onClick.AddListener(() => painter.SetColor(Color.black));
    }
}
