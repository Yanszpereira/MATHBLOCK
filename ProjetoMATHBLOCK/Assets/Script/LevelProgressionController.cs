using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class LevelProgressionController : MonoBehaviour
{
    private const float CheckInterval = 0.35f;
    private const float TransitionDelay = 2.25f;

    private DoorOpener[] doors;
    private float nextCheckTime;
    private bool transitionStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!SceneManager.GetActiveScene().name.StartsWith("Fase"))
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
        ShowTransitionMessage();
        yield return new WaitForSecondsRealtime(TransitionDelay);

        string nextScene = GetNextScene(SceneManager.GetActiveScene().name);
        Time.timeScale = 1f;
        Cursor.lockState = nextScene == "MainMenu" ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = nextScene == "MainMenu";

        if (Application.CanStreamedLevelBeLoaded(nextScene))
            SceneManager.LoadScene(nextScene);
        else
            Debug.LogError($"Próxima cena '{nextScene}' não está no Build Settings.", this);
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

    private static void ShowTransitionMessage()
    {
        GameObject canvasObject = new GameObject("Level Complete Canvas", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject textObject = new GameObject("Message", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(760f, 120f);

        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = "FASE CONCLUÍDA!";
        text.fontSize = 52;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.86f, 1f, 0.96f, 1f);
        text.raycastTarget = false;
    }
}
