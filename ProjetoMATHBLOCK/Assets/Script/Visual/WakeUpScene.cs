using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class WakeUpScene : MonoBehaviour
{
    private enum BlinkPhase
    {
        Waiting,
        Closing,
        Holding,
        Opening,
        Finished
    }

    [Header("Abertura inicial dos olhos")]
    [SerializeField, Min(0f), Tooltip("Tempo até abrir os olhos, em segundos.")]
    private float initialOpeningDelay = 2f;
    [SerializeField, Min(0.1f), Tooltip("Velocidade da abertura inicial dos olhos.")]
    private float initialOpeningSpeed = 3.5f;

    [Header("Primeira piscada")]
    [SerializeField, Min(0f), Tooltip("Intervalo após abrir os olhos, em segundos.")]
    private float firstBlinkInterval = 2f;
    [SerializeField, Min(0.1f), Tooltip("Velocidade de fechamento e abertura da primeira piscada.")]
    private float firstBlinkSpeed = 3.5f;

    [Header("Segunda piscada")]
    [SerializeField, Min(0f), Tooltip("Intervalo após a primeira piscada, em segundos.")]
    private float secondBlinkInterval = 2f;
    [SerializeField, Min(0.1f), Tooltip("Velocidade de fechamento e abertura da segunda piscada.")]
    private float secondBlinkSpeed = 3.5f;

    [Header("Primeira rotação")]
    [SerializeField, Tooltip("Objeto que será rotacionado ao iniciar.")]
    private Transform objectToRotate;
    [SerializeField, FormerlySerializedAs("rotationEuler"),
     FormerlySerializedAs("finalLocalRotationEuler"),
     Tooltip("Rotação local final da primeira etapa em X, Y e Z.")]
    private Vector3 firstRotationEuler;
    [SerializeField, FormerlySerializedAs("rotationSpeed"), Min(0.1f),
     Tooltip("Velocidade da primeira etapa em graus por segundo.")]
    private float firstRotationSpeed = 90f;

    [Header("Segunda rotação")]
    [SerializeField, Tooltip("Rotação local final da segunda etapa em X, Y e Z.")]
    private Vector3 secondRotationEuler;
    [SerializeField, Min(0.1f), Tooltip("Velocidade da segunda etapa em graus por segundo.")]
    private float secondRotationSpeed = 90f;

    [Header("Terceira rotação")]
    [SerializeField, Tooltip("Rotação local final da terceira etapa em X, Y e Z.")]
    private Vector3 thirdRotationEuler;
    [SerializeField, Min(0.1f), Tooltip("Velocidade da terceira etapa em graus por segundo.")]
    private float thirdRotationSpeed = 90f;
    [SerializeField, Tooltip("Objeto Stamp a ativar ao terminar a terceira rotação. Se vazio, procura um objeto chamado Stamp na cena.")]
    private GameObject stampObject;

    [Header("Retorno ao menu")]
    [SerializeField, Min(0f), Tooltip("Tempo após a última rotação para carregar a cena MainMenu, em segundos.")]
    private float mainMenuDelay = 3f;

    private const int ActionCount = 3;
    private const float BlinkHoldDuration = 0.08f;

    private GameObject blinkCanvasObject;
    private SleepEyeTransition eyeTransition;
    private Quaternion rotationTarget;
    private Quaternion rotationStart;
    private float rotationElapsed;
    private float rotationDuration;
    private int rotationStep;
    private BlinkPhase blinkPhase = BlinkPhase.Waiting;
    private float intervalElapsed;
    private float blinkProgress;
    private float holdElapsed;
    private int currentBlinkIndex;
    private int completedBlinks;

    private void Start()
    {
        blinkProgress = 1f;
        if (stampObject == null)
            stampObject = FindStampObject();

        if (objectToRotate != null)
            BeginRotationStep();
        CreateBlinkOverlay();
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        UpdateObjectRotation(deltaTime);

        switch (blinkPhase)
        {
            case BlinkPhase.Waiting:
                if (completedBlinks >= ActionCount)
                {
                    blinkPhase = BlinkPhase.Finished;
                    Destroy(blinkCanvasObject);
                    blinkCanvasObject = null;
                    return;
                }

                intervalElapsed += deltaTime;
                if (intervalElapsed >= GetIntervalForNextBlink())
                {
                    intervalElapsed = 0f;
                    currentBlinkIndex = completedBlinks;
                    blinkPhase = currentBlinkIndex == 0
                        ? BlinkPhase.Opening
                        : BlinkPhase.Closing;
                }
                break;

            case BlinkPhase.Closing:
                blinkProgress = Mathf.MoveTowards(
                    blinkProgress,
                    1f,
                    GetCurrentBlinkSpeed() * deltaTime);
                UpdateLids();

                if (blinkProgress >= 1f)
                {
                    holdElapsed = 0f;
                    blinkPhase = BlinkPhase.Holding;
                }
                break;

            case BlinkPhase.Holding:
                holdElapsed += deltaTime;
                if (holdElapsed >= BlinkHoldDuration)
                    blinkPhase = BlinkPhase.Opening;
                break;

            case BlinkPhase.Opening:
                blinkProgress = Mathf.MoveTowards(
                    blinkProgress,
                    0f,
                    GetCurrentBlinkSpeed() * deltaTime);
                UpdateLids();

                if (blinkProgress <= 0f)
                {
                    completedBlinks++;
                    blinkPhase = BlinkPhase.Waiting;
                }
                break;
        }
    }

    private void UpdateObjectRotation(float deltaTime)
    {
        if (objectToRotate == null || rotationStep >= 3)
            return;

        float remainingDeltaTime = deltaTime;
        while (rotationStep < 3)
        {
            float stepDeltaTime = Mathf.Min(
                remainingDeltaTime,
                rotationDuration - rotationElapsed);
            rotationElapsed += stepDeltaTime;
            remainingDeltaTime -= stepDeltaTime;

            float progress = rotationDuration > 0f
                ? Mathf.Clamp01(rotationElapsed / rotationDuration)
                : 1f;
            float easedProgress = progress * progress * progress
                * (progress * (progress * 6f - 15f) + 10f);
            objectToRotate.localRotation = Quaternion.Slerp(
                rotationStart,
                rotationTarget,
                easedProgress);

            if (progress < 1f)
                break;

            objectToRotate.localRotation = rotationTarget;
            AdvanceRotationStep();
            if (remainingDeltaTime <= 0f && rotationStep < 3)
            {
                break;
            }
        }
    }

    private void AdvanceRotationStep()
    {
        rotationStep++;
        if (rotationStep < 3)
            BeginRotationStep();
        else if (stampObject != null)
            stampObject.SetActive(true);
        else
            Debug.LogError("WakeUpScene: objeto Stamp não encontrado. Atribua-o no Inspector ou nomeie-o como Stamp.", this);

        StartCoroutine(ReturnToMainMenuAfterDelay());
    }

    private IEnumerator ReturnToMainMenuAfterDelay()
    {
        yield return new WaitForSecondsRealtime(mainMenuDelay);
        SceneTransitionManager.TryTransitionToScene("MainMenu", this);
    }

    private GameObject FindStampObject()
    {
        foreach (Transform candidate in FindObjectsByType<Transform>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == gameObject.scene
                && candidate.name == "Stamp")
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private void BeginRotationStep()
    {
        rotationStart = objectToRotate.localRotation;
        rotationTarget = GetRotationTarget(rotationStep);
        rotationElapsed = 0f;
        float angle = Quaternion.Angle(rotationStart, rotationTarget);
        rotationDuration = angle / GetRotationSpeed(rotationStep);
    }

    private Quaternion GetRotationTarget(int step)
    {
        switch (step)
        {
            case 0:
                return Quaternion.Euler(firstRotationEuler);
            case 1:
                return Quaternion.Euler(secondRotationEuler);
            default:
                return Quaternion.Euler(thirdRotationEuler);
        }
    }

    private float GetRotationSpeed(int step)
    {
        switch (step)
        {
            case 0:
                return Mathf.Max(0.1f, firstRotationSpeed);
            case 1:
                return Mathf.Max(0.1f, secondRotationSpeed);
            default:
                return Mathf.Max(0.1f, thirdRotationSpeed);
        }
    }

    private float GetIntervalForNextBlink()
    {
        switch (completedBlinks)
        {
            case 0:
                return initialOpeningDelay;
            case 1:
                return firstBlinkInterval;
            default:
                return secondBlinkInterval;
        }
    }

    private float GetCurrentBlinkSpeed()
    {
        switch (currentBlinkIndex)
        {
            case 0:
                return Mathf.Max(0.1f, initialOpeningSpeed);
            case 1:
                return Mathf.Max(0.1f, firstBlinkSpeed);
            default:
                return Mathf.Max(0.1f, secondBlinkSpeed);
        }
    }

    private void CreateBlinkOverlay()
    {
        blinkCanvasObject = new GameObject(
            "Wake Up Blink Overlay",
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

        RectTransform upperLid = CreateLid("Upper Eyelid");
        RectTransform lowerLid = CreateLid("Lower Eyelid");
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

    private void OnDestroy()
    {
        if (blinkCanvasObject != null)
            Destroy(blinkCanvasObject);
    }
}
