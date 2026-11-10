using UnityEngine;

public class StampController : MonoBehaviour
{
    [SerializeField] private Transform objetoEmMovimento;
    [SerializeField] private Vector3 posicaoFinal;
    [SerializeField, Min(0.01f)] private float velocidade = 1f;
    [SerializeField] private GameObject objetoAtivar;

    private Vector3 posicaoOriginal;
    private bool movimentoIniciado;
    private bool retornando;
    private bool movimentoConcluido;

    private void Start()
    {
        if (objetoEmMovimento == null)
            objetoEmMovimento = transform;

        if (velocidade <= 0f)
        {
            Debug.LogError("A velocidade do StampController deve ser maior que zero.", this);
            enabled = false;
            return;
        }

        if (objetoAtivar == null)
        {
            Debug.LogError("Defina o objeto a ativar no retorno no Inspector.", this);
            enabled = false;
            return;
        }
    }

    private void Update()
    {
        if (movimentoConcluido)
            return;

        if (!movimentoIniciado)
        {
            if (!objetoEmMovimento.gameObject.activeInHierarchy)
                return;

            posicaoOriginal = objetoEmMovimento.position;
            movimentoIniciado = true;
        }

        Vector3 destino = retornando ? posicaoOriginal : posicaoFinal;
        objetoEmMovimento.position = Vector3.MoveTowards(
            objetoEmMovimento.position,
            destino,
            velocidade * Time.deltaTime);

        if (objetoEmMovimento.position != destino)
            return;

        if (!retornando)
        {
            retornando = true;
            objetoAtivar.SetActive(true);
        }
        else
        {
            movimentoConcluido = true;
        }
    }
}
