using System.Collections;
using UnityEngine;

/// <summary>
/// Applies the equipped operator color only to the tip renderer of the player
/// pencil. The original material remains untouched when no operator is equipped.
/// </summary>
public sealed class PencilTipOperatorColor : MonoBehaviour
{
    [Header("Ponta do lápis")]
    [SerializeField] private Transform tipRoot;
    [SerializeField] private Renderer tipRenderer;

    [Header("Cores dos operadores")]
    [SerializeField] private Color additionColor = new Color(0.1f, 1f, 0.25f, 1f);
    [SerializeField] private Color subtractionColor = new Color(1f, 0.45f, 0.05f, 1f);
    [SerializeField] private Color multiplicationColor = new Color(0.1f, 0.35f, 1f, 1f);
    [SerializeField] private Color divisionColor = new Color(1f, 0.08f, 0.08f, 1f);

    [Header("Brilho da troca")]
    [SerializeField, Min(0.01f)] private float glowDuration = 0.5f;
    [SerializeField, Min(0f)] private float glowIntensity = 4f;
    [SerializeField, Min(0.01f)] private float glowRange = 1.25f;
    [SerializeField, Range(0f, 1f)] private float glowColorBoost = 0.35f;
    [SerializeField, Min(0f)] private float glowEmissionStrength = 3f;

    private MaterialPropertyBlock propertyBlock;
    private Light glowLight;
    private Coroutine glowRoutine;
    private Color activeOperatorColor;
    private void Start()
    {
        ResolveTipRenderer();
    }

    public void ApplyOperatorColor(GravityInteract.PencilOperator operatorType)
    {
        if (!ResolveTipRenderer())
            return;

        if (operatorType == GravityInteract.PencilOperator.None)
        {
            // Clearing the property block restores the material exactly as it
            // was configured, including the material prepared by the overlay.
            tipRenderer.SetPropertyBlock(null);
            StopGlow();
            return;
        }

        Color color;
        switch (operatorType)
        {
            case GravityInteract.PencilOperator.Addition:
                color = additionColor;
                break;

            case GravityInteract.PencilOperator.Subtraction:
                color = subtractionColor;
                break;

            case GravityInteract.PencilOperator.Multiplication:
                color = multiplicationColor;
                break;

            case GravityInteract.PencilOperator.Division:
                color = divisionColor;
                break;

            default:
                return;
        }

        activeOperatorColor = color;
        SetTipColor(color, Color.black, 0f);
        TriggerGlow(color);
        Debug.Log($"Ponta do lápis atualizada para o operador {operatorType}.", this);
    }

    private void SetTipColor(Color color, Color emissionColor, float emissionStrength)
    {
        propertyBlock ??= new MaterialPropertyBlock();
        tipRenderer.GetPropertyBlock(propertyBlock);

        Material material = tipRenderer.sharedMaterial;
        if (material != null && material.HasProperty("_BaseColor"))
            propertyBlock.SetColor("_BaseColor", color);
        if (material != null && material.HasProperty("_Color"))
            propertyBlock.SetColor("_Color", color);
        if (material != null && material.HasProperty("_EmissionColor"))
            propertyBlock.SetColor("_EmissionColor", emissionColor);
        if (material != null && material.HasProperty("_EmissionStrength"))
            propertyBlock.SetFloat("_EmissionStrength", emissionStrength);

        tipRenderer.SetPropertyBlock(propertyBlock);
    }

    private void TriggerGlow(Color color)
    {
        EnsureGlowLight();

        if (glowLight == null)
            return;

        glowLight.color = color;
        glowLight.range = glowRange;
        glowLight.intensity = glowIntensity;
        glowLight.enabled = true;

        if (glowRoutine != null)
            StopCoroutine(glowRoutine);

        glowRoutine = StartCoroutine(GlowRoutine());
    }

    private IEnumerator GlowRoutine()
    {
        float duration = Mathf.Max(0.01f, glowDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float normalizedTime = elapsed / duration;
            float envelope = 1f - normalizedTime;
            glowLight.intensity = glowIntensity * envelope;
            SetTipColor(
                Color.Lerp(activeOperatorColor, Color.white, envelope * glowColorBoost),
                activeOperatorColor,
                glowEmissionStrength * envelope);
            elapsed += Time.deltaTime;
            yield return null;
        }

        glowLight.intensity = 0f;
        glowLight.enabled = false;
        glowRoutine = null;
        SetTipColor(activeOperatorColor, Color.black, 0f);
    }

    private void EnsureGlowLight()
    {
        if (glowLight != null)
            return;

        GameObject glowObject = new GameObject("Pencil Tip Operator Glow");
        glowObject.transform.SetParent(tipRoot != null ? tipRoot : tipRenderer.transform, true);
        glowObject.transform.position = tipRenderer.bounds.center;

        glowLight = glowObject.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.shadows = LightShadows.None;
        glowLight.renderMode = LightRenderMode.ForcePixel;
        glowLight.intensity = 0f;
        glowLight.enabled = false;
    }

    private void StopGlow()
    {
        if (glowRoutine != null)
        {
            StopCoroutine(glowRoutine);
            glowRoutine = null;
        }

        if (glowLight != null)
        {
            glowLight.intensity = 0f;
            glowLight.enabled = false;
        }
    }

    private void OnDestroy()
    {
        StopGlow();
    }

    private bool ResolveTipRenderer()
    {
        if (tipRenderer != null)
            return true;

        if (tipRoot == null)
            tipRoot = FindDescendant(transform, "ponta");

        if (tipRoot != null)
            tipRenderer = tipRoot.GetComponent<Renderer>() ?? tipRoot.GetComponentInChildren<Renderer>(true);

        if (tipRenderer != null)
            return true;

        return false;
    }

    private static Transform FindDescendant(Transform root, string targetName)
    {
        if (root == null)
            return null;

        if (string.Equals(root.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            return root;

        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            Transform result = FindDescendant(root.GetChild(childIndex), targetName);
            if (result != null)
                return result;
        }

        return null;
    }
}
