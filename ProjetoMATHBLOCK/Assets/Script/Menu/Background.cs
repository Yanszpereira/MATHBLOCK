using UnityEngine;
using UnityEngine.InputSystem;

public class MenuParallax : MonoBehaviour
{
    [SerializeField] private float intensidade = 30f;
    [SerializeField] private float suavidade = 5f;

    private RectTransform rectTransform;
    private Vector2 posicaoInicial;

    private void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        posicaoInicial = rectTransform.anchoredPosition;
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        Vector2 mouse = Mouse.current.position.ReadValue();

        float x = (mouse.x / Screen.width - 0.5f) * 2f;
        float y = (mouse.y / Screen.height - 0.5f) * 2f;

        Vector2 alvo = posicaoInicial + new Vector2(
    -x * intensidade,
    -y * intensidade
);

        rectTransform.anchoredPosition = Vector2.Lerp(
            rectTransform.anchoredPosition,
            alvo,
            suavidade * Time.deltaTime
        );
    }
}