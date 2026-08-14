using UnityEngine;

public class ResponsiveScribbles : MonoBehaviour
{
    [Header("Resolução base do menu")]
    [SerializeField] private Vector2 referenciaResolucao = new Vector2(1920f, 1080f);

    private RectTransform[] scribbles;
    private Vector2[] posicoesIniciais;

    private void Awake()
    {
        CacheScribbles();
        AjustarPosicoes();
    }

    private void OnEnable()
    {
        CacheScribbles();
        AjustarPosicoes();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled)
        {
            AjustarPosicoes();
        }
    }

    private void CacheScribbles()
    {
        var filhos = GetComponentsInChildren<RectTransform>(true);
        int quantidade = 0;

        for (int i = 0; i < filhos.Length; i++)
        {
            if (filhos[i] != transform)
            {
                quantidade++;
            }
        }

        scribbles = new RectTransform[quantidade];
        posicoesIniciais = new Vector2[quantidade];

        int indice = 0;
        for (int i = 0; i < filhos.Length; i++)
        {
            if (filhos[i] == transform)
                continue;

            scribbles[indice] = filhos[i];
            posicoesIniciais[indice] = filhos[i].anchoredPosition;
            indice++;
        }
    }

    private void AjustarPosicoes()
    {
        if (scribbles == null || scribbles.Length == 0)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();

        float larguraTela = canvas != null ? canvas.pixelRect.width : Screen.width;
        float alturaTela = canvas != null ? canvas.pixelRect.height : Screen.height;

        float escalaX = referenciaResolucao.x > 0f ? larguraTela / referenciaResolucao.x : 1f;
        float escalaY = referenciaResolucao.y > 0f ? alturaTela / referenciaResolucao.y : 1f;

        for (int i = 0; i < scribbles.Length; i++)
        {
            Vector2 posicaoBase = posicoesIniciais[i];
            scribbles[i].anchoredPosition = new Vector2(
                posicaoBase.x * escalaX,
                posicaoBase.y * escalaY
            );
        }
    }
}
