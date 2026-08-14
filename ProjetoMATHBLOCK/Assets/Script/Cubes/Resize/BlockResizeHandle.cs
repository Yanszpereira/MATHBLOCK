using UnityEngine;

public enum ResizeHandlePosition
{
    Top,
    Bottom,
    Left,
    Right
}

public enum ResizeHandleVisualState
{
    Normal,
    Hover,
    Selected,
    Allowed,
    Blocked
}

[DisallowMultipleComponent]
public sealed class BlockResizeHandle : MonoBehaviour
{
    private const int ArrowTextureWidth = 64;
    private const int ArrowTextureHeight = 96;
    private const float PixelsPerUnit = 96f;
    private static Sprite dottedArrowSprite;

    [SerializeField] private ResizeHandlePosition position;
    [SerializeField] private Collider interactionCollider;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Renderer[] visualRenderers;

    [Header("Placeholder Colors")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.65f, 1f, 1f);
    [SerializeField] private Color hoverColor = Color.yellow;
    [SerializeField] private Color selectedColor = new Color(1f, 0.5f, 0f, 1f);
    [SerializeField] private Color allowedColor = Color.green;
    [SerializeField] private Color blockedColor = Color.red;

    private MaterialPropertyBlock propertyBlock;
    private SpriteRenderer spriteRenderer;

    public ResizeHandlePosition Position => position;
    public Collider InteractionCollider => interactionCollider;
    public Transform VisualRoot => visualRoot;

    private void Awake()
    {
        ResolveReferences();
        ReplaceModelWithDottedSprite();
        SetVisualState(ResizeHandleVisualState.Normal);
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    public void SetVisualState(ResizeHandleVisualState state)
    {
        ResolveReferences();
        propertyBlock ??= new MaterialPropertyBlock();
        Color color = GetColor(state);

        if (spriteRenderer != null)
            spriteRenderer.color = color;

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            Renderer targetRenderer = visualRenderers[i];
            if (targetRenderer == null)
                continue;

            targetRenderer.GetPropertyBlock(propertyBlock);
            Material material = targetRenderer.sharedMaterial;
            if (material != null && material.HasProperty("_BaseColor"))
                propertyBlock.SetColor("_BaseColor", color);
            if (material != null && material.HasProperty("_Color"))
                propertyBlock.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ReplaceModelWithDottedSprite()
    {
        if (visualRoot == null)
            visualRoot = transform;

        Renderer[] oldRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < oldRenderers.Length; i++)
        {
            if (oldRenderers[i] != null)
                oldRenderers[i].enabled = false;
        }

        GameObject spriteObject = new GameObject("Dotted 2D Arrow");
        spriteObject.layer = gameObject.layer;
        spriteObject.transform.SetParent(visualRoot, false);
        spriteObject.transform.localPosition = new Vector3(0f, 0f, -0.025f);
        spriteObject.transform.localRotation = Quaternion.identity;
        spriteObject.transform.localScale = Vector3.one * 1.15f;

        spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetOrCreateDottedArrowSprite();
        spriteRenderer.sortingOrder = 250;
        spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        spriteRenderer.receiveShadows = false;
        visualRenderers = new Renderer[] { spriteRenderer };
    }

    private static Sprite GetOrCreateDottedArrowSprite()
    {
        if (dottedArrowSprite != null)
            return dottedArrowSprite;

        Texture2D texture = new Texture2D(
            ArrowTextureWidth,
            ArrowTextureHeight,
            TextureFormat.RGBA32,
            false)
        {
            name = "MathBlock Dotted Resize Arrow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[ArrowTextureWidth * ArrowTextureHeight];
        Color solid = Color.white;
        Color transparent = new Color(1f, 1f, 1f, 0f);

        for (int y = 0; y < ArrowTextureHeight; y++)
        {
            for (int x = 0; x < ArrowTextureWidth; x++)
            {
                float normalizedX = ((x + 0.5f) / ArrowTextureWidth) - 0.5f;
                float normalizedY = ((y + 0.5f) / ArrowTextureHeight) - 0.5f;
                bool shaft = Mathf.Abs(normalizedX) <= 0.115f && normalizedY >= -0.46f && normalizedY <= 0.12f;
                float headHalfWidth = Mathf.Max(0f, (0.48f - normalizedY) * 0.92f);
                bool head = normalizedY >= -0.02f && normalizedY <= 0.48f && Mathf.Abs(normalizedX) <= headHalfWidth;
                bool insideArrow = shaft || head;

                int dotX = Mathf.Abs((x + 2) % 11 - 5);
                int dotY = Mathf.Abs((y + 1) % 11 - 5);
                bool dottedHole = (dotX * dotX) + (dotY * dotY) <= 5;
                pixels[(y * ArrowTextureWidth) + x] = insideArrow && !dottedHole ? solid : transparent;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        dottedArrowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, ArrowTextureWidth, ArrowTextureHeight),
            new Vector2(0.5f, 0.12f),
            PixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
        dottedArrowSprite.name = "MathBlock Dotted Resize Arrow";
        dottedArrowSprite.hideFlags = HideFlags.HideAndDontSave;
        return dottedArrowSprite;
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
        if (interactionCollider == null)
            interactionCollider = GetComponentInChildren<Collider>(true);

        if (visualRoot == null)
        {
            Renderer firstRenderer = GetComponentInChildren<Renderer>(true);
            if (firstRenderer != null)
                visualRoot = firstRenderer.transform.parent != null ? firstRenderer.transform.parent : firstRenderer.transform;
        }

        if (visualRenderers == null || visualRenderers.Length == 0)
            visualRenderers = visualRoot != null ? visualRoot.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();
    }
}
