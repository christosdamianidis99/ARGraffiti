// Runtime/UIButtonStyle.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image)), RequireComponent(typeof(Button))]
public class UIButtonStyle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    Image bg;
    Color normal, hover, down;

    void Awake()
    {
        bg = GetComponent<Image>();
        normal = bg.color;
        hover = Color.Lerp(normal, Color.white, 0.06f);
        down = Color.Lerp(normal, Color.black, 0.08f);
    }
    public void OnPointerDown(PointerEventData e) { bg.color = down; transform.localScale = Vector3.one * 0.98f; }
    public void OnPointerUp(PointerEventData e) { bg.color = hover; transform.localScale = Vector3.one; }
    public void OnPointerEnter(PointerEventData e) { bg.color = hover; }
    public void OnPointerExit(PointerEventData e) { bg.color = normal; transform.localScale = Vector3.one; }
}
