using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>So conclui ao aproximar o jogador da porta final ja liberada pelo puzzle.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class GatilhoConclusaoFase : MonoBehaviour
{
    [SerializeField] private DoorOpener portaFinal;
    private BoxCollider area;
    private bool consumido;
    public DoorOpener PortaFinal => portaFinal;
    public bool Consumido => consumido;

    private void Awake() { area = GetComponent<BoxCollider>(); area.isTrigger = true; }
    public void Configurar(DoorOpener porta) => portaFinal = porta;

    public bool PodeConcluir(Collider jogador)
    {
        if (consumido || portaFinal == null || !portaFinal.HasOpened || jogador == null) return false;
        if (area == null) area = GetComponent<BoxCollider>();
        bool ehJogador = jogador.CompareTag("Player") || jogador.transform.root.CompareTag("Player") ||
            jogador.GetComponentInParent<PlayerInput>() != null;
        return ehJogador && area.bounds.Intersects(jogador.bounds);
    }

    private void OnTriggerEnter(Collider outro) => TentarConcluir(outro);
    private void OnTriggerStay(Collider outro) => TentarConcluir(outro);

    private void TentarConcluir(Collider outro)
    {
        if (!PodeConcluir(outro)) return;
        var fluxo = FindFirstObjectByType<LevelProgressionController>();
        if (fluxo != null && fluxo.ConcluirPorAproximacao(this)) consumido = true;
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider caixa = GetComponent<BoxCollider>();
        if (caixa == null) return;
        Gizmos.color = new Color(.25f, .85f, .55f, .3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(caixa.center, caixa.size);
        Gizmos.color = Color.green; Gizmos.DrawWireCube(caixa.center, caixa.size);
    }
}
