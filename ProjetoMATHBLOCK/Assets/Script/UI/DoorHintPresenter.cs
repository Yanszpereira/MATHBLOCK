using System.Collections;
using FMODUnity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Apresenta dicas contextuais quando o jogador atravessa portas.</summary>
public sealed class DoorHintPresenter : MonoBehaviour
{
    private const float SlideDuration = 0.58f;
    private const float ReadingDuration = 3.5f;
    private static DoorHintPresenter instance;
    private static int passedDoorCount;

    // Paleta do balão — tons quentes de "pergaminho" com identidade teal do jogo.
    private static readonly Color FillTop = new Color(1.00f, 0.97f, 0.89f, 1f);
    private static readonly Color FillBottom = new Color(0.99f, 0.92f, 0.78f, 1f);
    private static readonly Color BorderColor = new Color(0.07f, 0.30f, 0.34f, 1f);
    private static readonly Color TitleColor = new Color(0.05f, 0.20f, 0.23f, 1f);
    private static readonly Color BodyColor = new Color(0.28f, 0.22f, 0.14f, 1f);

    private RectTransform balloon;
    private CanvasGroup canvasGroup;
    private Coroutine animationRoutine;
    private float hiddenY;
    private const float VisibleY = -18f;

    public static void NotifyDoorPassed()
    {
        passedDoorCount++;
        if (passedDoorCount != 1)
            return;

        EnsureInstance().ShowOperatorHint();
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

        GameObject panelObject = new GameObject("Operator Hint Balloon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        panelObject.transform.SetParent(transform, false);
        balloon = panelObject.GetComponent<RectTransform>();
        balloon.anchorMin = new Vector2(0.5f, 1f);
        balloon.anchorMax = new Vector2(0.5f, 1f);
        balloon.pivot = new Vector2(0.5f, 1f);
        balloon.sizeDelta = new Vector2(700f, 330f);
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

        AddText(panelObject.transform, "DICA: OPERADORES", 32f, FontStyles.Bold, TitleColor,
            new Vector2(38f, -62f), new Vector2(-38f, -20f), TextAlignmentOptions.Center);
        AddText(panelObject.transform,
            "Pegue um operador e use-o entre dois MathBlocks. A soma junta valores; os outros operadores transformam o resultado de maneiras diferentes.",
            21f, FontStyles.Normal, BodyColor, new Vector2(44f, -128f), new Vector2(-44f, -66f), TextAlignmentOptions.Center);

        GameObject imageObject = new GameObject("Operator Illustration", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.transform.SetParent(panelObject.transform, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 1f);
        imageRect.anchorMax = new Vector2(0.5f, 1f);
        imageRect.pivot = new Vector2(0.5f, 1f);
        imageRect.anchoredPosition = new Vector2(0f, -138f);
        imageRect.sizeDelta = new Vector2(540f, 168f);
        RawImage illustration = imageObject.GetComponent<RawImage>();
        illustration.texture = Resources.Load<Texture2D>("Tutorial/operator_hint");
        illustration.uvRect = new Rect(0f, 0f, 1f, 1f);
        illustration.raycastTarget = false;
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
        yield return new WaitForSecondsRealtime(ReadingDuration);
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

    private static Sprite CreateBalloonSprite()
    {
        const int size = 200;
        const float shadowBlur = 18f;
        const float shadowOpacity = 0.30f;
        const float cardMargin = shadowBlur + 4f;
        const float cornerRadius = 34f;
        const float borderThickness = 5f;
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
