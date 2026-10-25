using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(ParticleSystem))]
public sealed class PortalParticleEffect : MonoBehaviour
{
    private const int LightTextureCount = 3;

    public enum PortalPlane
    {
        XY,
        XZ,
        YZ
    }

    private static readonly Gradient PortalColorGradient = CriarPaleta();
    private static Gradient CriarPaleta()
    {
        var gradiente = new Gradient();
        gradiente.SetKeys(new[] {
            new GradientColorKey(new Color(0.42f, 0.18f, 1f), 0f),
            new GradientColorKey(Color.white, 0.20f),
            new GradientColorKey(new Color(0.12f, 0.42f, 1f), 0.40f),
            new GradientColorKey(Color.white, 0.58f),
            new GradientColorKey(Color.white, 0.82f),
            new GradientColorKey(new Color(0.50f, 0.22f, 1f), 1f)
        }, new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0.5f, 1f) });
        return gradiente;
    }

    private static readonly HashSet<PortalParticleEffect> portaisAtivos = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparPortais() => portaisAtivos.Clear();

    [Header("Tremor por proximidade")]
    [SerializeField, Min(0.1f)] private float raioTremor = 4.5f;
    [SerializeField] private DoorOpener portaFinal;
    public static float IntensidadeProxima(Vector3 posicao)
    {
        float intensidade = 0f;
        foreach (var portal in portaisAtivos)
        {
            if (portal == null || !portal.isActiveAndEnabled || portal.portaFinal == null || !portal.portaFinal.IsFullyOpen) continue;
            float distancia = Vector3.Distance(posicao, portal.CentroInteriorMundo);
            intensidade = Mathf.Max(intensidade, Mathf.SmoothStep(0f, 1f,
                1f - Mathf.InverseLerp(0.8f, portal.raioTremor, distancia)));
        }
        return intensidade;
    }

    [Header("Área do portal")]
    [SerializeField]
    [Tooltip("Plano 2D usado para desenhar o portal dentro do BoxCollider.")]
    private PortalPlane plane = PortalPlane.XY;

    [Header("Interior da porta (sem alterar o collider de transicao)")]
    [SerializeField] private Vector3 centroInterior;
    [SerializeField] private Vector2 tamanhoInterior;
    [SerializeField] private float deslocamentoFundo = 0.035f;
    private Mesh malhaFundo;
    private GameObject fundo;

    public void ConfigurarInterior(Vector3 centro, Vector2 tamanho, PortalPlane plano, float sentidoNormal, DoorOpener porta)
    {
        centroInterior = centro;
        tamanhoInterior = tamanho;
        plane = plano;
        deslocamentoFundo = Mathf.Sign(sentidoNormal) * 0.035f;
        portaFinal = porta;
    }

    public Vector3 CentroInteriorMundo
    {
        get { GetPlaneAxes(out Vector3 centro, out _, out _, out _); return transform.TransformPoint(centro); }
    }

    [Header("Partículas")]
    [SerializeField, Min(1)]
    private int maxParticles = 1800;

    [SerializeField, Min(0f)]
    private float emissionRate = 600f;

    [SerializeField, Min(0.05f)]
    private float particleLifetime = 2.8f;

    [SerializeField, Min(0.001f)]
    private float particleSize = 0.1375f;

    [SerializeField, Min(0f)]
    private float particleSizeVariation = 0.025f;

    [SerializeField, Min(0f)]
    private float spiralRevolutions = 1.35f;

    [SerializeField]
    private bool clockwise = true;

    [Header("Sprites de luz")]
    [SerializeField]
    [Tooltip("Light_01 até Light_03. São preenchidos automaticamente no Editor.")]
    private Texture2D[] lightTextures = new Texture2D[LightTextureCount];

    [SerializeField]
    [Tooltip("Opcional. O material será clonado para cada textura de luz.")]
    private Material particleMaterial;

    [Header("Gizmos")]
    [SerializeField]
    private Color gizmoColor = new Color(0.1f, 0.65f, 1f, 0.9f);

    private readonly List<PortalParticle> particles = new List<PortalParticle>();
    private readonly List<Material> runtimeMaterials = new List<Material>();

    private BoxCollider portalCollider;
    private ParticleSystem particulasPrincipais;
    private ParticleSystem[] particleSystems;
    private ParticleSystemRenderer[] particleRenderers;
    private ParticleSystem.Particle[][] particleBuffers;
    private int[] particleCounts;
    private float emissionAccumulator;

    private struct PortalParticle
    {
        public Vector2 startOffset;
        public float age;
        public float lifetime;
        public float size;
        public int lightIndex;
        public bool rajada;
    }

    private void Awake()
    {
        CacheComponents();
        ConfigureParticleSystems();
        CriarFundoRetangular();
        int aquecimento = Mathf.Min(maxParticles, Mathf.CeilToInt(emissionRate * particleLifetime));
        for (int i = 0; i < aquecimento; i++)
        {
            PortalParticle particula = CreateParticle();
            particula.age = Random.value * particula.lifetime;
            particles.Add(particula);
        }
    }

    private void OnEnable()
    {
        portaisAtivos.Add(this);
        if (particleSystems == null)
            return;

        if (fundo != null) fundo.SetActive(true);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null)
                continue;

            particleSystems[i].Play();
            particleSystems[i].Pause();
        }
    }

    private void Update()
    {
        if (portalCollider == null || particleSystems == null)
            return;

        EnsureParticleBuffers();
        SpawnParticles();
        UpdateParticles();
        RenderParticles();
    }

    private void OnDisable()
    {
        portaisAtivos.Remove(this);
        if (fundo != null) fundo.SetActive(false);
        particles.Clear();
        emissionAccumulator = 0f;

        if (particleSystems == null)
            return;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] != null)
                particleSystems[i].Clear();
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < runtimeMaterials.Count; i++)
        {
            if (runtimeMaterials[i] != null)
                Destroy(runtimeMaterials[i]);
        }

        runtimeMaterials.Clear();
        if (malhaFundo != null) Destroy(malhaFundo);
    }

    private void CriarFundoRetangular()
    {
        Shader shader = Resources.Load<Shader>("Portal/PortalEspiralRetangular");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogError("Shader do interior do portal indisponivel.", this);
            return;
        }
        GetPlaneAxes(out Vector3 centro, out Vector3 eixoU, out Vector3 eixoV, out Vector2 metade);
        Vector3 normal = Vector3.Cross(eixoU, eixoV);
        centro += normal * deslocamentoFundo;
        malhaFundo = new Mesh { name = "Interior retangular do portal" };
        malhaFundo.vertices = new[] {
            centro - eixoU * metade.x - eixoV * metade.y, centro + eixoU * metade.x - eixoV * metade.y,
            centro + eixoU * metade.x + eixoV * metade.y, centro - eixoU * metade.x + eixoV * metade.y };
        malhaFundo.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        malhaFundo.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        malhaFundo.RecalculateNormals();
        malhaFundo.RecalculateBounds();
        fundo = new GameObject("Interior espiral - opaco");
        fundo.layer = gameObject.layer;
        fundo.transform.SetParent(transform, false);
        fundo.AddComponent<MeshFilter>().sharedMesh = malhaFundo;
        MeshRenderer renderizador = fundo.AddComponent<MeshRenderer>();
        var material = new Material(shader) { name = "Portal branco, roxo e azul", hideFlags = HideFlags.HideAndDontSave };
        renderizador.sharedMaterial = material;
        renderizador.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderizador.receiveShadows = false;
        renderizador.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderizador.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        runtimeMaterials.Add(material);
        fundo.AddComponent<DistanceFogBlurExclude>();
    }

    private void Reset()
    {
        CacheComponents();

        if (portalCollider != null)
            portalCollider.isTrigger = true;
    }

    private void OnValidate()
    {
        maxParticles = Mathf.Max(1, maxParticles);
        emissionRate = Mathf.Max(0f, emissionRate);
        particleLifetime = Mathf.Max(0.05f, particleLifetime);
        particleSize = Mathf.Max(0.001f, particleSize);
        particleSizeVariation = Mathf.Max(0f, particleSizeVariation);
        spiralRevolutions = Mathf.Max(0f, spiralRevolutions);

        if (!Application.isPlaying)
        {
            AutoAssignLightTextures();
            CacheComponents();

            if (portalCollider != null)
                portalCollider.isTrigger = true;
        }
    }

    private void CacheComponents()
    {
        if (portalCollider == null)
            portalCollider = GetComponent<BoxCollider>();

        if (particulasPrincipais == null)
            particulasPrincipais = GetComponent<ParticleSystem>();
    }

    private void ConfigureParticleSystems()
    {
        if (particulasPrincipais == null)
            return;

        particleSystems = new ParticleSystem[LightTextureCount];
        particleRenderers = new ParticleSystemRenderer[LightTextureCount];
        particleBuffers = new ParticleSystem.Particle[LightTextureCount][];
        particleCounts = new int[LightTextureCount];

        particleSystems[0] = particulasPrincipais;

        for (int i = 1; i < LightTextureCount; i++)
            particleSystems[i] = GetOrCreateParticleSystem(i);

        for (int i = 0; i < LightTextureCount; i++)
            ConfigureParticleSystem(particleSystems[i], i);

        EnsureParticleBuffers();

        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystems[i].Play();
            particleSystems[i].Pause();
        }
    }

    private ParticleSystem GetOrCreateParticleSystem(int lightIndex)
    {
        string childName = $"Portal Light {lightIndex + 1:00}";
        Transform childTransform = transform.Find(childName);

        if (childTransform == null)
        {
            GameObject childObject = new GameObject(childName);
            childTransform = childObject.transform;
            childTransform.SetParent(transform, false);
        }

        ParticleSystem childParticleSystem = childTransform.GetComponent<ParticleSystem>();

        if (childParticleSystem == null)
            childParticleSystem = childTransform.gameObject.AddComponent<ParticleSystem>();

        return childParticleSystem;
    }

    private void ConfigureParticleSystem(ParticleSystem target, int lightIndex)
    {
        if (target == null)
            return;

        ParticleSystem.MainModule main = target.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = maxParticles;
        main.startLifetime = particleLifetime;
        main.startSpeed = 0f;
        main.startSize = particleSize;
        main.startColor = Color.white;
        main.gravityModifier = 0f;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = target.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = target.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = target.GetComponent<ParticleSystemRenderer>();

        if (renderer == null)
            return;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.maxParticleSize = 0.5f;
        GetPlaneAxes(out Vector3 centro, out Vector3 eixoU, out Vector3 eixoV, out Vector2 metade);
        Vector3 tamanho = Abs(eixoU) * (metade.x * 2f + 0.5f) + Abs(eixoV) * (metade.y * 2f + 0.5f)
            + Abs(Vector3.Cross(eixoU, eixoV)) * 5f;
        renderer.localBounds = new Bounds(centro, tamanho);
        renderer.allowOcclusionWhenDynamic = false;

        Material material = CreateParticleMaterial(lightIndex);

        if (material != null)
            renderer.sharedMaterial = material;

        particleRenderers[lightIndex] = renderer;
    }

    private Material CreateParticleMaterial(int lightIndex)
    {
        Material material = particleMaterial != null
            ? new Material(particleMaterial)
            : CreateDefaultParticleMaterial();

        if (material == null)
            return null;

        Texture2D smokeTexture = GetLightTexture(lightIndex);

        if (smokeTexture != null)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", smokeTexture);

            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", smokeTexture);
        }

        material.name = $"Portal Light Material {lightIndex + 1:00}";
        runtimeMaterials.Add(material);
        return material;
    }

    private static Material CreateDefaultParticleMaterial()
    {
        Shader shader = Resources.Load<Shader>("Portal/ParticulasEnergia");

        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Additive");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
            return null;

        Material material = new Material(shader);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 1f);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", 1f);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", 1f);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.renderQueue = 3000;
        return material;
    }

    private static Vector3 Abs(Vector3 valor) => new Vector3(Mathf.Abs(valor.x), Mathf.Abs(valor.y), Mathf.Abs(valor.z));

    private Texture2D GetLightTexture(int lightIndex)
    {
        if (lightTextures == null ||
            lightIndex < 0 ||
            lightIndex >= lightTextures.Length)
        {
            return null;
        }

        return lightTextures[lightIndex];
    }

    private void EnsureParticleBuffers()
    {
        if (particleSystems == null)
            return;

        // A cena antiga reservava 50 mil posicoes por textura para cerca de 2 mil particulas vivas.
        int bufferSize = Mathf.Max(1, Mathf.Min(maxParticles, Mathf.CeilToInt(emissionRate * particleLifetime * 1.2f) + 1));

        if (particleBuffers == null || particleBuffers.Length != LightTextureCount)
            particleBuffers = new ParticleSystem.Particle[LightTextureCount][];

        for (int i = 0; i < LightTextureCount; i++)
        {
            if (particleBuffers[i] == null || particleBuffers[i].Length != bufferSize)
                particleBuffers[i] = new ParticleSystem.Particle[bufferSize];

            ParticleSystem.MainModule main = particleSystems[i].main;
            if (main.maxParticles != bufferSize) main.maxParticles = bufferSize;
        }

        if (particleCounts == null || particleCounts.Length != LightTextureCount)
            particleCounts = new int[LightTextureCount];
    }

    private void SpawnParticles()
    {
        if (emissionRate <= 0f || particles.Count >= maxParticles)
            return;

        emissionAccumulator += emissionRate * Time.deltaTime;

        int requestedCount = Mathf.FloorToInt(emissionAccumulator);
        int availableSlots = maxParticles - particles.Count;
        int spawnCount = Mathf.Min(requestedCount, availableSlots);

        for (int i = 0; i < spawnCount; i++)
            particles.Add(CreateParticle());

        emissionAccumulator -= spawnCount;

        if (particles.Count >= maxParticles)
            emissionAccumulator = Mathf.Min(emissionAccumulator, 1f);
    }

    private PortalParticle CreateParticle()
    {
        GetPlaneAxes(out _, out _, out _, out Vector2 halfSize);

        Vector2 startOffset = GetRandomPointOnRectangleBorder(halfSize);
        float size = Mathf.Max(0.001f, particleSize + Random.Range(-particleSizeVariation, particleSizeVariation));
        // Rajadas adicionais. A espiral e as texturas originais continuam intactas.
        bool rajada = portaFinal != null && portaFinal.IsFullyOpen &&
            Mathf.Repeat(Time.time, 1.1f) < 0.18f && Random.value < 0.7f;

        return new PortalParticle
        {
            startOffset = startOffset,
            age = 0f,
            lifetime = rajada ? Random.Range(0.8f, 1.3f) : particleLifetime * Random.Range(0.8f, 1.2f),
            size = size,
            rajada = rajada,
            lightIndex = Random.Range(0, LightTextureCount)
        };
    }

    private void UpdateParticles()
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            PortalParticle particle = particles[i];
            particle.age += Time.deltaTime;

            if (particle.age >= particle.lifetime)
            {
                particles.RemoveAt(i);
                continue;
            }

            particles[i] = particle;
        }
    }

    private void RenderParticles()
    {
        GetPlaneAxes(out Vector3 center, out Vector3 axisU, out Vector3 axisV, out _);
        Vector3 normal = Vector3.Cross(axisU, axisV);
        float escalaNormal = Mathf.Max(0.01f, transform.TransformVector(normal).magnitude);
        System.Array.Clear(particleCounts, 0, particleCounts.Length);

        for (int i = 0; i < particles.Count; i++)
        {
            PortalParticle portalParticle = particles[i];
            int lightIndex = Mathf.Clamp(portalParticle.lightIndex, 0, LightTextureCount - 1);
            int particleIndex = particleCounts[lightIndex];

            if (particleIndex >= particleBuffers[lightIndex].Length)
                continue;

            float progress = Mathf.Clamp01(portalParticle.age / portalParticle.lifetime);
            float contraction = 1f - progress;
            float direction = clockwise ? -1f : 1f;
            float angle = direction * progress * spiralRevolutions * Mathf.PI * 2f;
            // Trajetoria original: a rotacao livre deixa a energia vazar pela moldura.
            Vector2 rotatedOffset = Rotate(portalParticle.startOffset * contraction, angle);
            float profundidade = 0f;
            if (portalParticle.rajada)
            {
                rotatedOffset = Rotate(portalParticle.startOffset * (0.35f + progress * 0.85f), angle);
                profundidade = -Mathf.Sign(deslocamentoFundo) * (0.1f + progress * 2f) / escalaNormal;
            }

            ParticleSystem.Particle particle = particleBuffers[lightIndex][particleIndex];
            particle.position = center + axisU * rotatedOffset.x + axisV * rotatedOffset.y + normal * profundidade;
            Color cor = PortalColorGradient.Evaluate(progress);
            if (portalParticle.rajada) cor.a *= 1f - progress;
            particle.startColor = cor;
            particle.startSize = portalParticle.size * (portalParticle.rajada ? 1f + progress * 0.65f : 1f);
            particle.startLifetime = portalParticle.lifetime;
            particle.remainingLifetime = portalParticle.lifetime - portalParticle.age;
            particle.rotation3D = Vector3.zero;

            particleBuffers[lightIndex][particleIndex] = particle;
            particleCounts[lightIndex]++;
        }

        for (int i = 0; i < LightTextureCount; i++)
            particleSystems[i].SetParticles(particleBuffers[i], particleCounts[i]);
    }

    private Vector2 GetRandomPointOnRectangleBorder(Vector2 halfSize)
    {
        float width = Mathf.Max(0.001f, halfSize.x * 2f);
        float height = Mathf.Max(0.001f, halfSize.y * 2f);
        float perimeter = 2f * (width + height);
        float distance = Random.Range(0f, perimeter);

        if (distance < width)
            return new Vector2(-halfSize.x + distance, -halfSize.y);

        distance -= width;

        if (distance < height)
            return new Vector2(halfSize.x, -halfSize.y + distance);

        distance -= height;

        if (distance < width)
            return new Vector2(halfSize.x - distance, halfSize.y);

        distance -= width;
        return new Vector2(-halfSize.x, halfSize.y - distance);
    }

    private static Vector2 Rotate(Vector2 value, float angle)
    {
        float cosine = Mathf.Cos(angle);
        float sine = Mathf.Sin(angle);

        return new Vector2(
            value.x * cosine - value.y * sine,
            value.x * sine + value.y * cosine);
    }

    private void GetPlaneAxes(
        out Vector3 center,
        out Vector3 axisU,
        out Vector3 axisV,
        out Vector2 halfSize)
    {
        center = portalCollider != null ? portalCollider.center : Vector3.zero;

        switch (plane)
        {
            case PortalPlane.XZ:
                axisU = Vector3.right;
                axisV = Vector3.forward;
                halfSize = portalCollider != null
                    ? new Vector2(portalCollider.size.x, portalCollider.size.z) * 0.5f
                    : Vector2.one * 0.5f;
                break;

            case PortalPlane.YZ:
                axisU = Vector3.up;
                axisV = Vector3.forward;
                halfSize = portalCollider != null
                    ? new Vector2(portalCollider.size.y, portalCollider.size.z) * 0.5f
                    : Vector2.one * 0.5f;
                break;

            default:
                axisU = Vector3.right;
                axisV = Vector3.up;
                halfSize = portalCollider != null
                    ? new Vector2(portalCollider.size.x, portalCollider.size.y) * 0.5f
                    : Vector2.one * 0.5f;
                break;
        }
        if (tamanhoInterior.x > 0f && tamanhoInterior.y > 0f)
        {
            center = centroInterior;
            halfSize = tamanhoInterior * 0.5f;
        }
    }

#if UNITY_EDITOR
    private void AutoAssignLightTextures()
    {
        if (lightTextures == null || lightTextures.Length != LightTextureCount)
            lightTextures = new Texture2D[LightTextureCount];

        for (int i = 0; i < LightTextureCount; i++)
        {
            if (lightTextures[i] != null)
                continue;

            string assetName = $"light_{i + 1:00}";
            string[] guids = AssetDatabase.FindAssets($"{assetName} t:Texture2D");

            for (int guidIndex = 0; guidIndex < guids.Length; guidIndex++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[guidIndex]).Replace("\\", "/");

                if (!assetPath.ToLowerInvariant().Contains("/particle samples/sprites/"))
                    continue;

                lightTextures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                break;
            }
        }
    }
#else
    private void AutoAssignLightTextures()
    {
    }
#endif

    private void OnDrawGizmosSelected()
    {
        BoxCollider collider = GetComponent<BoxCollider>();

        if (collider == null)
            return;

        Vector3 gizmoSize = collider.size;

        switch (plane)
        {
            case PortalPlane.XZ:
                gizmoSize.y = 0.01f;
                break;

            case PortalPlane.YZ:
                gizmoSize.x = 0.01f;
                break;

            default:
                gizmoSize.z = 0.01f;
                break;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(collider.center, gizmoSize);
        Gizmos.DrawWireSphere(collider.center, 0.06f);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
