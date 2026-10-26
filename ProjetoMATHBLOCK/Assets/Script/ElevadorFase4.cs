
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ElevadorVerticalAutomatico : MonoBehaviour
{
    [Header("Destino")]
    [SerializeField] private Transform elevadorAlvo;

    [Header("Movimentacao")]
    [Min(0.1f)]
    [SerializeField] private float duracaoSubida = 6f;

    [Header("Jogador")]
    [SerializeField] private bool transportarJogador = true;

    private Rigidbody rb;
    private Transform jogador;
    private CharacterController controladorJogador;

    private Vector3 posicaoInicial;
    private Vector3 posicaoFinal;

    private float tempoDecorrido;
    private bool subindo;
    private bool concluido;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void OnTriggerEnter(Collider other)
    {
        RegistrarJogador(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RegistrarJogador(other);
    }

    private void RegistrarJogador(Collider other)
    {
        Transform candidato = other.transform;

        // Permite detectar colliders filhos do Player.
        while (candidato != null && !candidato.CompareTag("Player"))
        {
            candidato = candidato.parent;
        }

        if (candidato == null)
            return;

        jogador = candidato;
        controladorJogador = jogador.GetComponent<CharacterController>();

        if (subindo || concluido)
            return;

        IniciarSubida();
    }

    private void OnTriggerExit(Collider other)
    {
        if (jogador == null)
            return;

        if (other.transform == jogador ||
            other.transform.IsChildOf(jogador))
        {
            jogador = null;
            controladorJogador = null;
        }
    }

    private void IniciarSubida()
    {
        if (elevadorAlvo == null)
        {
            Debug.LogError(
                "[Elevador] ElevadorAlvo nao configurado!",
                this
            );
            return;
        }

        posicaoInicial = rb.position;

        // O elevador se move somente na vertical.
        posicaoFinal = new Vector3(
            posicaoInicial.x,
            elevadorAlvo.position.y,
            posicaoInicial.z
        );

        if (posicaoFinal.y <= posicaoInicial.y)
        {
            Debug.LogWarning(
                "[Elevador] O alvo deve estar acima do elevador.",
                this
            );
            return;
        }

        tempoDecorrido = 0f;
        subindo = true;

        Debug.Log("[Elevador] Subida iniciada.");
    }

    private void FixedUpdate()
    {
        if (!subindo)
            return;

        tempoDecorrido += Time.fixedDeltaTime;

        float progresso = Mathf.Clamp01(
            tempoDecorrido / Mathf.Max(0.1f, duracaoSubida)
        );

        // Aceleracao e desaceleracao simetricas.
        float suavizacao = Mathf.SmoothStep(
            0f,
            1f,
            progresso
        );

        Vector3 novaPosicao = Vector3.Lerp(
            posicaoInicial,
            posicaoFinal,
            suavizacao
        );

        Vector3 deslocamento = novaPosicao - rb.position;

        rb.MovePosition(novaPosicao);

        // Transporta o jogador enquanto estiver na zona.
        if (transportarJogador && jogador != null)
        {
            if (controladorJogador != null &&
                controladorJogador.enabled)
            {
                controladorJogador.Move(deslocamento);
            }
            else
            {
                jogador.position += deslocamento;
            }
        }

        if (progresso >= 1f)
        {
            subindo = false;
            concluido = true;

            Debug.Log("[Elevador] Destino alcancado.");
        }
    }
}
