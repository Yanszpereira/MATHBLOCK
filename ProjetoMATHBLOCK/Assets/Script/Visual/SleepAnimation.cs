using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SleepAnimation : MonoBehaviour
{
    [SerializeField, Min(0f)] private float blinkInterval = 5f;
    [SerializeField, Min(0.1f)] private float blinkSpeed = 3.5f;
    [SerializeField, Min(0f)] private float blinkHoldDuration = 0.08f;
    [SerializeField, Range(0.1f, 1f)] private float blinkOpeningSpeedMultiplier = 0.7f;
    [SerializeField, Min(0.1f)] private float finalBlinkSpeed = 0.7f;
    [SerializeField] private float initialTurnAngle = 85f;
    [SerializeField, Min(0.1f)] private float initialTurnDuration = 5f;
    [SerializeField] private GameObject aluno1;
    [SerializeField] private GameObject numero7;
    [SerializeField] private SkylandAnimation skylandAnimation;
    [SerializeField] private string sceneToLoad;

    private RectTransform upperLid;
    private RectTransform lowerLid;
    private GameObject blinkCanvasObject;
    private SleepEyeTransition eyeTransition;
    private Transform initialTurnTarget;
    private Quaternion initialTurnStartRotation;
    private Quaternion initialTurnEndRotation;
    private Quaternion rotationAnimationStart;
    private Quaternion rotationAnimationTarget;
    private float intervalElapsed;
    private float initialTurnElapsed;
    private float blinkHoldElapsed;
    private float blinkProgress;
    private int blinkCount;
    private bool isBlinking;
    private bool isClosing = true;
    private bool isHoldingClosed;
    private bool transitioningToScene;
    private bool initialTurnStarted;
    private bool rotationAnimationActive;

    private void Awake()
    {
        if (gameObject.scene.name != "MainScene")
        {
            enabled = false;
            return;
        }

        initialTurnTarget = transform.parent;
        CreateBlinkOverlay();
    }

    private void OnEnable()
    {
        SceneTransitionManager.FadeInCompleted += StartInitialTurn;
        if (blinkCanvasObject != null)
            blinkCanvasObject.SetActive(true);
    }

    private void OnDisable()
    {
        SceneTransitionManager.FadeInCompleted -= StartInitialTurn;
        if (!transitioningToScene && blinkCanvasObject != null)
            blinkCanvasObject.SetActive(false);
    }

    private void OnDestroy()
    {
        SceneTransitionManager.FadeInCompleted -= StartInitialTurn;
        if (!transitioningToScene && blinkCanvasObject != null)
            Destroy(blinkCanvasObject);
    }

    private void StartInitialTurn()
    {
        if (initialTurnStarted || initialTurnTarget == null)
            return;

        initialTurnStartRotation = initialTurnTarget.localRotation;
        initialTurnEndRotation = initialTurnStartRotation
            * Quaternion.AngleAxis(initialTurnAngle, Vector3.up);
        initialTurnStarted = true;
        StartRotationAnimation(initialTurnEndRotation);
    }

    private void StartRotationAnimation(Quaternion targetRotation)
    {
        if (initialTurnTarget == null)
            return;

        rotationAnimationStart = initialTurnTarget.localRotation;
        rotationAnimationTarget = targetRotation;
        initialTurnElapsed = 0f;
        rotationAnimationActive = true;
    }

    private void LateUpdate()
    {
        if (!rotationAnimationActive || initialTurnTarget == null)
            return;

        float duration = Mathf.Max(0.1f, initialTurnDuration);
        initialTurnElapsed = Mathf.Min(
            initialTurnElapsed + Time.unscaledDeltaTime,
            duration);
        float progress = initialTurnElapsed / duration;
        float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
        initialTurnTarget.localRotation = Quaternion.Slerp(
            rotationAnimationStart,
            rotationAnimationTarget,
            smoothProgress);

        if (progress >= 1f)
            rotationAnimationActive = false;
    }

    private void Update()
    {
        if (upperLid == null || lowerLid == null)
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
                isBlinking = false;
        }
    }

    private void CreateBlinkOverlay()
    {
        blinkCanvasObject = new GameObject(
            "Sleep Blink Overlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

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
        UpdateLids();
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
                StartRotationAnimation(initialTurnStartRotation);

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
                if (skylandAnimation != null)
                    skylandAnimation.enabled = true;
                else
                    Debug.LogWarning("SleepAnimation: atribua o componente SkylandAnimation no Inspector.", this);
                break;

            case 4:
                if (string.IsNullOrWhiteSpace(sceneToLoad))
                    Debug.LogError("SleepAnimation: defina a cena a carregar no Inspector.", this);
                else
                {
                    transitioningToScene = true;
                    eyeTransition.OpenEyesAfterNextScene(finalBlinkSpeed);
                    SceneManager.LoadScene(sceneToLoad);
                }
                break;
        }
    }
}