using UnityEngine;
using UnityEngine.InputSystem;

public class ScrollCredits : MonoBehaviour
{
    [Header("Movimento Automático")]
    [SerializeField] private float velocidadeAutomatica = 30f;

    [Header("Scroll do Mouse")]
    [SerializeField] private float velocidadeScroll = 0.5f;

    [Header("Limites")]
    [SerializeField] private float limiteInferiorY;
    [SerializeField] private float limiteSuperiorY;

    private RectTransform rect;
    private Vector2 posicaoInicial;

    private bool scrollAutomatico = true;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        posicaoInicial = rect.anchoredPosition;
        rect.anchoredPosition = posicaoInicial;

        limiteInferiorY = posicaoInicial.y;

        scrollAutomatico = true;
    }

    private void Update()
    {
        float scroll = 0f;

        if (Mouse.current != null)
            scroll = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scroll) > 0.01f)
        {
            scrollAutomatico = false;

            Vector2 pos = rect.anchoredPosition;
            pos.y += -scroll * velocidadeScroll;

            pos.y = Mathf.Clamp(
                pos.y,
                limiteInferiorY,
                limiteSuperiorY
            );

            rect.anchoredPosition = pos;
        }
        else if (scrollAutomatico)
        {
            Vector2 pos = rect.anchoredPosition;
            pos.y += velocidadeAutomatica * Time.deltaTime;

            if (pos.y >= limiteSuperiorY)
            {
                pos.y = posicaoInicial.y;
            }

            rect.anchoredPosition = pos;
        }
    }
}