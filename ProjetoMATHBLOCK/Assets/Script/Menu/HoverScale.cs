using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float escalaHover = 1.1f;
    [SerializeField] private float velocidade = 10f;
    [SerializeField] private Color corHover = new Color(0.72f, 1f, 0.92f, 1f);

    private Vector3 escalaOriginal;
    private Vector3 escalaAlvo;
    private Graphic graphic;
    private Color corOriginal;
    private Color corAlvo;
    private bool cursorSobre;

    private void Awake()
    {
        escalaOriginal = transform.localScale;
        escalaAlvo = escalaOriginal;
        graphic = GetComponent<Selectable>()?.targetGraphic ?? GetComponent<Graphic>();
        if (graphic != null)
            corOriginal = corAlvo = graphic.color;
    }

    private void OnEnable()
    {
        cursorSobre = false;
        escalaAlvo = escalaOriginal;
        corAlvo = corOriginal;
    }

    private void Update()
    {
        float t = 1f - Mathf.Exp(-velocidade * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, escalaAlvo, t);
        if (graphic != null)
            graphic.color = Color.Lerp(graphic.color, corAlvo, t);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        cursorSobre = true;
        // Alguns prefabs antigos possuem valor 3; limita para evitar o botao cobrir o menu.
        escalaAlvo = escalaOriginal * Mathf.Clamp(escalaHover, 1.06f, 1.18f);
        corAlvo = PreservarAlpha(corHover, corOriginal.a);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        cursorSobre = false;
        escalaAlvo = escalaOriginal;
        corAlvo = corOriginal;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        escalaAlvo = escalaOriginal * 0.94f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        escalaAlvo = cursorSobre
            ? escalaOriginal * Mathf.Clamp(escalaHover, 1.06f, 1.18f)
            : escalaOriginal;
    }

    private static Color PreservarAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
