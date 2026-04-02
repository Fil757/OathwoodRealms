using UnityEngine;
using UnityEngine.EventSystems;

public class ToggleOnPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public GameObject showWhilePressed;
    public GameObject hideWhilePressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (showWhilePressed != null) showWhilePressed.SetActive(true);
        if (hideWhilePressed != null) hideWhilePressed.SetActive(false);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (showWhilePressed != null) showWhilePressed.SetActive(false);
        if (hideWhilePressed != null) hideWhilePressed.SetActive(true);
    }
}

