using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Minimal final portal completion signal for the Fase 4 presentation.</summary>
[DisallowMultipleComponent]
public sealed class Fase4PortalCompletionTrigger : MonoBehaviour
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    [SerializeField] private TMP_Text completionLabel;
    private bool completed;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger == null || !trigger.isTrigger)
            Debug.LogError("Fase4PortalCompletionTrigger precisa estar em um Collider marcado como Trigger.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (completed || other == null || other.GetComponentInParent<PlayerMovement>() == null)
            return;

        completed = true;
        if (completionLabel != null)
        {
            completionLabel.text = "FASE 4 CONCLUÍDA";
            completionLabel.gameObject.SetActive(true);
        }
        Debug.Log("FASE 4 CONCLUÍDA", this);

        int menuBuildIndex = SceneUtility.GetBuildIndexByScenePath(MainMenuScenePath);
        if (menuBuildIndex < 0)
        {
            Debug.LogError($"A cena de menu '{MainMenuScenePath}' não está incluída no Build Settings/Profiles.", this);
            return;
        }

        SceneManager.LoadScene(menuBuildIndex, LoadSceneMode.Single);
    }
}
