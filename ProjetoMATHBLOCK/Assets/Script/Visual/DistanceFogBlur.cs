using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public sealed class DistanceFogBlur : MonoBehaviour
{
    public const string PreferenceKey = "visual.distanceBlur.enabled";
    public static bool UserEnabled => PlayerPrefs.GetInt(PreferenceKey, 1) != 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureDefaultPreference()
    {
        if (PlayerPrefs.HasKey(PreferenceKey))
            return;

        PlayerPrefs.SetInt(PreferenceKey, 1);
        PlayerPrefs.Save();
    }
    [Header("Distance fog")]
    [SerializeField, Min(0f)] private float startDistance = 24f;
    [SerializeField, Min(0.1f)] private float fullBlurDistance = 85f;
    [SerializeField, Range(0f, 5f)] private float blurRadius = 2.2f;
    [SerializeField, Range(0f, 1f)] private float fogColorStrength = 0.12f;
    [SerializeField] private Color fogColor = new Color(0.46f, 0.56f, 0.68f, 1f);

    [Header("Subtle dotted character")]
    [SerializeField, Range(0f, 0.35f)] private float dotStrength = 0.14f;
    [SerializeField, Range(2f, 24f)] private float dotScale = 7f;
    [SerializeField] private Color dotColor = new Color(0.055f, 0.012f, 0.085f, 1f);
    [SerializeField, Range(2f, 12f)] private float phaseTwoGroundDotScale = 4f;
    [SerializeField, Range(0f, 1f)] private float phaseTwoGroundDotStrength = 0.32f;

    [Header("Downward look atmosphere")]
    [SerializeField, Range(0f, 0.8f)] private float downwardDarkening = 0.52f;
    [SerializeField, Range(1f, 3f)] private float downwardBlurMultiplier = 1.55f;
    [SerializeField, Range(2f, 40f)] private float downwardDotScale = 18f;

    [Header("Performance")]
    [SerializeField, Range(1, 4)] private int desktopDownsample = 1;
    [SerializeField, Range(1, 4)] private int androidDownsample = 2;
    [SerializeField] private bool disableOnVeryLowMemoryAndroid = true;
    [SerializeField, Min(512)] private int minimumMemoryMb = 1800;

    private Material material;
    private Material exclusionMaskMaterial;
    private Camera targetCamera;
    private GravityInteract gravityInteract;
    private ParticleSystemRenderer[] particleRenderers;
    private Renderer[] taggedCloudRenderers;
    private Renderer[] phaseOneCubeRenderers;
    private Renderer[] mathBlockRenderers;
    private Renderer[] countdownTimerRenderers;
    private Renderer[] phaseTwoScenarioRenderers;
    private float nextParticleRefreshTime;
    private readonly HashSet<Renderer> exclusionRenderers = new HashSet<Renderer>();

    private static readonly int StartDistanceId = Shader.PropertyToID("_StartDistance");
    private static readonly int FullDistanceId = Shader.PropertyToID("_FullDistance");
    private static readonly int BlurRadiusId = Shader.PropertyToID("_BlurRadius");
    private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
    private static readonly int FogColorStrengthId = Shader.PropertyToID("_FogColorStrength");
    private static readonly int DotStrengthId = Shader.PropertyToID("_DotStrength");
    private static readonly int DotScaleId = Shader.PropertyToID("_DotScale");
    private static readonly int DotColorId = Shader.PropertyToID("_DotColor");
    private static readonly int GroundDotScaleId = Shader.PropertyToID("_GroundDotScale");
    private static readonly int GroundDotStrengthId = Shader.PropertyToID("_GroundDotStrength");
    private static readonly int DownLookId = Shader.PropertyToID("_DownLook");
    private static readonly int DownDarkeningId = Shader.PropertyToID("_DownDarkening");
    private static readonly int MobileQualityId = Shader.PropertyToID("_MobileQuality");
    private static readonly int ExclusionMaskId = Shader.PropertyToID("_ExclusionMask");
    private static readonly int DotExclusionMaskId = Shader.PropertyToID("_DotExclusionMask");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneInitializer()
    {
        GlobalSceneBootstrap.Register(InstallOnWorldCamera, -100);
    }

    private static void InstallOnWorldCamera(Scene scene)
    {
        if (!UserEnabled ||
            (!scene.name.StartsWith("Fase", System.StringComparison.OrdinalIgnoreCase) &&
             !scene.name.Equals("MainScene", System.StringComparison.OrdinalIgnoreCase)))
            return;
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        Camera worldCamera = null;
        foreach (Camera candidate in cameras)
        {
            if (candidate == null || !candidate.enabled || candidate.cullingMask == 0)
                continue;
            if (worldCamera == null || candidate.depth < worldCamera.depth)
                worldCamera = candidate;
        }

        if (worldCamera != null && worldCamera.GetComponent<DistanceFogBlur>() == null)
            worldCamera.gameObject.AddComponent<DistanceFogBlur>();
    }

    public static void SetUserEnabled(bool value)
    {
        PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
        PlayerPrefs.Save();

        DistanceFogBlur[] effects = FindObjectsByType<DistanceFogBlur>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (DistanceFogBlur effect in effects)
            if (effect != null)
                effect.enabled = value;

        if (!value || effects.Length > 0)
            return;

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        Camera worldCamera = null;
        foreach (Camera candidate in cameras)
        {
            if (candidate == null || !candidate.enabled || candidate.cullingMask == 0)
                continue;
            if (worldCamera == null || candidate.depth < worldCamera.depth)
                worldCamera = candidate;
        }
        if (worldCamera != null)
            worldCamera.gameObject.AddComponent<DistanceFogBlur>();
    }

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.depthTextureMode |= DepthTextureMode.Depth;

        if (Application.platform == RuntimePlatform.Android &&
            disableOnVeryLowMemoryAndroid && SystemInfo.systemMemorySize < minimumMemoryMb)
        {
            enabled = false;
            return;
        }

        Shader shader = Shader.Find("Hidden/MathBlock/DistanceFogBlur");
        Shader maskShader = Shader.Find("Hidden/MathBlock/BlurExclusionMask");
        if (shader != null && shader.isSupported)
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        else
            enabled = false;

        if (maskShader != null && maskShader.isSupported)
            exclusionMaskMaterial = new Material(maskShader) { hideFlags = HideFlags.HideAndDontSave };

        gravityInteract = FindFirstObjectByType<GravityInteract>();
        RefreshParticleRenderers();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (material == null || blurRadius <= 0f)
        {
            Graphics.Blit(source, destination);
            return;
        }

        bool mobile = Application.platform == RuntimePlatform.Android;
        int downsample = Mathf.Max(1, mobile ? androidDownsample : desktopDownsample);

        material.SetFloat(StartDistanceId, startDistance);
        material.SetFloat(FullDistanceId, Mathf.Max(startDistance + 0.1f, fullBlurDistance));
        // Dot com Vector3.down funciona independentemente da representacao Euler
        // usada pelo rig da camera. O efeito comeca suavemente depois do horizonte.
        float downwardAlignment = targetCamera != null
            ? Vector3.Dot(targetCamera.transform.forward.normalized, Vector3.down)
            : 0f;
        float downLook = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.72f, downwardAlignment));
        material.SetFloat(BlurRadiusId, blurRadius * Mathf.Lerp(1f, downwardBlurMultiplier, downLook));
        material.SetColor(FogColorId, fogColor);
        material.SetFloat(FogColorStrengthId, fogColorStrength);
        material.SetFloat(DotStrengthId, dotStrength);
        material.SetFloat(DotScaleId, Mathf.Lerp(dotScale, downwardDotScale, downLook));
        material.SetColor(DotColorId, dotColor);
        material.SetFloat(GroundDotScaleId, phaseTwoGroundDotScale);
        material.SetFloat(GroundDotStrengthId, phaseTwoGroundDotStrength);
        material.SetFloat(DownLookId, downLook);
        material.SetFloat(DownDarkeningId, downwardDarkening);
        material.SetFloat(MobileQualityId, mobile ? 1f : 0f);

        RenderTexture exclusionMask = BuildExclusionMask(source.width, source.height);
        RenderTexture dotExclusionMask = BuildDotExclusionMask(source.width, source.height);
        material.SetTexture(ExclusionMaskId, exclusionMask != null ? exclusionMask : Texture2D.blackTexture);
        material.SetTexture(DotExclusionMaskId, dotExclusionMask != null ? dotExclusionMask : Texture2D.blackTexture);

        if (downsample == 1)
        {
            Graphics.Blit(source, destination, material);
            if (exclusionMask != null)
                RenderTexture.ReleaseTemporary(exclusionMask);
            if (dotExclusionMask != null)
                RenderTexture.ReleaseTemporary(dotExclusionMask);
            return;
        }

        int width = Mathf.Max(1, source.width / downsample);
        int height = Mathf.Max(1, source.height / downsample);
        RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, source.format);
        temporary.filterMode = FilterMode.Bilinear;
        Graphics.Blit(source, temporary);
        Graphics.Blit(temporary, destination, material);
        RenderTexture.ReleaseTemporary(temporary);
        if (exclusionMask != null)
            RenderTexture.ReleaseTemporary(exclusionMask);
        if (dotExclusionMask != null)
            RenderTexture.ReleaseTemporary(dotExclusionMask);
    }

    private RenderTexture BuildDotExclusionMask(int width, int height)
    {
        if (exclusionMaskMaterial == null || targetCamera == null ||
            phaseTwoScenarioRenderers == null || phaseTwoScenarioRenderers.Length == 0)
            return null;

        RenderTexture mask = RenderTexture.GetTemporary(
            width,
            height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);
        mask.filterMode = FilterMode.Bilinear;

        CommandBuffer commands = CommandBufferPool.Get("MathBlock Phase 2 Dot Exclusions");
        commands.SetRenderTarget(mask);
        commands.ClearRenderTarget(false, true, Color.black);
        commands.SetViewProjectionMatrices(targetCamera.worldToCameraMatrix, targetCamera.projectionMatrix);

        foreach (Renderer targetRenderer in phaseTwoScenarioRenderers)
        {
            if (targetRenderer == null || !targetRenderer.enabled || !targetRenderer.gameObject.activeInHierarchy)
                continue;

            int materialCount = Mathf.Max(1, targetRenderer.sharedMaterials.Length);
            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
                commands.DrawRenderer(targetRenderer, exclusionMaskMaterial, materialIndex, 0);
        }

        Graphics.ExecuteCommandBuffer(commands);
        CommandBufferPool.Release(commands);
        return mask;
    }

    private RenderTexture BuildExclusionMask(int width, int height)
    {
        if (exclusionMaskMaterial == null || targetCamera == null)
            return null;

        if (Time.unscaledTime >= nextParticleRefreshTime)
            RefreshParticleRenderers();

        exclusionRenderers.Clear();
        if (particleRenderers != null)
        {
            foreach (ParticleSystemRenderer particleRenderer in particleRenderers)
                if (particleRenderer != null && particleRenderer.enabled && particleRenderer.gameObject.activeInHierarchy)
                    exclusionRenderers.Add(particleRenderer);
        }

        if (taggedCloudRenderers != null)
        {
            foreach (Renderer cloudRenderer in taggedCloudRenderers)
                if (cloudRenderer != null && cloudRenderer.enabled && cloudRenderer.gameObject.activeInHierarchy)
                    exclusionRenderers.Add(cloudRenderer);
        }

        if (phaseOneCubeRenderers != null)
        {
            foreach (Renderer cubeRenderer in phaseOneCubeRenderers)
                if (cubeRenderer != null && cubeRenderer.enabled && cubeRenderer.gameObject.activeInHierarchy)
                    exclusionRenderers.Add(cubeRenderer);
        }

        AddRenderersToExclusion(mathBlockRenderers);
        AddRenderersToExclusion(countdownTimerRenderers);

        foreach (DistanceFogBlurExclude exclusion in DistanceFogBlurExclude.ActiveExclusions)
        {
            if (exclusion == null || !exclusion.isActiveAndEnabled)
                continue;

            foreach (Renderer targetRenderer in exclusion.Renderers)
                if (targetRenderer != null && targetRenderer.enabled && targetRenderer.gameObject.activeInHierarchy)
                    exclusionRenderers.Add(targetRenderer);
        }

        if (gravityInteract == null)
            gravityInteract = FindFirstObjectByType<GravityInteract>();

        if (gravityInteract != null && gravityInteract.IsHoldingObject)
        {
            Renderer[] heldRenderers = gravityInteract.GrabbedObject.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer heldRenderer in heldRenderers)
                if (heldRenderer != null)
                    exclusionRenderers.Add(heldRenderer);
        }

        // R8 pode ser convertido pela Unity para R8_SRGB, formato ausente em algumas GPUs/Android.
        // ARGB32 Linear é amplamente suportado e evita o fallback (e o warning) a cada frame.
        RenderTexture mask = RenderTexture.GetTemporary(
            width,
            height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);
        mask.filterMode = FilterMode.Bilinear;

        CommandBuffer commands = CommandBufferPool.Get("MathBlock Blur Exclusions");
        commands.SetRenderTarget(mask);
        commands.ClearRenderTarget(false, true, Color.black);
        commands.SetViewProjectionMatrices(targetCamera.worldToCameraMatrix, targetCamera.projectionMatrix);

        foreach (Renderer targetRenderer in exclusionRenderers)
        {
            int materialCount = Mathf.Max(1, targetRenderer.sharedMaterials.Length);
            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
                commands.DrawRenderer(targetRenderer, exclusionMaskMaterial, materialIndex, 0);
        }

        Graphics.ExecuteCommandBuffer(commands);
        CommandBufferPool.Release(commands);
        return mask;
    }

    private void AddRenderersToExclusion(Renderer[] renderers)
    {
        if (renderers == null)
            return;

        foreach (Renderer targetRenderer in renderers)
            if (targetRenderer != null && targetRenderer.enabled && targetRenderer.gameObject.activeInHierarchy)
                exclusionRenderers.Add(targetRenderer);
    }

private void RefreshParticleRenderers()
    {
        // Particulas transparentes devem receber o fog do pos-processamento.
        // Exclusoes continuam sendo opt-in via DistanceFogBlurExclude.
        particleRenderers = null;

        List<Renderer> cloudRenderers = new List<Renderer>();
        GameObject[] taggedClouds = GameObject.FindGameObjectsWithTag("Cloud");
        foreach (GameObject cloud in taggedClouds)
            cloudRenderers.AddRange(cloud.GetComponentsInChildren<Renderer>(true));
        taggedCloudRenderers = cloudRenderers.ToArray();

        List<Renderer> cubeRenderers = new List<Renderer>();
        Renderer[] sceneRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Renderer candidate in sceneRenderers)
        {
            if (candidate != null &&
                candidate.gameObject.scene == gameObject.scene &&
                MobileSceneToonMaterials.IsPhaseOneCube(candidate))
            {
                cubeRenderers.Add(candidate);
            }
        }
        phaseOneCubeRenderers = cubeRenderers.ToArray();

        List<Renderer> blockRenderers = new List<Renderer>();
        MathBlockValue[] blocks = FindObjectsByType<MathBlockValue>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (MathBlockValue block in blocks)
            if (block != null && block.gameObject.scene == gameObject.scene)
                blockRenderers.AddRange(block.GetComponentsInChildren<Renderer>(true));
        mathBlockRenderers = blockRenderers.ToArray();

        List<Renderer> timerRenderers = new List<Renderer>();
        CountdownTimer[] timers = FindObjectsByType<CountdownTimer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (CountdownTimer timer in timers)
            if (timer != null && timer.gameObject.scene == gameObject.scene)
                timerRenderers.AddRange(timer.GetComponentsInChildren<Renderer>(true));
        countdownTimerRenderers = timerRenderers.ToArray();

        List<Renderer> phaseTwoRenderers = new List<Renderer>();
        foreach (Renderer candidate in sceneRenderers)
        {
            if (MobileSceneToonMaterials.BelongsToPhaseTwoScenarioGeometry(candidate)
                && MobileSceneToonMaterials.IsGroundSurface(candidate))
                phaseTwoRenderers.Add(candidate);
        }
        phaseTwoScenarioRenderers = phaseTwoRenderers.ToArray();

        nextParticleRefreshTime = Time.unscaledTime + 2f;
    }

    private void OnDestroy()
    {
        if (material != null)
            DestroyImmediate(material);
        if (exclusionMaskMaterial != null)
            DestroyImmediate(exclusionMaskMaterial);
    }
}
