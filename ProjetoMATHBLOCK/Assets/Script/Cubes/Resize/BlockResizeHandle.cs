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

    public ResizeHandlePosition Position => position;
    public Collider InteractionCollider => interactionCollider;
    public Transform VisualRoot => visualRoot;

    private void Awake()
    {
        ResolveReferences();
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
