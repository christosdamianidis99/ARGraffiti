using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class TouchBlocker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public void OnPointerDown(PointerEventData e) { e.Use(); }
    public void OnPointerUp(PointerEventData e) { e.Use(); }
    public void OnDrag(PointerEventData e) { e.Use(); }
}
