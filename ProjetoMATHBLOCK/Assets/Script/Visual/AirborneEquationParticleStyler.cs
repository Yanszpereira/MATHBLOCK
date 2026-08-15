using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dá acabamento consistente às contas que flutuam no cenário sem alterar
/// os demais sistemas de partículas do jogo.
/// </summary>
public sealed class AirborneEquationParticleStyler : MonoBehaviour
{
    private const string ShaderName = "MathBlock/Airborne Equation Particle";
    private static readonly List<Material> runtimeMaterials = new List<Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StyleSceneEquations()
    {
        for (int index = runtimeMaterials.Count - 1; index >= 0; index--)
        {
            if (runtimeMaterials[index] != null)
                Destroy(runtimeMaterials[index]);
        }
        runtimeMaterials.Clear();

        Shader shader = Shader.Find(ShaderName);
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning($"Shader de partículas '{ShaderName}' não encontrado ou incompatível.");
            return;
        }

        ParticleSystemRenderer[] renderers = FindObjectsByType<ParticleSystemRenderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (ParticleSystemRenderer target in renderers)
        {
            if (target == null || !IsAirborneEquation(target.transform))
                continue;

            ApplyMaterial(target, shader);
            target.sortMode = ParticleSystemSortMode.YoungestInFront;
            target.maxParticleSize = Mathf.Min(target.maxParticleSize, 0.28f);
            target.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            target.receiveShadows = false;
        }
    }

    private static bool IsAirborneEquation(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.name.StartsWith("Contas", System.StringComparison.OrdinalIgnoreCase))
                return true;
            current = current.parent;
        }
        return false;
    }

    private static void ApplyMaterial(ParticleSystemRenderer target, Shader shader)
    {
        Material source = target.sharedMaterial;
        Texture texture = source != null && source.HasProperty("_MainTex")
            ? source.GetTexture("_MainTex")
            : null;
        Color color = source != null && source.HasProperty("_Color")
            ? source.GetColor("_Color")
            : Color.white;

        Material material = new Material(shader)
        {
            name = (source != null ? source.name : target.name) + " (Airborne Equation Runtime)",
            hideFlags = HideFlags.HideAndDontSave
        };
        if (texture != null)
            material.SetTexture("_MainTex", texture);
        material.SetColor("_Color", color);
        material.SetFloat("_Brightness", 0.9f);
        material.SetFloat("_AlphaFeather", 0.14f);
        material.SetFloat("_Softness", 2.5f);
        target.sharedMaterial = material;
        runtimeMaterials.Add(material);
    }
}
