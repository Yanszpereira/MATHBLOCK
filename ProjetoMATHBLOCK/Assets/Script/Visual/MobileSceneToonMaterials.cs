using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public sealed class MobileSceneToonMaterials : MonoBehaviour
{
    private const string ToonShaderName = "Custom/URPToonShader";
    private readonly List<Material> runtimeMaterials = new List<Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsMobile() || (!scene.name.StartsWith("Fase") && scene.name != "MainScene"))
            return;

        GameObject installer = new GameObject("Mobile - Scene Toon Materials");
        SceneManager.MoveGameObjectToScene(installer, scene);
        installer.AddComponent<MobileSceneToonMaterials>();
    }

    private static bool IsMobile()
    {
        return Application.isMobilePlatform || UnityEngine.Device.Application.isMobilePlatform;
    }

    private void Start()
    {
        Shader toonShader = Shader.Find(ToonShaderName);
        if (toonShader == null || !toonShader.isSupported)
        {
            Debug.LogError($"Mobile Toon: shader '{ToonShaderName}' indisponivel.", this);
            return;
        }

        foreach (Renderer target in FindObjectsByType<Renderer>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (!CanConvert(target))
                continue;

            ConvertRenderer(target, toonShader);
        }
    }

    private bool CanConvert(Renderer target)
    {
        if (target == null || target.gameObject.scene != gameObject.scene ||
            target is ParticleSystemRenderer || target is TrailRenderer || target is LineRenderer)
            return false;

        if (target.GetComponent<TextMesh>() != null || target.GetComponentInParent<Canvas>() != null ||
            target.GetComponentInParent<MathBlockValue>() != null ||
            target.GetComponentInParent<PencilGunOverlaySetup>() != null ||
            target.GetComponentInParent<ToonCloudSkyGenerator>() != null)
            return false;

        if (BelongsToSky(target.transform))
            return false;

        Material[] materials = target.sharedMaterials;
        if (materials == null || materials.Length == 0)
            return false;

        for (int i = 0; i < materials.Length; i++)
        {
            if (!IsOpaqueSceneMaterial(materials[i]))
                return false;
        }

        return true;
    }

    private static bool IsOpaqueSceneMaterial(Material material)
    {
        if (material == null || material.shader == null)
            return false;

        string shaderName = material.shader.name;
        if (shaderName.Contains("Sky", System.StringComparison.OrdinalIgnoreCase) ||
            shaderName.Contains("AbyssCylinder", System.StringComparison.OrdinalIgnoreCase) ||
            material.name.Contains("Sky", System.StringComparison.OrdinalIgnoreCase) ||
            shaderName.Contains("Particle") ||
            shaderName.Contains("UI/") || shaderName.Contains("TextMesh") ||
            shaderName.Contains("Sprite"))
            return false;

        if (material.renderQueue >= (int)RenderQueue.AlphaTest)
            return false;

        if (material.IsKeywordEnabled("_EMISSION") ||
            (material.HasProperty("_EmissionColor") &&
             material.GetColor("_EmissionColor").maxColorComponent > 0.001f))
            return false;

        if (material.HasProperty("_ZWrite") && material.GetFloat("_ZWrite") < 0.5f)
            return false;

        Color color = material.HasProperty("_BaseColor")
            ? material.GetColor("_BaseColor")
            : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
        return color.a >= 0.999f;
    }

    private static bool BelongsToSky(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            string objectName = current.name;
            if (objectName.Contains("Sky", System.StringComparison.OrdinalIgnoreCase) ||
                objectName.Contains("Ceu", System.StringComparison.OrdinalIgnoreCase) ||
                objectName.Contains("Céu", System.StringComparison.OrdinalIgnoreCase))
                return true;

            current = current.parent;
        }

        return false;
    }

    private void ConvertRenderer(Renderer target, Shader toonShader)
    {
        Material[] originals = target.sharedMaterials;
        Material[] converted = new Material[originals.Length];

        for (int i = 0; i < originals.Length; i++)
        {
            Material source = originals[i];
            string textureProperty = source.HasProperty("_MainTex")
                ? "_MainTex"
                : source.HasProperty("_BaseMap") ? "_BaseMap" : null;
            Texture texture = textureProperty != null ? source.GetTexture(textureProperty) : null;
            Color color = source.HasProperty("_BaseColor")
                ? source.GetColor("_BaseColor")
                : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;

            Material toon = new Material(toonShader)
            {
                name = source.name + " (Mobile Scene Toon)",
                hideFlags = HideFlags.HideAndDontSave
            };
            toon.SetColor("_BaseColor", color);
            toon.SetColor("_Color", color);
            if (texture != null)
            {
                toon.SetTexture("_MainTex", texture);
                toon.SetTextureScale("_MainTex", source.GetTextureScale(textureProperty));
                toon.SetTextureOffset("_MainTex", source.GetTextureOffset(textureProperty));
            }
            toon.SetFloat("_ZWrite", 1f);
            toon.SetFloat("_SrcBlend", (float)BlendMode.One);
            toon.SetFloat("_DstBlend", (float)BlendMode.Zero);
            toon.renderQueue = (int)RenderQueue.Geometry;
            toon.EnableKeyword("_SPECULAR_ON");
            toon.EnableKeyword("_RIM_ON");
            toon.EnableKeyword("_OUTLINE_ON");
            converted[i] = toon;
            runtimeMaterials.Add(toon);
        }

        target.sharedMaterials = converted;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            if (runtimeMaterials[i] != null)
                Destroy(runtimeMaterials[i]);
        }
    }
}
