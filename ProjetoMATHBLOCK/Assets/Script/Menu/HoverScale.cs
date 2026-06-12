using UnityEngine;
using UnityEngine.EventSystems;

public class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float escalaHover = 1.1f;
    [SerializeField] private float velocidade = 8f;

    private Vector3 escalaOriginal;
    private Vector3 escalaAlvo;

    private void Start()
    {
        escalaOriginal = transform.localScale;
        escalaAlvo = escalaOriginal;
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            escalaAlvo,
            velocidade * Time.deltaTime
        );
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        escalaAlvo = escalaOriginal * escalaHover;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        escalaAlvo = escalaOriginal;
    }
}