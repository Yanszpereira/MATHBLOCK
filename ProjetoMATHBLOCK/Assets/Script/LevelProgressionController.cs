using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LevelProgressionController : MonoBehaviour
{
    private const float CheckInterval = 0.35f;
    private const float TransitionDelay = 2.25f;

    private DoorOpener[] doors;
    private float nextCheckTime;
    private bool transitionStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneInitializer()
    {
        GlobalSceneBootstrap.Register(Install);
    }

    private static void Install(Scene scene)
    {
        if (!scene.name.StartsWith("Fase") ||
            FindFirstObjectByType<LevelProgressionController>() != null)
            return;
        GameObject progressionObject = new GameObject("Level Progression");
        progressionObject.AddComponent<LevelProgressionController>();
    }

    private void Start()
    {
        RefreshDoors();
    }

    private void Update()
    {
        if (transitionStarted || Time.unscaledTime < nextCheckTime)
            return;
        nextCheckTime = Time.unscaledTime + CheckInterval;

        if (doors == null || doors.Length == 0)
        {
            RefreshDoors();
            return;
        }

        bool foundActiveDoor = false;
        foreach (DoorOpener door in doors)
        {
            if (door == null || !door.gameObject.activeInHierarchy)
                continue;
            foundActiveDoor = true;
            if (!door.HasOpened)
                return;
        }

        if (foundActiveDoor)
            StartCoroutine(LoadNextScene());
    }

    private void RefreshDoors()
    {
        doors = FindObjectsByType<DoorOpener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private IEnumerator LoadNextScene()
    {
        transitionStarted = true;
        WinScreenController winScreen = FindWinScreen();
        if (winScreen != null)
        {
            winScreen.gameObject.SetActive(false);
            winScreen.gameObject.SetActive(true);
        }

        yield return new WaitForSecondsRealtime(TransitionDelay);

        string nextScene = GetNextScene(SceneManager.GetActiveScene().name);
        Time.timeScale = 1f;
        Cursor.lockState = nextScene == "MainMenu" ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = nextScene == "MainMenu";

        if (winScreen != null)
            winScreen.gameObject.SetActive(false);

        SceneTransitionManager.TryTransitionToScene(nextScene, this);
    }

    private static string GetNextScene(string currentScene)
    {
        switch (currentScene)
        {
            case "Fase 1": return "Fase 2";
            case "Fase 2": return "Fase 3";
            default: return "MainMenu";
        }
    }

    private static WinScreenController FindWinScreen()
    {
        WinScreenController[] screens = FindObjectsByType<WinScreenController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (WinScreenController screen in screens)
        {
            if (screen.gameObject.name == "WinScreen")
                return screen;
        }

        Debug.LogError(
            $"A cena '{SceneManager.GetActiveScene().name}' não contém um objeto chamado 'WinScreen' com WinScreenController.");
        return null;
    }
}
