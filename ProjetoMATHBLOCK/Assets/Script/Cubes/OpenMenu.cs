using UnityEngine;

public class MenuTransition : MonoBehaviour
{
    [Header("Start Menu")]
    [SerializeField] private RectTransform startMenu;

    [Header("Destino")]
    [SerializeField] private Vector2 posicaoDestino;
    [SerializeField] private Vector3 rotacaoDestino;

    [Header("Menus")]
    [SerializeField] private GameObject menuOpcoes;
    [SerializeField] private GameObject menuCreditos;

    [Header("Animacao")]
    [SerializeField] private float velocidade = 6f;

    private Vector2 posicaoInicial;
    private Quaternion rotacaoInicial;

    private Vector2 alvoPosicao;
    private Quaternion alvoRotacao;

    private void Start()
    {
        posicaoInicial = startMenu.anchoredPosition;
        rotacaoInicial = startMenu.localRotation;

        alvoPosicao = posicaoInicial;
        alvoRotacao = rotacaoInicial;

        menuOpcoes.SetActive(false);
        menuCreditos.SetActive(false);
    }

    private void Update()
    {
        startMenu.anchoredPosition = Vector2.Lerp(
            startMenu.anchoredPosition,
            alvoPosicao,
            velocidade * Time.deltaTime
        );

        startMenu.localRotation = Quaternion.Lerp(
            startMenu.localRotation,
            alvoRotacao,
            velocidade * Time.deltaTime
        );
    }

    public void AbrirOpcoes()
    {
        menuCreditos.SetActive(false);
        menuOpcoes.SetActive(true);

        alvoPosicao = posicaoDestino;
        alvoRotacao = Quaternion.Euler(rotacaoDestino);
    }

    public void AbrirCreditos()
    {
        menuOpcoes.SetActive(false);
        menuCreditos.SetActive(true);

        alvoPosicao = posicaoDestino;
        alvoRotacao = Quaternion.Euler(rotacaoDestino);
    }

    public void Voltar()
    {
        menuOpcoes.SetActive(false);
        menuCreditos.SetActive(false);

        alvoPosicao = posicaoInicial;
        alvoRotacao = rotacaoInicial;
    }
}