using UnityEngine;
using UnityEngine.UI;

public class ButtonStyler : MonoBehaviour
{
    public Button graffitiButton;
    public Color active = new Color(0.13f, 0.77f, 0.37f, 0.85f);
    public Color inactive = new Color(1f, 1f, 1f, 0.25f);

    public void SetGraffitiActive(bool on)
    {
        var img = graffitiButton ? graffitiButton.GetComponent<Image>() : null;
        if (img) img.color = on ? active : inactive;
    }
}
