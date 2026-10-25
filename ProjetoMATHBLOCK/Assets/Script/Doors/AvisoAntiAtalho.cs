using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Uma unica brincadeira por partida ao tentar contornar a porta inicial fechada.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class AvisoAntiAtalho : MonoBehaviour
{
    [SerializeField] private DoorOpener porta;
    private BoxCollider area;
    public static bool JaExibido { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarSessao() => JaExibido = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegistrarNovaPartida() => GlobalSceneBootstrap.Register(cena =>
    {
        if (cena.name == "MainMenu") JaExibido = false;
    });

    public void Configurar(DoorOpener referencia) => porta = referencia;
    private void Awake() => area = GetComponent<BoxCollider>();
    private void OnTriggerEnter(Collider outro) => TentarMostrar(outro);
    private void OnTriggerStay(Collider outro) => TentarMostrar(outro);

    public bool TentarMostrar(Collider outro)
    {
        if (JaExibido || porta == null || porta.HasOpened || outro == null || Time.timeScale <= 0f)
            return false;
        bool jogador = outro.CompareTag("Player") || outro.GetComponentInParent<PlayerInput>() != null;
        if (!jogador || !area.bounds.Intersects(outro.bounds)) return false;
        JaExibido = true;
        DoorHintPresenter.MostrarAvisoAtalho();
        return true;
    }
}
