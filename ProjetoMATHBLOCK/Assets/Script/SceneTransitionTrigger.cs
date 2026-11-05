using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(BoxCollider))]
public sealed class SceneTransitionTrigger : MonoBehaviour
{
    private const string PlayerTag = "Player";

    [SerializeField]
    [Tooltip("Nome exato da cena de destino, conforme cadastrada no Build Settings.")]
    private string destinationSceneName;

    private BoxCollider triggerCollider;
    private bool transitionStarted;

    private void Reset()
    {
        ConfigureTriggerCollider();
    }

    private void Awake()
    {
        ConfigureTriggerCollider();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
            ConfigureTriggerCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Nas fases com recompensa, apenas o gatilho salvo na porta final pode
        // iniciar a conclusao. O portal antigo nao pode pular a apresentacao.
        if (FindFirstObjectByType<GatilhoConclusaoFase>(FindObjectsInactive.Include) != null) return;
        if (transitionStarted || other == null || !IsPlayer(other))
            return;

        StartCoroutine(LoadDestinationScene());
    }

    private bool IsPlayer(Collider other)
    {
        return other.CompareTag(PlayerTag) ||
               (other.transform.root != null && other.transform.root.CompareTag(PlayerTag));
    }

    private IEnumerator LoadDestinationScene()
    {
        if (string.IsNullOrWhiteSpace(destinationSceneName) ||
            !Application.CanStreamedLevelBeLoaded(destinationSceneName))
            yield break;

        RegistroConclusaoFase.TryRegistrarConclusaoDaCenaAtual(this);
        transitionStarted = true;
        string cenaAtual = SceneManager.GetActiveScene().name;
        if (cenaAtual != "Fase 4" && cenaAtual.StartsWith("Fase") &&
            ProgressoAprendizado.Instance != null &&
            ProgressoAprendizado.Instance.ConcluirFase(cenaAtual))
            yield return ResultadosAprendizadoUI.Mostrar(cenaAtual);

        if (!PhaseResultTransitionController.TryShowResult(
                destinationSceneName, cenaAtual == "Fase 4", this))
            transitionStarted = false;
    }

    public static bool TryLoadScene(string destinationSceneName, Object logContext = null)
    {
        if (string.IsNullOrWhiteSpace(destinationSceneName))
        {
            Debug.LogError(
                $"{nameof(SceneTransitionTrigger)} não possui uma cena de destino.",
                logContext);
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(destinationSceneName))
        {
            Debug.LogError(
                $"A cena de destino '{destinationSceneName}' não está no Build Settings ou não pode ser carregada.",
                logContext);
            return false;
        }

        return SceneTransitionManager.TryTransitionToScene(destinationSceneName, logContext);
    }

    private void ConfigureTriggerCollider()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<BoxCollider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }
}
