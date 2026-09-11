using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
public sealed class SceneTransitionManager : MonoBehaviour
{
    private const string ResourceName = "SceneTransitionManager";
    private const int TransitionSortingOrder = 32760;
    private const float MaximumFadeDeltaTime = 1f / 30f;

    public static SceneTransitionManager Instance { get; private set; }
    public static bool IsTransitioning => Instance != null && Instance.isTransitioning;

    [Header("Fade")]
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.6f;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.6f;
    [SerializeField] private Color fadeColor = Color.black;

    [Header("UI")]
    [SerializeField] private Canvas transitionCanvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image fadeImage;

    private readonly List<PlayerInput> blockedPlayerInputs = new();
    private bool isTransitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => EnsureInstance();

    private static SceneTransitionManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        SceneTransitionManager existing = FindFirstObjectByType<SceneTransitionManager>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        SceneTransitionManager prefab = Resources.Load<SceneTransitionManager>(ResourceName);
        if (prefab != null)
            return Instantiate(prefab);

        return new GameObject(ResourceName).AddComponent<SceneTransitionManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureOverlay();
        SetFadeAlpha(1f);
        SetInputBlock(true);
    }

    private IEnumerator Start()
    {
        isTransitioning = true;
        BlockGameplayInput();
        yield return null;
        BlockGameplayInput();
        yield return FadeTo(0f, fadeInDuration);
        FinishTransition();
    }

    public static bool TryTransitionToScene(string sceneName, Object logContext = null)
    {
        SceneTransitionManager manager = EnsureInstance();
        return manager != null && manager.TryBeginTransition(sceneName, logContext);
    }

    public static bool TryTransitionToScene(int buildIndex, Object logContext = null)
    {
        string scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        if (string.IsNullOrWhiteSpace(scenePath))
        {
            Debug.LogError($"Build index de destino '{buildIndex}' nao e valido.", logContext);
            return false;
        }

        SceneTransitionManager manager = EnsureInstance();
        if (manager == null || manager.isTransitioning)
            return false;

        manager.StartCoroutine(manager.TransitionRoutine(buildIndex, scenePath));
        return true;
    }

    public static bool TryReloadCurrentScene(Object logContext = null) =>
        TryTransitionToScene(SceneManager.GetActiveScene().buildIndex, logContext);

    public static bool TryExecuteWithFade(System.Action action, Object logContext = null)
    {
        if (action == null)
        {
            Debug.LogError("A acao executada durante o fade nao foi informada.", logContext);
            return false;
        }

        SceneTransitionManager manager = EnsureInstance();
        if (manager == null || manager.isTransitioning)
            return false;

        manager.StartCoroutine(manager.FadeActionRoutine(action));
        return true;
    }

    private bool TryBeginTransition(string sceneName, Object logContext)
    {
        if (isTransitioning)
            return false;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("Nome da cena de destino nao foi informado.", logContext);
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"A cena de destino '{sceneName}' nao esta no Build Settings ou nao pode ser carregada.",
                logContext);
            return false;
        }

        StartCoroutine(TransitionRoutine(sceneName));
        return true;
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        BeginTransition();
        yield return FadeTo(1f, fadeOutDuration);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
        {
            Debug.LogError($"Nao foi possivel iniciar o carregamento de '{sceneName}'.", this);
            yield return RecoverFromLoadFailure();
            yield break;
        }

        yield return LoadSceneAndPrepare(operation);
        yield return FadeTo(0f, fadeInDuration);
        FinishTransition();
    }

    private IEnumerator TransitionRoutine(int buildIndex, string scenePath)
    {
        BeginTransition();
        yield return FadeTo(1f, fadeOutDuration);

        AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
        if (operation == null)
        {
            Debug.LogError($"Nao foi possivel iniciar o carregamento de '{scenePath}'.", this);
            yield return RecoverFromLoadFailure();
            yield break;
        }

        yield return LoadSceneAndPrepare(operation);
        yield return FadeTo(0f, fadeInDuration);
        FinishTransition();
    }

    private IEnumerator FadeActionRoutine(System.Action action)
    {
        BeginTransition();
        yield return FadeTo(1f, fadeOutDuration);

        try
        {
            action();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception, this);
        }

        yield return null;
        yield return FadeTo(0f, fadeInDuration);
        FinishTransition();
    }

    private void BeginTransition()
    {
        BlockResizeController.ExitAllForSceneTransition();
        isTransitioning = true;
        SetInputBlock(true);
        BlockGameplayInput();
    }

    private IEnumerator LoadSceneAndPrepare(AsyncOperation operation)
    {
        while (!operation.isDone)
            yield return null;

        SetFadeAlpha(1f);
        BlockGameplayInput();

        // Executa Start e a primeira atualizacao da cena nova atras do preto.
        yield return null;
        BlockGameplayInput();
    }

    private IEnumerator RecoverFromLoadFailure()
    {
        yield return FadeTo(0f, fadeInDuration);
        FinishTransition();
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        if (duration <= 0f)
        {
            SetFadeAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;
        SetFadeAlpha(startAlpha);

        // Descarta o delta acumulado pelo carregamento da cena. Sem isso, um
        // frame longo pode consumir toda a duracao do fade in de uma vez.
        yield return null;

        while (elapsed < duration)
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaximumFadeDeltaTime);
            SetFadeAlpha(Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetFadeAlpha(targetAlpha);
    }

    private void BlockGameplayInput()
    {
        foreach (PlayerInput playerInput in FindObjectsByType<PlayerInput>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (playerInput == null || !playerInput.inputIsActive || blockedPlayerInputs.Contains(playerInput))
                continue;

            playerInput.DeactivateInput();
            blockedPlayerInputs.Add(playerInput);
        }
    }

    private void FinishTransition()
    {
        for (int index = blockedPlayerInputs.Count - 1; index >= 0; index--)
        {
            PlayerInput playerInput = blockedPlayerInputs[index];
            if (playerInput != null && playerInput.enabled && playerInput.gameObject.activeInHierarchy)
                playerInput.ActivateInput();
        }

        blockedPlayerInputs.Clear();
        BlockResizeController.RestoreGameplayControls(SceneManager.GetActiveScene());
        SetInputBlock(false);
        isTransitioning = false;
    }

    private void SetInputBlock(bool blocked)
    {
        canvasGroup.blocksRaycasts = blocked;
        canvasGroup.interactable = blocked;
        fadeImage.raycastTarget = blocked;
    }

    private void SetFadeAlpha(float alpha) => canvasGroup.alpha = Mathf.Clamp01(alpha);

    private void EnsureOverlay()
    {
        if (transitionCanvas == null)
        {
            GameObject canvasObject = new GameObject(
                "Transition Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            transitionCanvas = canvasObject.GetComponent<Canvas>();
            canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        }

        transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        transitionCanvas.sortingOrder = TransitionSortingOrder;

        CanvasScaler scaler = transitionCanvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (canvasGroup == null)
            canvasGroup = transitionCanvas.GetComponent<CanvasGroup>();

        if (fadeImage == null)
        {
            GameObject imageObject = new GameObject("Fade Image", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(transitionCanvas.transform, false);
            fadeImage = imageObject.GetComponent<Image>();
        }

        RectTransform rect = fadeImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        fadeImage.color = fadeColor;
    }
}
