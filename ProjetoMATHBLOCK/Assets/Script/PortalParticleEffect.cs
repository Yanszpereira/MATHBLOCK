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

    private static readonly Gradient PortalColorGradient = CreatePortalColorGradient();

    private static Gradient CreatePortalColorGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.08f, 0.42f, 1f), 0f),
                new GradientColorKey(Color.white, 0.25f),
                new GradientColorKey(new Color(0.48f, 0.52f, 0.58f), 0.5f),
                new GradientColorKey(Color.white, 0.75f),
                new GradientColorKey(new Color(0.08f, 0.42f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        return gradient;
    }

    [Header("Área do portal")]
    [SerializeField]
    [Tooltip("Plano 2D usado para desenhar o portal dentro do BoxCollider.")]
    private PortalPlane plane = PortalPlane.XY;

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
    private ParticleSystem particleSystem;
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
    }

    private void Awake()
    {
        CacheComponents();
        ConfigureParticleSystems();
    }

    private void OnEnable()
    {
        if (particleSystems == null)
            return;

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

        if (particleSystem == null)
            particleSystem = GetComponent<ParticleSystem>();
    }

    private void ConfigureParticleSystems()
    {
        if (particleSystem == null)
            return;

        particleSystems = new ParticleSystem[LightTextureCount];
        particleRenderers = new ParticleSystemRenderer[LightTextureCount];
        particleBuffers = new ParticleSystem.Particle[LightTextureCount][];
        particleCounts = new int[LightTextureCount];

        particleSystems[0] = particleSystem;

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
        Shader shader = Shader.Find("Particles/Additive");

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

        int bufferSize = Mathf.Max(1, maxParticles);

        if (particleBuffers == null || particleBuffers.Length != LightTextureCount)
            particleBuffers = new ParticleSystem.Particle[LightTextureCount][];

        for (int i = 0; i < LightTextureCount; i++)
        {
            if (particleBuffers[i] == null || particleBuffers[i].Length != bufferSize)
                particleBuffers[i] = new ParticleSystem.Particle[bufferSize];

            ParticleSystem.MainModule main = particleSystems[i].main;
            main.maxParticles = bufferSize;
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

        Vector2 edgeOffset = GetRandomPointOnRectangleBorder(halfSize);
        float size = Mathf.Max(0.001f, particleSize + Random.Range(-particleSizeVariation, particleSizeVariation));

        return new PortalParticle
        {
            startOffset = edgeOffset,
            age = 0f,
            lifetime = particleLifetime * Random.Range(0.8f, 1.2f),
            size = size,
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
            Vector2 rotatedOffset = Rotate(portalParticle.startOffset * contraction, angle);

            ParticleSystem.Particle particle = particleBuffers[lightIndex][particleIndex];
            particle.position = center + axisU * rotatedOffset.x + axisV * rotatedOffset.y;
            particle.startColor = PortalColorGradient.Evaluate(progress);
            particle.startSize = portalParticle.size;
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
