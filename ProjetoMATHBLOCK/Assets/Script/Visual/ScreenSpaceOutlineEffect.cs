using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Camera))]
public sealed class ScreenSpaceOutlineEffect : MonoBehaviour
{
    [SerializeField] private Color outlineColor = new Color(0.028f, 0.006f, 0.045f, 0.62f);
    [SerializeField, Range(0.5f, 8f)] private float thicknessPixels = 1f;
    [SerializeField, Range(1f, 2f)] private float espessuraBlocos = 1.3f;
    [SerializeField, Range(0.001f, 0.2f)] private float depthSensitivity = 0.025f;
    private Camera targetCamera;
    private Material outlineMaterial, maskMaterial;
    private GravityInteract gravityInteract;
    private readonly HashSet<Renderer> renderers = new HashSet<Renderer>();
    private readonly HashSet<Renderer> blocos = new HashSet<Renderer>();
    private readonly HashSet<Renderer> interioresPortais = new HashSet<Renderer>();
    private readonly Dictionary<Renderer, int> submalhas = new Dictionary<Renderer, int>();
    private readonly Plane[] planosVisiveis = new Plane[6];
    private float nextRefresh;
    private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int ThicknessId = Shader.PropertyToID("_OutlineThickness");
    private static readonly int SensitivityId = Shader.PropertyToID("_DepthSensitivity");
    private static readonly int MaskId = Shader.PropertyToID("_OutlineObjectMask");
    private static readonly int BlockMaskId = Shader.PropertyToID("_MathBlockOutlineMask");
    private static readonly int BlockThicknessId = Shader.PropertyToID("_MathBlockOutlineScale");
    private static readonly int PortalMaskId = Shader.PropertyToID("_PortalOutlineExclude");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install() => GlobalSceneBootstrap.Register(OnSceneLoaded, -50);

    private static void OnSceneLoaded(Scene scene)
    {
        if (!IsGameplayScene(scene)) return;
        Camera camera = Camera.main;
        if (camera != null && camera.GetComponent<ScreenSpaceOutlineEffect>() == null)
            camera.gameObject.AddComponent<ScreenSpaceOutlineEffect>();
    }

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetCamera.depthTextureMode |= DepthTextureMode.DepthNormals;
        Shader outline = Shader.Find("Hidden/MathBlock/ScreenSpaceOutline");
        Shader mask = Shader.Find("Hidden/MathBlock/ScreenSpaceOutlineMask");
        if (outline == null || mask == null || !outline.isSupported || !mask.isSupported)
        {
            Debug.LogError(
                $"Outline global indisponível. Shader principal: {DescribeShader(outline)}; máscara: {DescribeShader(mask)}.",
                this);
            enabled = false;
            return;
        }
        outlineMaterial = new Material(outline) { hideFlags = HideFlags.HideAndDontSave };
        maskMaterial = new Material(mask) { hideFlags = HideFlags.HideAndDontSave };
        RefreshRenderers();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!IsGameplayScene(SceneManager.GetActiveScene()) || outlineMaterial == null || maskMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }
        // Mantem contornos dos blocos em resolucao integral, inclusive no Android.
        RenderTexture mask = BuildMask(source.width, source.height);
        outlineMaterial.SetColor(ColorId, outlineColor);
        outlineMaterial.SetFloat(ThicknessId, thicknessPixels);
        outlineMaterial.SetFloat(BlockThicknessId, espessuraBlocos);
        outlineMaterial.SetFloat(SensitivityId, depthSensitivity);
        outlineMaterial.SetTexture(MaskId, mask);
        Graphics.Blit(source, destination, outlineMaterial);
        RenderTexture.ReleaseTemporary(mask);
    }

    private RenderTexture BuildMask(int width, int height)
    {
        if (Time.unscaledTime >= nextRefresh) RefreshRenderers();
        RenderTexture mask = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        mask.filterMode = FilterMode.Bilinear;
        CommandBuffer commands = CommandBufferPool.Get("MathBlock New Outline");
        commands.SetRenderTarget(mask); commands.ClearRenderTarget(false, true, Color.black);
        commands.SetViewProjectionMatrices(targetCamera.worldToCameraMatrix, targetCamera.projectionMatrix);
        commands.SetGlobalFloat(PortalMaskId, 0f);
        GeometryUtility.CalculateFrustumPlanes(targetCamera, planosVisiveis);
        Transform segurado = gravityInteract != null && gravityInteract.IsHoldingObject ? gravityInteract.GrabbedObject : null;
        foreach (Renderer target in renderers)
        {
            if (target == null || !target.enabled || !target.gameObject.activeInHierarchy ||
                !GeometryUtility.TestPlanesAABB(planosVisiveis, target.bounds) ||
                (segurado != null && (target.transform == segurado || target.transform.IsChildOf(segurado)))) continue;
            int count = submalhas[target];
            commands.SetGlobalFloat(BlockMaskId, blocos.Contains(target) ? 1f : 0f);
            for (int i = 0; i < count; i++) commands.DrawRenderer(target, maskMaterial, i, 0);
        }
        commands.SetGlobalFloat(BlockMaskId, 0f);
        // O interior luminoso nao recebe linhas de geometria que esta atras dele.
        // A moldura e os blocos na frente conservam seus contornos originais.
        commands.SetGlobalFloat(PortalMaskId, 1f);
        foreach (Renderer interior in interioresPortais)
            if (interior != null && interior.enabled && interior.gameObject.activeInHierarchy &&
                GeometryUtility.TestPlanesAABB(planosVisiveis, interior.bounds))
                commands.DrawRenderer(interior, maskMaterial, 0, 0);
        commands.SetGlobalFloat(PortalMaskId, 0f);
        Graphics.ExecuteCommandBuffer(commands); CommandBufferPool.Release(commands);
        return mask;
    }

    private void RefreshRenderers()
    {
        renderers.Clear();
        blocos.Clear();
        interioresPortais.Clear();
        submalhas.Clear();
        if (gravityInteract == null) gravityInteract = FindFirstObjectByType<GravityInteract>();
        foreach (Renderer target in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (target is MeshRenderer && target.gameObject.scene == targetCamera.gameObject.scene &&
                target.GetComponentInParent<PortalParticleEffect>() != null) interioresPortais.Add(target);
            if (CanOutline(target))
            {
                renderers.Add(target);
                submalhas[target] = Mathf.Max(1, target.sharedMaterials.Length);
                if (target.GetComponentInParent<MathBlockValue>() != null) blocos.Add(target);
            }
        }
        nextRefresh = Time.unscaledTime + 1f;
    }

    private bool CanOutline(Renderer target)
    {
        if (target == null || target.gameObject.scene != targetCamera.gameObject.scene || target is ParticleSystemRenderer ||
            target is TrailRenderer || target is LineRenderer || target.GetComponentInParent<Canvas>() != null ||
            target.GetComponentInParent<ParticleSystem>() != null ||
            target.GetComponent<TextMesh>() != null ||
            target.GetComponentInParent<ToonCloudSkyGenerator>() != null) return false;
        for (Transform current = target.transform; current != null; current = current.parent)
        {
            string n = current.name;
            if (n.Contains("Sky", System.StringComparison.OrdinalIgnoreCase) || n.Contains("Ceu", System.StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Céu", System.StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Cloud", System.StringComparison.OrdinalIgnoreCase) ||
                n.Contains("Nuvem", System.StringComparison.OrdinalIgnoreCase) ||
                current.CompareTag("Cloud")) return false;
        }
        return target.sharedMaterials != null && target.sharedMaterials.Length > 0;
    }

    private void OnDestroy()
    {
        if (outlineMaterial != null) DestroyImmediate(outlineMaterial);
        if (maskMaterial != null) DestroyImmediate(maskMaterial);
    }

    private static string DescribeShader(Shader shader)
    {
        return shader == null ? "não encontrado" : $"{shader.name} (suportado={shader.isSupported})";
    }

    private static bool IsGameplayScene(Scene scene)
    {
        return scene.IsValid() &&
               (scene.name.StartsWith("Fase", System.StringComparison.OrdinalIgnoreCase) ||
                scene.name.Equals("MainScene", System.StringComparison.OrdinalIgnoreCase));
    }
}
