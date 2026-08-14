using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class OperatorHudStyler : MonoBehaviour
{
    private static readonly string[] Names = { "Soma", "Subtracao", "Multiplicacao", "Divisao" };
    private static readonly string[] Symbols = { "+", "−", "×", "÷" };
    private static readonly Color[] Accents =
    {
        new Color(0.18f, 1.00f, 0.49f, 1f),
        new Color(1.00f, 0.43f, 0.17f, 1f),
        new Color(0.12f, 0.62f, 1.00f, 1f),
        new Color(1.00f, 0.34f, 0.40f, 1f)
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu" ||
            FindFirstObjectByType<OperatorHudStyler>() != null)
            return;

        new GameObject("Operator HUD Toon Style").AddComponent<OperatorHudStyler>();
    }

    private void Start()
    {
        Image[] images = FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<RectTransform> operatorRects = new List<RectTransform>();

        for (int i = 0; i < Names.Length; i++)
        {
            string expectedName = Names[i];
            Image image = Array.Find(images, candidate =>
                candidate != null && candidate.name.Equals(expectedName, StringComparison.OrdinalIgnoreCase));

            if (image != null)
            {
                ApplyStyle(image, Symbols[i], Accents[i]);
                operatorRects.Add(image.rectTransform);
            }
        }

        AjustarLayoutLateral(operatorRects);
    }

    private static void AjustarLayoutLateral(List<RectTransform> operators)
    {
        if (operators.Count == 0)
            return;

        float centerY = 0f;
        foreach (RectTransform rect in operators)
            centerY += rect.anchoredPosition.y;
        centerY /= operators.Count;

        foreach (RectTransform rect in operators)
        {
            // Reduz os botoes sem deformar os simbolos.
            rect.sizeDelta *= 0.78f;

            // Afasta cada item a partir do centro da coluna, preservando a ordem.
            Vector2 position = rect.anchoredPosition;
            position.y = centerY + (position.y - centerY) * 1.28f;
            rect.anchoredPosition = position;
        }
    }

    private static void ApplyStyle(Image background, string symbol, Color accent)
    {
        background.sprite = CreateToonOctagon(accent);
        background.type = Image.Type.Simple;
        background.preserveAspect = true;
        background.color = new Color(1f, 1f, 1f, background.color.a);

        Image glow = CreateImageChild(background.transform, "Toon Glow");
        Stretch(glow.rectTransform, -7f);
        glow.sprite = CreateGlowOctagon(accent);
        glow.preserveAspect = true;
        glow.raycastTarget = false;
        glow.transform.SetAsFirstSibling();

        Image dots = CreateImageChild(background.transform, "Selected Dither Dots");
        Stretch(dots.rectTransform, 8f);
        dots.sprite = CreateDitherSprite(accent);
        dots.preserveAspect = true;
        dots.raycastTarget = false;

        TextMeshProUGUI text = CreateSymbol(background.transform);
        text.text = symbol;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 52f;
        text.fontStyle = FontStyles.Bold;
        text.color = accent;
        text.raycastTarget = false;
        text.transform.SetAsLastSibling();

        OperatorHudVisual state = background.GetComponent<OperatorHudVisual>();
        if (state == null)
            state = background.gameObject.AddComponent<OperatorHudVisual>();

        state.Initialize(background, text, dots, glow);
    }

    private static Image CreateImageChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.GetComponent<Image>();

        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(parent, false);
        return child.GetComponent<Image>();
    }

    private static TextMeshProUGUI CreateSymbol(Transform parent)
    {
        Transform existing = parent.Find("Toon Operator Symbol");
        if (existing != null)
            return existing.GetComponent<TextMeshProUGUI>();

        GameObject child = new GameObject(
            "Toon Operator Symbol",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        child.transform.SetParent(parent, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return child.GetComponent<TextMeshProUGUI>();
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static Sprite CreateToonOctagon(Color accent)
    {
        const int size = 128;
        Texture2D texture = NewTexture("OperatorToonOctagon", size);
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);
        Color outerLine = new Color(0.015f, 0.07f, 0.08f, 1f);
        Color innerLine = new Color(accent.r, accent.g, accent.b, 1f);
        Color fillTop = new Color(0.08f, 0.34f, 0.36f, 1f);
        Color fillBottom = new Color(0.025f, 0.16f, 0.19f, 1f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2(x, y) - center;
            float edge = OctagonDistance(p);
            Color pixel = Color.clear;

            if (edge <= 61f)
            {
                if (edge >= 57f)
                    pixel = outerLine;
                else if (edge >= 50f)
                    pixel = innerLine;
                else
                {
                    float vertical = Mathf.InverseLerp(8f, 112f, y);
                    pixel = Color.Lerp(fillBottom, fillTop, vertical);

                    // Faixa de brilho desenhada como um highlight de toon shader.
                    if (y > 76 && y < 91 && x > 28 && x < 92 && edge < 45f)
                        pixel = Color.Lerp(pixel, accent, 0.24f);
                }
            }

            texture.SetPixel(x, y, pixel);
        }

        return FinishSprite(texture);
    }

    private static Sprite CreateGlowOctagon(Color accent)
    {
        const int size = 128;
        Texture2D texture = NewTexture("OperatorToonGlow", size);
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float edge = OctagonDistance(new Vector2(x, y) - center);
            float alpha = edge <= 61f ? Mathf.InverseLerp(61f, 42f, edge) * 0.36f : 0f;
            texture.SetPixel(x, y, new Color(accent.r, accent.g, accent.b, alpha));
        }

        return FinishSprite(texture);
    }

    private static Sprite CreateDitherSprite(Color accent)
    {
        const int size = 128;
        Texture2D texture = NewTexture("OperatorSelectedDither", size);
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float edge = OctagonDistance(new Vector2(x, y) - center);
            bool inside = edge < 46f;
            bool dot = ((x + 3) % 14 <= 3) && ((y + 5) % 14 <= 3);
            Color pixel = inside && dot
                ? new Color(accent.r, accent.g, accent.b, 0.78f)
                : Color.clear;
            texture.SetPixel(x, y, pixel);
        }

        return FinishSprite(texture);
    }

    private static float OctagonDistance(Vector2 point)
    {
        float ax = Mathf.Abs(point.x);
        float ay = Mathf.Abs(point.y);
        return Mathf.Max(ax, ay) + 0.42f * Mathf.Min(ax, ay);
    }

    private static Texture2D NewTexture(string name, int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    private static Sprite FinishSprite(Texture2D texture)
    {
        texture.Apply(false, true);
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }
}

public sealed class OperatorHudVisual : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float selectedThreshold = 0.85f;

    private Image background;
    private TextMeshProUGUI symbol;
    private Image ditherDots;
    private Image glow;
    private Vector3 baseScale;

    public void Initialize(Image targetBackground, TextMeshProUGUI targetSymbol, Image dots, Image targetGlow)
    {
        background = targetBackground;
        symbol = targetSymbol;
        ditherDots = dots;
        glow = targetGlow;
        baseScale = transform.localScale;
        Refresh(true);
    }

    private void LateUpdate()
    {
        Refresh(false);
    }

    private void Refresh(bool immediate)
    {
        if (background == null || symbol == null || ditherDots == null || glow == null)
            return;

        float sourceAlpha = background.color.a;
        bool selected = sourceAlpha >= selectedThreshold;

        Color symbolColor = symbol.color;
        symbolColor.a = Mathf.Max(0.45f, sourceAlpha);
        symbol.color = symbolColor;

        ditherDots.enabled = selected;
        glow.enabled = selected;

        Vector3 targetScale = selected ? baseScale * 1.08f : baseScale;
        float t = immediate ? 1f : 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, t);
    }
}
