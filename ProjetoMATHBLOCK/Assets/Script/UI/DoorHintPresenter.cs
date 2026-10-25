using System.Collections;
using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Apresenta operadores na Fase 1 e redimensionamento na Fase 2, com a mesma identidade visual.</summary>
public sealed class DoorHintPresenter : MonoBehaviour
{
    private const float InitialDelay = 5f;
    private const float SlideDuration = 0.58f;
    private const float ReadingDuration = 3.5f;
    private static DoorHintPresenter instance;

    // Papel claro com a identidade teal preservada na borda e no glow.
    private static readonly Color FillTop = new Color(1f, 1f, 0.985f, 0.98f);
    private bool IsResizeHint => gameObject.scene.name == "Fase 2";
    private Color FillBottom => IsResizeHint ? new Color(0.90f, 0.94f, 1f, 0.98f) : new Color(0.90f, 0.97f, 0.955f, 0.98f);
    private Color BorderColor => IsResizeHint ? RotulosConteudo.Cor(ConteudoMatematico.EscalaEEspaco) : new Color(0.16f, 1f, 0.78f, 1f);
    private Color GlowColor => IsResizeHint ? new Color(0.20f, 0.55f, 1f, 0.28f) : new Color(0.05f, 1f, 0.78f, 0.28f);
    private Color TitleColor => IsResizeHint ? new Color(0.055f, 0.22f, 0.48f, 1f) : new Color(0.025f, 0.34f, 0.29f, 1f);
    private static readonly Color BodyColor = new Color(0.055f, 0.11f, 0.13f, 0.94f);

    private RectTransform balloon;
    private CanvasGroup canvasGroup;
    private Coroutine animationRoutine;
    private float hiddenY;
    private const float VisibleY = -18f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneInitializer()
    {
        GlobalSceneBootstrap.Register(InstallAtGameStart);
    }

    private static void InstallAtGameStart(Scene scene)
    {
        if (scene.name != "Fase 1" && scene.name != "Fase 2")
            return;

        DoorHintPresenter presenter = EnsureInstance();
        presenter.StartCoroutine(presenter.ShowAfterInitialDelay());
    }

    public static void NotifyDoorPassed()
    {
        // Mantido para não quebrar chamadas antigas das portas/spawners.
    }

    public static void MostrarAvisoAtalho()
    {
        DoorHintPresenter presenter = EnsureInstance();
        presenter.StartCoroutine(presenter.AnimarAvisoAtalho());
    }

    private IEnumerator AnimarAvisoAtalho()
    {
        // Reutiliza papel, borda e fonte da dica. Fica embaixo para nao
        // disputar espaco com a introducao e nao bloqueia os controles.
        var aviso = new GameObject("Aviso de tentativa de atalho", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        aviso.transform.SetParent(transform, false);
        var rect = aviso.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0f);
        rect.sizeDelta = new Vector2(680f, 150f);
        Image fundoAviso = aviso.GetComponent<Image>();
        fundoAviso.sprite = CreateBalloonSprite();
        fundoAviso.type = Image.Type.Sliced;
        fundoAviso.raycastTarget = false;
        var grupoAviso = aviso.GetComponent<CanvasGroup>();
        grupoAviso.blocksRaycasts = grupoAviso.interactable = false;
        AddText(aviso.transform, "BOA TENTATIVA!", 28f, FontStyles.Bold, TitleColor,
            new Vector2(36f, -57f), new Vector2(-36f, -20f), TextAlignmentOptions.Center);
        AddText(aviso.transform, "A matemática não pula etapas.\nVolta lá e resolve a conta!", 23f, FontStyles.Normal, BodyColor,
            new Vector2(36f, -125f), new Vector2(-36f, -59f), TextAlignmentOptions.Center);
        const float entrada = .3f, leitura = 4.5f, saida = .4f;
        for (float t = 0f; t < entrada + leitura + saida; t += Time.unscaledDeltaTime)
        {
            float visivel = t < entrada ? Mathf.SmoothStep(0f, 1f, t / entrada)
                : 1f - Mathf.SmoothStep(0f, 1f, (t - entrada - leitura) / saida);
            grupoAviso.alpha = visivel;
            rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(5f, 25f, visivel));
            yield return null;
        }
        Destroy(aviso);
    }

    private static DoorHintPresenter EnsureInstance()
    {
        if (instance != null)
            return instance;

        GameObject root = new GameObject("Door Tutorial Hints");
        instance = root.AddComponent<DoorHintPresenter>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        BuildInterface();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private IEnumerator ShowAfterInitialDelay()
    {
        yield return new WaitForSecondsRealtime(InitialDelay);
        ShowOperatorHint();
    }

    private void ShowOperatorHint()
    {
        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        Vector3 soundPosition = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        RuntimeManager.PlayOneShot("event:/soma", soundPosition);
        animationRoutine = StartCoroutine(AnimateBalloon());
    }

    private void BuildInterface()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1800;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject(IsResizeHint ? "Resize Hint Balloon" : "Operator Hint Balloon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        panelObject.transform.SetParent(transform, false);
        balloon = panelObject.GetComponent<RectTransform>();
        balloon.anchorMin = new Vector2(0.5f, 1f);
        balloon.anchorMax = new Vector2(0.5f, 1f);
        balloon.pivot = new Vector2(0.5f, 1f);
        balloon.sizeDelta = new Vector2(780f, 268f);
        hiddenY = balloon.sizeDelta.y + 35f;
        balloon.anchoredPosition = new Vector2(0f, hiddenY);

        Image background = panelObject.GetComponent<Image>();
        background.sprite = CreateBalloonSprite();
        background.type = Image.Type.Sliced;
        background.color = Color.white;
        background.raycastTarget = false;

        canvasGroup = panelObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        AddBackdropLayer(panelObject.transform, "Outer Glow", new Vector2(24f, 24f), GlowColor, 0);
        AddBackdropLayer(panelObject.transform, "Bright Border", new Vector2(10f, 10f),
            IsResizeHint ? new Color(0.20f, 0.55f, 1f, 0.52f) : new Color(0.08f, 0.85f, 0.68f, 0.52f), 1);

        GameObject accentObject = new GameObject("Accent Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        accentObject.transform.SetParent(panelObject.transform, false);
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.anchorMin = accentRect.anchorMax = new Vector2(0f, 0.5f);
        accentRect.pivot = new Vector2(0.5f, 0.5f);
        accentRect.anchoredPosition = new Vector2(326f, 0f);
        accentRect.sizeDelta = new Vector2(3f, 186f);
        Image accent = accentObject.GetComponent<Image>();
        accent.color = new Color(BorderColor.r, BorderColor.g, BorderColor.b, 0.55f);
        accent.raycastTarget = false;

        AddText(panelObject.transform, IsResizeHint ? "DICA  •  ESCALA E ESPAÇO" : "DICA  •  OPERADORES", IsResizeHint ? 28f : 32f, FontStyles.Bold, TitleColor,
            new Vector2(354f, -76f), new Vector2(-38f, -24f), TextAlignmentOptions.Left);
        AddText(panelObject.transform,
            IsResizeHint
                ? "Mire em um bloco e ative o redimensionamento.\n\nArraste as setas azuis para ajustar largura, altura e profundidade."
                : "Pegue um operador e aplique-o entre dois MathBlocks.\n\nCada símbolo transforma os valores de uma maneira diferente.",
            21f, FontStyles.Normal, BodyColor, new Vector2(354f, -226f), new Vector2(-42f, -88f), TextAlignmentOptions.Left);

        GameObject imageObject = new GameObject(IsResizeHint ? "Resize Illustration" : "Operator Illustration", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.transform.SetParent(panelObject.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 1f);
        imageRect.anchorMax = new Vector2(0.5f, 1f);
        imageRect.pivot = new Vector2(0.5f, 1f);
        imageRect.anchoredPosition = new Vector2(-218f, IsResizeHint ? -55f : -82f);
        // Mantem a captura enviada inteira e na proporcao original, sem esticar o bloco.
        imageRect.sizeDelta = IsResizeHint ? new Vector2(280f, 280f * 768f / 1366f) : new Vector2(270f, 96f);
        RawImage illustration = imageObject.GetComponent<RawImage>();
        illustration.texture = Resources.Load<Texture2D>(IsResizeHint ? "Tutorial/resize_hint" : "Tutorial/operator_hint");
        illustration.uvRect = new Rect(0f, 0f, 1f, 1f);
        illustration.raycastTarget = false;
    }

    private void AddBackdropLayer(Transform panel, string layerName, Vector2 expansion, Color color, int siblingIndex)
    {
        GameObject layerObject = new GameObject(layerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        layerObject.transform.SetParent(panel, false);
        layerObject.transform.SetSiblingIndex(siblingIndex);

        RectTransform rect = layerObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = -expansion;
        rect.offsetMax = expansion;

        Image image = layerObject.GetComponent<Image>();
        image.sprite = CreateBalloonSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
    }

    private static void AddText(Transform parent, string content, float size, FontStyles style, Color color,
        Vector2 offsetMin, Vector2 offsetMax, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject("Hint Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        TMP_FontAsset styledFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Schoolbell-Regular SDF");
        if (styledFont != null)
            text.font = styledFont;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.characterSpacing = style == FontStyles.Bold ? 1.5f : 0.25f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }

    private IEnumerator AnimateBalloon()
    {
        yield return Slide(hiddenY, VisibleY, 0f, 0.98f, SlideDuration);
        yield return new WaitForSecondsRealtime(IsResizeHint ? 8f : ReadingDuration);
        yield return Slide(VisibleY, hiddenY, 0.98f, 0f, SlideDuration * 0.9f);
        canvasGroup.alpha = 0f;
        animationRoutine = null;
    }

    private IEnumerator Slide(float from, float to, float alphaFrom, float alphaTo, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            balloon.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(from, to, t));
            canvasGroup.alpha = Mathf.Lerp(alphaFrom, alphaTo, t);
            yield return null;
        }
        balloon.anchoredPosition = new Vector2(0f, to);
        canvasGroup.alpha = alphaTo;
    }

    /// <summary>
    /// Distância de um ponto até um retângulo arredondado centrado na origem
    /// (negativa = dentro, positiva = fora). Base para todo o anti-aliasing
    /// do balão: bordas, contorno e sombra usam a mesma função.
    /// </summary>
    private static float RoundedBoxSDF(Vector2 point, Vector2 halfSize, float cornerRadius)
    {
        Vector2 q = new Vector2(
            Mathf.Abs(point.x) - halfSize.x + cornerRadius,
            Mathf.Abs(point.y) - halfSize.y + cornerRadius);

        float outsideDistance = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
        float insideDistance = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
        return outsideDistance + insideDistance - cornerRadius;
    }

    private Sprite CreateBalloonSprite()
    {
        const int size = 200;
        const float shadowBlur = 18f;
        const float shadowOpacity = 0.42f;
        const float cardMargin = shadowBlur + 4f;
        const float cornerRadius = 34f;
        const float borderThickness = 6f;
        const float antiAliasWidth = 1.25f;

        Vector2 halfSize = new Vector2(size / 2f - cardMargin, size / 2f - cardMargin);
        Vector2 center = new Vector2(size / 2f, size / 2f);

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Tutorial Balloon",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            // Gradiente vertical sutil no preenchimento — dá profundidade sem
            // parecer plano nem chamativo demais.
            float verticalT = y / (float)(size - 1);
            Color rowFillColor = Color.Lerp(FillBottom, FillTop, verticalT);

            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                float dist = RoundedBoxSDF(p, halfSize, cornerRadius);

                float cardAlpha = Mathf.Clamp01(1f - Mathf.InverseLerp(-antiAliasWidth, antiAliasWidth, dist));
                float innerAlpha = Mathf.Clamp01(1f - Mathf.InverseLerp(-antiAliasWidth, antiAliasWidth, dist + borderThickness));
                float shadowAlpha = Mathf.Clamp01(1f - Mathf.InverseLerp(-shadowBlur, shadowBlur, dist)) * shadowOpacity;

                Color cardColor = Color.Lerp(BorderColor, rowFillColor, innerAlpha);

                float finalAlpha = cardAlpha + shadowAlpha * (1f - cardAlpha);
                Color finalColor = Color.clear;

                if (finalAlpha > 0.0001f)
                {
                    Color blended = (cardColor * cardAlpha) + (Color.black * (shadowAlpha * (1f - cardAlpha)));
                    finalColor = blended / finalAlpha;
                }

                finalColor.a = finalAlpha;
                pixels[y * size + x] = finalColor;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        float sliceBorder = cardMargin + cornerRadius + 6f;
        return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100f, 0,
            SpriteMeshType.FullRect, Vector4.one * sliceBorder);
    }
}
