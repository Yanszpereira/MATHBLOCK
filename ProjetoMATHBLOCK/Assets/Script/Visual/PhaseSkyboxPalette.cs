using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mantem o modelo de ceu procedural da Fase 1 e varia somente a paleta
/// das fases seguintes. Assim todas as fases conservam a mesma linguagem visual.
/// </summary>
public static class PhaseSkyboxPalette
{
    private static Material runtimeSkybox;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        GlobalSceneBootstrap.Register(ApplyToScene, -500);
    }

    private static void ApplyToScene(Scene scene)
    {
        if (!TryGetPalette(scene.name, out Color skyTint, out Color groundColor))
            return;

        Shader shader = Shader.Find("Skybox/Procedural");
        if (shader == null)
        {
            Debug.LogWarning("Skybox procedural nao encontrado; a paleta da fase nao foi aplicada.");
            return;
        }

        if (runtimeSkybox != null)
            Object.Destroy(runtimeSkybox);

        runtimeSkybox = new Material(shader)
        {
            name = $"Skybox {scene.name} (Runtime)",
            hideFlags = HideFlags.DontSave
        };

        // Mesmos parametros de SkyboxEscuro, usado pela Fase 1.
        runtimeSkybox.SetFloat("_SunDisk", 2f);
        runtimeSkybox.SetFloat("_SunSize", 0.02f);
        runtimeSkybox.SetFloat("_SunSizeConvergence", 5f);
        runtimeSkybox.SetFloat("_AtmosphereThickness", 0.72f);
        runtimeSkybox.SetFloat("_Exposure", 1.05f);
        runtimeSkybox.SetColor("_SkyTint", skyTint);
        runtimeSkybox.SetColor("_GroundColor", groundColor);

        RenderSettings.skybox = runtimeSkybox;
        DynamicGI.UpdateEnvironment();
    }

    private static bool TryGetPalette(string sceneName, out Color skyTint, out Color groundColor)
    {
        string normalized = sceneName.ToLowerInvariant();

        if (normalized.Contains("fase 2") || normalized.Contains("fase2"))
        {
            skyTint = new Color(0.68f, 0.48f, 0.92f, 1f);
            groundColor = new Color(0.25f, 0.13f, 0.36f, 1f);
            return true;
        }

        if (normalized.Contains("fase 3") || normalized.Contains("fase3"))
        {
            skyTint = new Color(1f, 0.49f, 0.22f, 1f);
            groundColor = new Color(0.38f, 0.13f, 0.055f, 1f);
            return true;
        }

        if (normalized.Contains("fase 4") || normalized.Contains("fase4"))
        {
            skyTint = new Color(0.25f, 0.62f, 1f, 1f);
            groundColor = new Color(0.06f, 0.18f, 0.38f, 1f);
            return true;
        }

        skyTint = default;
        groundColor = default;
        return false;
    }
}
