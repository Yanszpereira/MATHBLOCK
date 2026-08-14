using UnityEngine;

public enum ResizeHandlePosition { Top, Bottom, Left, Right }
public enum ResizeHandleVisualState { Normal, Hover, Selected, Allowed, Blocked }

[DisallowMultipleComponent]
public sealed class BlockResizeHandle : MonoBehaviour
{
    private const int TextureSize = 256;
    private const float PixelsPerUnit = 256f;
    private const float BaseVisualScale = 0.72f;

    [SerializeField] private ResizeHandlePosition position;
    [SerializeField] private Collider interactionCollider;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Renderer[] visualRenderers;

    [Header("Arrow Colors")]
    [SerializeField] private Color normalColor = new Color(0.20f, 0.48f, 1f, 1f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.82f, 0.20f, 1f);
    [SerializeField] private Color selectedColor = new Color(1f, 0.48f, 0.12f, 1f);
    [SerializeField] private Color allowedColor = new Color(0.25f, 1f, 0.50f, 1f);
    [SerializeField] private Color blockedColor = new Color(1f, 0.18f, 0.22f, 1f);
    [SerializeField] private Color fillColor = new Color(0.045f, 0.075f, 0.12f, 0.94f);

    [Header("Visual Feedback")]
    [SerializeField, Min(0f)] private float transitionSpeed = 16f;
    [SerializeField, Range(1f, 1.2f)] private float hoverScale = 1.06f;
    [SerializeField, Range(0f, 0.06f)] private float pulseAmount = 0.015f;
    [SerializeField, Min(0f)] private float pulseSpeed = 4.5f;
    [SerializeField, Range(0f, 1f)] private float glowOpacity = 0.34f;
    [SerializeField, Range(0f, 1f)] private float dotsOpacity = 0.72f;

    private readonly Sprite[] stateSprites = new Sprite[5];
    private SpriteRenderer spriteRenderer;
    private ResizeHandleVisualState visualState;
    private float currentScale = 1f;
    private float targetScale = 1f;
    private bool visualsBuilt;

    public ResizeHandlePosition Position => position;
    public Collider InteractionCollider => interactionCollider;
    public Transform VisualRoot => visualRoot;

    private void Awake()
    {
        ResolveReferences();
        BuildSingleArrowVisual();
        SetVisualState(ResizeHandleVisualState.Normal);
    }

    private void Update()
    {
        if (spriteRenderer == null) return;

        float blend = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
        currentScale = Mathf.Lerp(currentScale, targetScale, blend);
        bool animate = visualState == ResizeHandleVisualState.Hover
            || visualState == ResizeHandleVisualState.Selected
            || visualState == ResizeHandleVisualState.Allowed;
        float pulse = animate ? Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount : 0f;
        spriteRenderer.transform.localScale = Vector3.one * (BaseVisualScale * (currentScale + pulse));
    }

    private void Reset() => ResolveReferences();
    private void OnValidate() => ResolveReferences();

    public void SetVisualState(ResizeHandleVisualState state)
    {
        visualState = state;
        targetScale = state == ResizeHandleVisualState.Normal ? 1f : hoverScale;
        if (spriteRenderer != null)
            spriteRenderer.sprite = GetStateSprite(state);
    }

    private void BuildSingleArrowVisual()
    {
        if (visualsBuilt) return;
        visualsBuilt = true;
        if (visualRoot == null) visualRoot = transform;

        foreach (Renderer oldRenderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            if (oldRenderer != null) oldRenderer.enabled = false;

        GameObject arrowObject = new GameObject("Resize Arrow - Single Image");
        arrowObject.layer = gameObject.layer;
        arrowObject.transform.SetParent(visualRoot, false);
        arrowObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        arrowObject.transform.localScale = Vector3.one * BaseVisualScale;

        spriteRenderer = arrowObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingOrder = 250;
        spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        spriteRenderer.receiveShadows = false;
        visualRenderers = new Renderer[] { spriteRenderer };
    }

    private Sprite GetStateSprite(ResizeHandleVisualState state)
    {
        int index = (int)state;
        if (stateSprites[index] == null)
            stateSprites[index] = CreateCompositeSprite(GetColor(state), state.ToString());
        return stateSprites[index];
    }

    private Sprite CreateCompositeSprite(Color accent, string stateName)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "MathBlock Arrow " + stateName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[TextureSize * TextureSize];
        float aa = 1.5f / TextureSize;
        const float borderWidth = 0.040f;
        const float glowWidth = 0.065f;

        for (int y = 0; y < TextureSize; y++)
        for (int x = 0; x < TextureSize; x++)
        {
            Vector2 p = new Vector2(((x + 0.5f) / TextureSize) - 0.5f, ((y + 0.5f) / TextureSize) - 0.5f);
            float distance = ArrowDistance(p);
            Color pixel = Color.clear;

            // Glow, borda, preenchimento e pontos são compostos neste único pixel.
            if (distance <= glowWidth)
            {
                float glow = 1f - SmoothStep(0f, glowWidth, Mathf.Max(0f, distance));
                pixel = new Color(accent.r, accent.g, accent.b, glow * glowOpacity);
            }
            if (distance <= aa)
            {
                float edgeAlpha = 1f - SmoothStep(-aa, aa, distance);
                pixel = AlphaOver(pixel, new Color(accent.r, accent.g, accent.b, edgeAlpha));
            }
            if (distance <= -borderWidth + aa)
            {
                float fillAlpha = 1f - SmoothStep(-borderWidth - aa, -borderWidth + aa, distance);
                Color opaqueFill = new Color(fillColor.r, fillColor.g, fillColor.b, fillColor.a * fillAlpha);
                pixel = AlphaOver(pixel, opaqueFill);

                const float spacing = 0.095f;
                const float radius = 0.016f;
                float gx = Mathf.Repeat(p.x + spacing * 0.5f, spacing) - spacing * 0.5f;
                float gy = Mathf.Repeat(p.y + spacing * 0.5f, spacing) - spacing * 0.5f;
                float dot = 1f - SmoothStep(radius - aa, radius + aa, new Vector2(gx, gy).magnitude);
                pixel = AlphaOver(pixel, new Color(accent.r, accent.g, accent.b, dot * dotsOpacity * fillAlpha));
            }
            pixels[y * TextureSize + x] = pixel;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.12f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Color AlphaOver(Color under, Color over)
    {
        float alpha = over.a + under.a * (1f - over.a);
        if (alpha <= 0.0001f) return Color.clear;
        return new Color(
            (over.r * over.a + under.r * under.a * (1f - over.a)) / alpha,
            (over.g * over.a + under.g * under.a * (1f - over.a)) / alpha,
            (over.b * over.a + under.b * under.a * (1f - over.a)) / alpha,
            alpha);
    }

    private static float ArrowDistance(Vector2 p)
    {
        const float radius = 0.028f;
        Vector2 box = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y + 0.18f)) - new Vector2(0.085f - radius, 0.25f - radius);
        float shaft = Mathf.Min(Mathf.Max(box.x, box.y), 0f)
            + new Vector2(Mathf.Max(box.x, 0f), Mathf.Max(box.y, 0f)).magnitude - radius;
        float head = SdTriangle(p, new Vector2(0f, 0.46f - radius), new Vector2(0.34f - radius, 0.08f + radius), new Vector2(-0.34f + radius, 0.08f + radius)) - radius;
        return Mathf.Min(shaft, head);
    }

    private static float SdTriangle(Vector2 p, Vector2 p0, Vector2 p1, Vector2 p2)
    {
        Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
        Vector2 v0 = p - p0, v1 = p - p1, v2 = p - p2;
        Vector2 q0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
        Vector2 q1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
        Vector2 q2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
        float s = Mathf.Sign((e0.x * e2.y) - (e0.y * e2.x));
        Vector2 d0 = new Vector2(Vector2.Dot(q0, q0), s * Cross(v0, e0));
        Vector2 d1 = new Vector2(Vector2.Dot(q1, q1), s * Cross(v1, e1));
        Vector2 d2 = new Vector2(Vector2.Dot(q2, q2), s * Cross(v2, e2));
        Vector2 d = Vector2.Min(Vector2.Min(d0, d1), d2);
        return -Mathf.Sqrt(d.x) * Mathf.Sign(d.y);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    private static float SmoothStep(float a, float b, float value)
    {
        float t = Mathf.Clamp01((value - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private Color GetColor(ResizeHandleVisualState state)
    {
        switch (state)
        {
            case ResizeHandleVisualState.Hover: return hoverColor;
            case ResizeHandleVisualState.Selected: return selectedColor;
            case ResizeHandleVisualState.Allowed: return allowedColor;
            case ResizeHandleVisualState.Blocked: return blockedColor;
            default: return normalColor;
        }
    }

    private void ResolveReferences()
    {
        if (interactionCollider == null) interactionCollider = GetComponentInChildren<Collider>(true);
        if (visualRoot == null)
        {
            Renderer first = GetComponentInChildren<Renderer>(true);
            if (first != null) visualRoot = first.transform.parent != null ? first.transform.parent : first.transform;
        }
        if (visualRenderers == null || visualRenderers.Length == 0)
            visualRenderers = visualRoot != null ? visualRoot.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
    }
}
