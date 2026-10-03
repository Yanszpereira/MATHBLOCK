using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class SleepAnimation : MonoBehaviour
{
    [Header("Piscadas")]
    [SerializeField, Min(0f), Tooltip("Intervalo de espera entre piscadas, em segundos.")]
    private float blinkInterval = 5f;
    [SerializeField, Min(0.1f), Tooltip("Velocidade de fechamento das piscadas normais.")]
    private float blinkSpeed = 3.5f;
    [SerializeField, Min(0f), Tooltip("Tempo que as pálpebras permanecem fechadas, em segundos.")]
    private float blinkHoldDuration = 0.08f;
    [SerializeField, Range(0.1f, 1f), Tooltip("Abertura como fração da velocidade de fechamento.")]
    private float blinkOpeningSpeedMultiplier = 0.7f;
    [SerializeField, Min(0.1f), Tooltip("Velocidade de fechamento da piscada que troca de cena.")]
    private float finalBlinkSpeed = 0.7f;

    [Header("Rotações após o fade")]
    [SerializeField, Tooltip("Incrementos X, Y e Z do primeiro giro nos eixos de referência do jogador.")]
    private Vector3 firstTurnEuler = new Vector3(0f, 85f, 0f);
    [SerializeField, Min(0.1f), Tooltip("Duração do primeiro giro, em segundos.")]
    private float firstTurnDuration = 5f;
    [SerializeField, Tooltip("Incrementos X, Y e Z do segundo giro nos eixos de referência do jogador.")]
    private Vector3 secondTurnEuler = new Vector3(0f, 85f, 0f);
    [SerializeField, Min(0.1f), Tooltip("Duração do segundo giro, em segundos.")]
    private float secondTurnDuration = 5f;

    [Header("Objetos das piscadas")]
    [SerializeField] private GameObject aluno1;
    [SerializeField] private GameObject numero7;
    [SerializeField] private GameObject provaJogador;
    [SerializeField] private GameObject redcube;

    [Header("Cena após a quarta piscada")]
    [SerializeField] private string sceneToLoad;

    private RectTransform upperLid;
    private RectTransform lowerLid;
    private GameObject blinkCanvasObject;
    private SleepEyeTransition eyeTransition;
    private CanvasGroup skipButtonGroup;
    private Transform initialTurnTarget;
    private Quaternion initialTurnStartRotation;
    private Quaternion firstTurnEndRotation;
    private Quaternion secondTurnEndRotation;
    private Quaternion rotationAnimationStart;
    private Quaternion rotationAnimationTarget;
    private float intervalElapsed;
    private float initialTurnElapsed;
    private float rotationAnimationDuration;
    private float blinkHoldElapsed;
    private float blinkProgress;
    private int blinkCount;
    private bool isBlinking;
    private bool isClosing = true;
    private bool isHoldingClosed;
    private bool transitioningToScene;
    private bool initialTurnStarted;
    private bool rotationAnimationActive;
    private bool returnRotationPending;
    private bool entryBlinkOnly;
    private bool entryBlinkStarted;
    private bool entryBlinkFinished;
    private bool skipTransitionStarted;
    private int rotationSequenceStep;

    private void Awake()
    {
        bool isMainScene = gameObject.scene.name == "MainScene";
        entryBlinkOnly = gameObject.scene.name == "Fase 1";
        if (!isMainScene && !entryBlinkOnly)
        {
            enabled = false;
            return;
        }

        if (isMainScene)
        {
            initialTurnTarget = CreateCameraTurnPivot();
            if (provaJogador == null)
                provaJogador = FindSceneObject("Prova Jogador");
            if (redcube == null)
                redcube = FindSceneObject("redcube");
        }

        CreateBlinkOverlay();
    }

    private GameObject FindSceneObject(string objectName)
    {
        foreach (Transform candidate in FindObjectsByType<Transform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == gameObject.scene
                && string.Equals(
                    candidate.name,
                    objectName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private Transform CreateCameraTurnPivot()
    {
        Transform cameraParent = transform.parent;
        if (cameraParent == null)
            return transform;

        Vector3 cameraWorldPosition = transform.position;
        Quaternion cameraWorldRotation = transform.rotation;
        GameObject pivotObject = new GameObject("Sleep Camera Rotation Pivot");
        Transform pivot = pivotObject.transform;
        pivot.SetParent(cameraParent, true);
        pivot.SetPositionAndRotation(cameraWorldPosition, cameraWorldRotation);
        transform.SetParent(pivot, true);
        return pivot;
    }

    private void OnEnable()
    {
        if (entryBlinkOnly)
            SceneTransitionManager.FadeInCompleted += StartEntryBlink;
        else
            SceneTransitionManager.FadeInCompleted += StartInitialTurn;

        if (blinkCanvasObject != null)
            blinkCanvasObject.SetActive(true);
    }

    private void OnDisable()
    {
        SceneTransitionManager.FadeInCompleted -= StartInitialTurn;
        SceneTransitionManager.FadeInCompleted -= StartEntryBlink;
        if (!transitioningToScene && blinkCanvasObject != null)
            blinkCanvasObject.SetActive(false);
    }

    private void OnDestroy()
    {
        SceneTransitionManager.FadeInCompleted -= StartInitialTurn;
        SceneTransitionManager.FadeInCompleted -= StartEntryBlink;
        if (!transitioningToScene && blinkCanvasObject != null)
            Destroy(blinkCanvasObject);
    }

    private void StartEntryBlink()
    {
        if (!entryBlinkStarted)
        {
            entryBlinkStarted = true;
            intervalElapsed = blinkInterval;
        }
    }

    private void StartInitialTurn()
    {
        if (initialTurnStarted)
            return;

        initialTurnStarted = true;
        ShowSkipButton();
        if (initialTurnTarget == null)
            return;

        initialTurnStartRotation = initialTurnTarget.localRotation;
        firstTurnEndRotation = Quaternion.Euler(firstTurnEuler)
            * initialTurnStartRotation;
        secondTurnEndRotation = Quaternion.Euler(secondTurnEuler)
            * firstTurnEndRotation;
        rotationSequenceStep = 1;
        StartRotationAnimation(firstTurnEndRotation, firstTurnDuration);
    }

    private void StartRotationAnimation(Quaternion targetRotation, float duration)
    {
        if (initialTurnTarget == null)
            return;

        rotationAnimationStart = initialTurnTarget.localRotation;
        rotationAnimationTarget = targetRotation;
        initialTurnElapsed = 0f;
        rotationAnimationDuration = Mathf.Max(0.1f, duration);
        rotationAnimationActive = true;
    }

    private void LateUpdate()
    {
        if (!rotationAnimationActive || initialTurnTarget == null)
            return;

        initialTurnElapsed = Mathf.Min(
            initialTurnElapsed + Time.unscaledDeltaTime,
            rotationAnimationDuration);
        float progress = initialTurnElapsed / rotationAnimationDuration;
        float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
        initialTurnTarget.localRotation = Quaternion.Slerp(
            rotationAnimationStart,
            rotationAnimationTarget,
            smoothProgress);

        if (progress >= 1f && rotationSequenceStep == 1)
        {
            rotationSequenceStep = 2;
            StartRotationAnimation(secondTurnEndRotation, secondTurnDuration);
        }
        else if (progress >= 1f)
        {
            rotationAnimationActive = false;
            rotationSequenceStep = 0;
        }
    }

    private void Update()
    {
        if (upperLid == null || lowerLid == null)
            return;

        if (skipButtonGroup != null && skipButtonGroup.interactable && !MobileTouchControls.ShouldShowTouchControls())
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                SkipToPhaseOne();
            }
        }

        if (entryBlinkOnly && (!entryBlinkStarted || entryBlinkFinished))
            return;

        float deltaTime = Time.unscaledDeltaTime;
        if (!isBlinking)
        {
            intervalElapsed += deltaTime;
            if (intervalElapsed < blinkInterval)
                return;

            intervalElapsed = 0f;
            isBlinking = true;
            isClosing = true;
            isHoldingClosed = false;
        }

        float currentBlinkSpeed = blinkCount == 3 ? finalBlinkSpeed : blinkSpeed;
        if (isClosing)
        {
            blinkProgress = Mathf.MoveTowards(
                blinkProgress,
                1f,
                Mathf.Max(0.1f, currentBlinkSpeed) * deltaTime);
            UpdateLids();

            if (blinkProgress >= 1f)
            {
                isClosing = false;
                isHoldingClosed = blinkHoldDuration > 0f;
                blinkHoldElapsed = 0f;
                OnBlinkClosed();
            }
        }
        else if (isHoldingClosed)
        {
            blinkHoldElapsed += deltaTime;
            if (blinkHoldElapsed >= blinkHoldDuration)
                isHoldingClosed = false;
        }
        else
        {
            float openingSpeed = Mathf.Max(0.1f, currentBlinkSpeed * blinkOpeningSpeedMultiplier);
            blinkProgress = Mathf.MoveTowards(blinkProgress, 0f, openingSpeed * deltaTime);
            UpdateLids();

            if (blinkProgress <= 0f)
            {
                isBlinking = false;
                if (entryBlinkOnly)
                    entryBlinkFinished = true;

                if (returnRotationPending)
                {
                    returnRotationPending = false;
                    StartRotationAnimation(initialTurnStartRotation, secondTurnDuration);
                }
            }
        }
    }

    private void CreateBlinkOverlay()
    {
        blinkCanvasObject = new GameObject(
            "Sleep Blink Overlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = blinkCanvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = blinkCanvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        upperLid = CreateLid("Upper Eyelid");
        lowerLid = CreateLid("Lower Eyelid");
        eyeTransition = blinkCanvasObject.AddComponent<SleepEyeTransition>();
        eyeTransition.Initialize(upperLid, lowerLid);
        EnsureSkipButtonEventSystem();
        CreateSkipButton();
        UpdateLids();
    }

    private void EnsureSkipButtonEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (eventSystem == null)
            eventSystem = new GameObject("Sleep Skip EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

        inputModule.AssignDefaultActions();
    }

    private void CreateSkipButton()
    {
        GameObject buttonObject = new GameObject(
            "Skip To Phase 1 Button",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(CanvasGroup));
        buttonObject.transform.SetParent(blinkCanvasObject.transform, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = Vector2.one;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.pivot = Vector2.one;
        buttonRect.sizeDelta = new Vector2(265f, 130f);
        buttonRect.anchoredPosition = new Vector2(-115f, -25f);

        Image background = buttonObject.GetComponent<Image>();
        Sprite noteIcon = Resources.Load<Sprite>("HudImages/IconesPapel/NoteIcon");
        if (noteIcon != null)
        {
            background.sprite = noteIcon;
            background.preserveAspect = true;
        }
        background.color = Color.white;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = background;
        button.onClick.AddListener(SkipToPhaseOne);

        skipButtonGroup = buttonObject.GetComponent<CanvasGroup>();
        skipButtonGroup.alpha = 0f;
        skipButtonGroup.interactable = false;
        skipButtonGroup.blocksRaycasts = false;

        GameObject textObject = new GameObject(
            "Skip Button Text",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(55f, 23f);
        textRect.offsetMax = new Vector2(-15f, -22f);

        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset schoolbellFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Schoolbell-Regular SDF");
        if (schoolbellFont != null)
            label.font = schoolbellFont;
        label.text = MobileTouchControls.ShouldShowTouchControls()
            ? "Clique para pular"
            : "Clique ou pressione Enter para pular";
        label.fontSize = 19f;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 15f;
        label.fontSizeMax = 19f;
        label.enableWordWrapping = true;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.02f, 0.11f, 0.13f, 1f);
        label.raycastTarget = false;
    }

    private void ShowSkipButton()
    {
        if (skipButtonGroup == null)
            return;

        skipButtonGroup.alpha = 1f;
        skipButtonGroup.interactable = true;
        skipButtonGroup.blocksRaycasts = true;
    }

    private void HideSkipButton()
    {
        if (skipButtonGroup == null)
            return;

        skipButtonGroup.alpha = 0f;
        skipButtonGroup.interactable = false;
        skipButtonGroup.blocksRaycasts = false;
    }

    private void SkipToPhaseOne()
    {
        if (skipTransitionStarted)
            return;

        skipTransitionStarted = true;
        transitioningToScene = true;
        Time.timeScale = 1f;
        HideSkipButton();
        blinkCanvasObject.SetActive(false);

        if (!SceneTransitionManager.TryTransitionToScene("Fase 1", this))
        {
            skipTransitionStarted = false;
            transitioningToScene = false;
            blinkCanvasObject.SetActive(true);
            ShowSkipButton();
        }
    }

    private RectTransform CreateLid(string lidName)
    {
        GameObject lidObject = new GameObject(
            lidName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        lidObject.transform.SetParent(blinkCanvasObject.transform, false);

        Image image = lidObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return lidObject.GetComponent<RectTransform>();
    }

    private void UpdateLids()
    {
        if (eyeTransition != null)
            eyeTransition.SetClosedAmount(blinkProgress);
    }

    private void OnBlinkClosed()
    {
        blinkCount++;

        switch (blinkCount)
        {
            case 1:
                break;

            case 2:
                returnRotationPending = true;

                if (aluno1 != null)
                    aluno1.SetActive(false);
                else
                    Debug.LogWarning("SleepAnimation: atribua o objeto Aluno1 no Inspector.", this);

                if (numero7 != null)
                    numero7.SetActive(true);
                else
                    Debug.LogWarning("SleepAnimation: atribua o objeto numero7 no Inspector.", this);
                break;

            case 3:
                if (provaJogador != null)
                    provaJogador.SetActive(false);
                else
                    Debug.LogWarning("SleepAnimation: objeto Prova Jogador não encontrado na MainScene.", this);

                if (redcube != null)
                    redcube.SetActive(true);
                else
                    Debug.LogWarning("SleepAnimation: objeto redcube não encontrado na MainScene.", this);
                break;

            case 4:
                if (string.IsNullOrWhiteSpace(sceneToLoad))
                    Debug.LogError("SleepAnimation: defina a cena a carregar no Inspector.", this);
                else
                {
                    transitioningToScene = true;
                    HideSkipButton();
                    if (!SceneTransitionManager.TryTransitionToScene(sceneToLoad, this))
                    {
                        transitioningToScene = false;
                        ShowSkipButton();
                    }
                }
                break;
        }
    }
}