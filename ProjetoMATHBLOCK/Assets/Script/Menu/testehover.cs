using UnityEngine;
using UnityEngine.EventSystems;

public class TesteHover : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("Mouse entrou!");
    }
}