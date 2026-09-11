using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CountdownTimer : MonoBehaviour
{
    [Header("Tempo Inicial")]
    public float tempoInicial = 300f;

    [Header("Referência do Texto")]
    public TMP_Text timerText;

    [Header("Transição ao Reiniciar")]
    [SerializeField, Min(0.05f)] private float fadeOutDuration = 0.65f;
    [SerializeField, Min(0.05f)] private float fadeInDuration = 0.45f;
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private string resetSceneName = "Fase 1";

    [Header("Cronômetro físico")]
    [SerializeField] private bool followPlayerLookDirection = true;
    [SerializeField] private Camera playerCamera;
    [Tooltip("A face visivel do texto deste prefab esta a 180 graus do eixo forward.")]
    [SerializeField] private float visibleFaceYaw = 180f;

    private static CanvasGroup activeFadeGroup;
    private static CountdownTimer clockAuthority;

    private float tempoAtual;
    private bool contando = true;
    private bool resetStarted;
    private CanvasGroup fadeGroup;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSharedClock()
    {
        clockAuthority = null;
        activeFadeGroup = null;
    }

    private void OnEnable()
    {
        if (clockAuthority == null)
            clockAuthority = this;
    }

    private void OnDisable()
    {
        if (clockAuthority == this)
            clockAuthority = null;
    }

    private void Start()
    {
        if (clockAuthority != null && clockAuthority != this)
        {
            tempoInicial = clockAuthority.tempoInicial;
            tempoAtual = clockAuthority.tempoAtual;
        }
        else
        {
            clockAuthority = this;
            tempoAtual = Mathf.Max(0f, tempoInicial);
        }
        AtualizarTexto();

    }

    private void LateUpdate()
    {
        if (!followPlayerLookDirection)
            return;

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
                return;
        }

        // Direção do timer até a câmera, projetada no plano horizontal
        // para o objeto girar apenas no eixo Y (sem inclinar/tombar).
        Vector3 directionToCamera = playerCamera.transform.position - transform.position;
        directionToCamera.y = 0f;
        if (directionToCamera.sqrMagnitude < 0.0001f)
            return;

        // Usa a face real do texto em vez de presumir um eixo do FBX. Giramos a
        // referencia em 180 graus, alinhamos horizontalmente e preservamos a
        // inclinacao original do modelo.
        Transform face = timerText != null ? timerText.transform : transform;
        Vector3 visibleFaceDirection = Quaternion.AngleAxis(visibleFaceYaw, Vector3.up) * face.forward;
        visibleFaceDirection.y = 0f;
        if (visibleFaceDirection.sqrMagnitude < 0.0001f)
            return;

        float correction = Vector3.SignedAngle(
            visibleFaceDirection.normalized,
            directionToCamera.normalized,
            Vector3.up);
        transform.Rotate(Vector3.up, correction, Space.World);
    }

    private void Update()
    {
        if (clockAuthority == null)
            clockAuthority = this;

        // Todas as placas fisicas exibem o mesmo relogio. Somente uma delas
        // decrementa o tempo e pode iniciar a troca de cena/fade.
        if (clockAuthority != this)
        {
            tempoInicial = clockAuthority.tempoInicial;
            tempoAtual = clockAuthority.tempoAtual;
            contando = clockAuthority.contando;
            AtualizarTexto();
            return;
        }

        if (!contando || resetStarted)
            return;

        tempoAtual -= Time.deltaTime;
        if (tempoAtual > 0f)
        {
<<<<<<< HEAD
            AtualizarTexto();
            return;
=======
            tempoAtual = 0;
            contando = false;

            Debug.Log("Tempo esgotado!");
            // Aqui voc� pode chamar Game Over
            SceneTransitionManager.TryTransitionToScene("Fase 1", this);
>>>>>>> f6d3b363c6be784869f6b52c7564c8fb55016096
        }

        tempoAtual = 0f;
        contando = false;
        AtualizarTexto();
        StartCoroutine(RestartCurrentSceneWithFade());
    }

    private IEnumerator RestartCurrentSceneWithFade()
    {
        if (resetStarted)
            yield break;

        resetStarted = true;
        Scene activeScene = SceneManager.GetActiveScene();
        Debug.Log($"Tempo esgotado em {activeScene.name}. Reiniciando o jogo em {resetSceneName}.", this);

        EnsureFadeOverlay(0f);
        yield return FadeCanvas(0f, 1f, fadeOutDuration, false);

        if (string.IsNullOrWhiteSpace(resetSceneName) || !Application.CanStreamedLevelBeLoaded(resetSceneName))
        {
            Debug.LogError($"Não foi possível reiniciar: a cena '{resetSceneName}' não está no Build Settings.", this);
            resetStarted = false;
            contando = true;
            yield return FadeCanvas(1f, 0f, fadeInDuration, true);
            yield break;
        }

        TimerResetFadeCompletion.Attach(fadeGroup, fadeInDuration);
        SceneManager.LoadScene(resetSceneName, LoadSceneMode.Single);
    }
<<<<<<< HEAD

    private void EnsureFadeOverlay(float initialAlpha)
    {
        if (fadeGroup != null)
        {
            fadeGroup.alpha = initialAlpha;
            activeFadeGroup = fadeGroup;
            return;
        }

        GameObject canvasObject = new GameObject(
            "Timer Reset Fade",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));

        // Mantém o overlay vivo durante o LoadScene, para não haver um frame
        // sem o preto entre a cena antiga sendo descarregada e a nova iniciando.
        DontDestroyOnLoad(canvasObject);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        fadeGroup = canvasObject.GetComponent<CanvasGroup>();
        fadeGroup.alpha = initialAlpha;
        fadeGroup.interactable = true;
        fadeGroup.blocksRaycasts = true;

        GameObject imageObject = new GameObject("Fade Color", typeof(RectTransform), typeof(Image));
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.SetParent(canvasObject.transform, false);
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = fadeColor;
        image.raycastTarget = true;

        activeFadeGroup = fadeGroup;
    }

    private IEnumerator FadeCanvas(float from, float to, float duration, bool destroyWhenFinished)
    {
        if (fadeGroup == null)
            yield break;

        duration = Mathf.Max(0.05f, duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = normalized * normalized * (3f - 2f * normalized);
            fadeGroup.alpha = Mathf.Lerp(from, to, eased);
            yield return null;
        }

        fadeGroup.alpha = to;
        if (destroyWhenFinished && fadeGroup != null)
        {
            Destroy(fadeGroup.gameObject);
            if (activeFadeGroup == fadeGroup)
                activeFadeGroup = null;
            fadeGroup = null;
        }
    }

    private void AtualizarTexto()
    {
        if (timerText == null)
            return;

        int minutos = Mathf.FloorToInt(tempoAtual / 60f);
        int segundos = Mathf.FloorToInt(tempoAtual % 60f);
        timerText.text = $"{minutos:00}:{segundos:00}";
    }
}

/// <summary>
/// Fica no Canvas persistente e conclui o fade independentemente da ordem de
/// inicializacao dos objetos da cena carregada.
/// </summary>
internal sealed class TimerResetFadeCompletion : MonoBehaviour
{
    private CanvasGroup group;
    private float duration;
    private bool completed;

    public static void Attach(CanvasGroup target, float fadeDuration)
    {
        if (target == null)
            return;

        TimerResetFadeCompletion completion = target.GetComponent<TimerResetFadeCompletion>();
        if (completion == null)
            completion = target.gameObject.AddComponent<TimerResetFadeCompletion>();
        completion.group = target;
        completion.duration = Mathf.Max(0.05f, fadeDuration);
        SceneManager.sceneLoaded -= completion.OnSceneLoaded;
        SceneManager.sceneLoaded += completion.OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (completed)
            return;

        completed = true;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        StartCoroutine(RevealScene());
    }

    private IEnumerator RevealScene()
    {
        // Um frame garante que a camera e a HUD globais já foram inicializadas.
        yield return null;
        float elapsed = 0f;
        while (group != null && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            group.alpha = 1f - t;
            yield return null;
        }

        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            Destroy(group.gameObject);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
=======
>>>>>>> f6d3b363c6be784869f6b52c7564c8fb55016096
}
