using UnityEngine;
using UnityEngine.SceneManagement;

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
        if (transitionStarted || other == null || !IsPlayer(other))
            return;

        LoadDestinationScene();
    }

    private bool IsPlayer(Collider other)
    {
        return other.CompareTag(PlayerTag) ||
               (other.transform.root != null && other.transform.root.CompareTag(PlayerTag));
    }

    private void LoadDestinationScene()
    {
        if (!TryLoadScene(destinationSceneName, this))
            return;

        transitionStarted = true;
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

        SceneManager.LoadScene(destinationSceneName, LoadSceneMode.Single);
        return true;
    }

    private void ConfigureTriggerCollider()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<BoxCollider>();

        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }
}
