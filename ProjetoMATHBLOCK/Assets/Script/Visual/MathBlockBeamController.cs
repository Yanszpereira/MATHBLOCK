using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(LineRenderer))]
public sealed class MathBlockBeamController : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private GravityInteract gravityInteract;
    [SerializeField] private Transform beamOrigin;
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private ParticleSystem beamParticles;
    [SerializeField] private ParticleSystem tipGlowParticles;

    [Header("Curva")]
    [SerializeField, Range(8, 16)] private int pointCount = 12;
    [SerializeField, Min(0f)] private float curveOffset = 0.35f;

    [Header("Largura")]
    [SerializeField, Min(0f)] private float originWidth = 0.07f;
    [SerializeField, Min(0f)] private float targetWidth = 0.04f;

    [Header("Cor sem operador")]
    [SerializeField] private Color noOperatorColor = new Color(0.82f, 0.84f, 0.88f, 1f);

    [Header("Entrada do raio")]
    [SerializeField, Min(0.01f)] private float beamRevealDuration = 0.5f;

    [Header("Partículas do raio")]
    [SerializeField, Min(0f)] private float particlesPerSecond = 24f;
    [SerializeField, Min(0.01f)] private float particleLifetime = 0.6f;
    [SerializeField, Min(0f)] private float particleRadius = 0.08f;
    [SerializeField, Min(0.001f)] private float particleSize = 0.035f;

    [Header("Brilho na ponta do lápis")]
    [SerializeField, Min(0.001f)] private float tipGlowSize = 0.16f;

    private Vector3[] points;
    private Transform cachedHeldBlock;
    private ResizableBlock cachedResizableBlock;
    private MathBlockValue cachedMathBlockValue;
    private BoxCollider cachedCollider;
    private Renderer cachedRenderer;
    private PencilTipOperatorColor operatorColorSource;
    private GravityInteract.PencilOperator cachedOperator;
    private bool hasAppliedOperatorColor;
    private bool isBeamActive;
    private float beamRevealElapsed;
    private float particleEmissionRemainder;
    private ParticleSystem.Particle[] particleBuffer;
    private readonly ParticleSystem.Particle[] tipGlowParticleBuffer =
        new ParticleSystem.Particle[1];
    private ParticleSystemRenderer particleRenderer;
    private MaterialPropertyBlock particleRendererProperties;
    private Texture2D tipGlowTexture;

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (beamParticles == null)
            beamParticles = GetComponent<ParticleSystem>();
        if (beamParticles == null)
            beamParticles = gameObject.AddComponent<ParticleSystem>();

        if (tipGlowParticles == null)
        {
            GameObject glowObject = new GameObject("BeamTipGlow");
            glowObject.transform.SetParent(transform, false);
            tipGlowParticles = glowObject.AddComponent<ParticleSystem>();
        }

        pointCount = Mathf.Clamp(pointCount, 8, 16);
        points = new Vector3[pointCount];

        ConfigureParticleSystem();
        ConfigureTipGlowParticleSystem();

        if (lineRenderer == null)
            return;

        lineRenderer.positionCount = pointCount;
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.generateLightingData = false;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.numCapVertices = 3;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.widthMultiplier = originWidth;
        lineRenderer.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, originWidth > 0f ? targetWidth / originWidth : 0f));
        lineRenderer.enabled = false;
    }

    private void Start()
    {
        if (gravityInteract != null)
            operatorColorSource = gravityInteract.GetComponent<PencilTipOperatorColor>();

        if (gravityInteract == null)
            Debug.LogWarning("MathBlockBeamController sem GravityInteract configurado.", this);
        if (beamOrigin == null)
            Debug.LogWarning("MathBlockBeamController sem BeamOrigin configurado.", this);
        if (lineRenderer == null)
            Debug.LogWarning("MathBlockBeamController sem LineRenderer configurado.", this);
        if (operatorColorSource == null)
            Debug.LogWarning("MathBlockBeamController não encontrou PencilTipOperatorColor no PencilGun.", this);
    }

    private void LateUpdate()
    {
        if (lineRenderer == null || gravityInteract == null || beamOrigin == null)
        {
            DisableBeam();
            return;
        }

        Transform heldBlock = gravityInteract.HeldBlock;
        GravityInteract.PencilOperator currentOperator = gravityInteract.EquippedOperator;

        if (heldBlock == null)
        {
            DisableBeam();
            return;
        }

        if (heldBlock != cachedHeldBlock)
        {
            CacheHeldBlock(heldBlock);
            RestartBeamReveal();
            hasAppliedOperatorColor = false;
        }

        if (!isBeamActive)
            RestartBeamReveal();

        if (!hasAppliedOperatorColor || currentOperator != cachedOperator)
        {
            cachedOperator = currentOperator;
            Color color;
            if (currentOperator == GravityInteract.PencilOperator.None)
            {
                color = noOperatorColor;
            }
            else if (operatorColorSource == null ||
                     !operatorColorSource.TryGetOperatorColor(currentOperator, out color))
            {
                DisableBeam();
                return;
            }

            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
            hasAppliedOperatorColor = true;
        }

        cachedMathBlockValue?.SetHeldOutline(lineRenderer.startColor);

        Vector3 start = beamOrigin.position;
        Vector3 end = GetHeldBlockCenter();
        Vector3 midpoint = (start + end) * 0.5f;
        Vector3 control = midpoint + beamOrigin.forward * curveOffset;
        float duration = Mathf.Max(0.01f, beamRevealDuration);
        beamRevealElapsed = Mathf.Min(beamRevealElapsed + Time.deltaTime, duration);
        float revealProgress = Mathf.Clamp01(beamRevealElapsed / duration);

        for (int index = 0; index < points.Length; index++)
        {
            float t = revealProgress * index / (float)(points.Length - 1);
            float inverseT = 1f - t;
            points[index] = inverseT * inverseT * start
                + 2f * inverseT * t * control
                + t * t * end;
        }

        lineRenderer.SetPositions(points);
        lineRenderer.enabled = true;
        isBeamActive = true;
        EmitBeamParticles();
        ApplyBeamColorToParticles();
        UpdateTipGlow(start, lineRenderer.startColor);
    }

    private void ConfigureParticleSystem()
    {
        ParticleSystem.MainModule main = beamParticles.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = Color.white;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = beamParticles.colorOverLifetime;
        colorOverLifetime.enabled = false;

        ParticleSystem.EmissionModule emission = beamParticles.emission;
        emission.enabled = false;

        particleRenderer = beamParticles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            if (lineRenderer != null && lineRenderer.sharedMaterial != null)
                particleRenderer.sharedMaterial = lineRenderer.sharedMaterial;

            particleRendererProperties = new MaterialPropertyBlock();
            particleRenderer.GetPropertyBlock(particleRendererProperties);
            SetNeutralMaterialTint("_BaseColor");
            SetNeutralMaterialTint("_Color");
            SetNeutralMaterialTint("_TintColor");
            particleRenderer.SetPropertyBlock(particleRendererProperties);
        }

        particleBuffer = new ParticleSystem.Particle[Mathf.Max(64, main.maxParticles)];
        beamParticles.Play();
    }

    private void ConfigureTipGlowParticleSystem()
    {
        tipGlowTexture = CreateTipGlowTexture();

        ParticleSystem.MainModule main = tipGlowParticles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.maxParticles = 1;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = Color.white;

        ParticleSystem.EmissionModule emission = tipGlowParticles.emission;
        emission.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            tipGlowParticles.colorOverLifetime;
        colorOverLifetime.enabled = false;

        ParticleSystemRenderer renderer = tipGlowParticles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            if (lineRenderer.sharedMaterial != null)
                renderer.sharedMaterial = lineRenderer.sharedMaterial;

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_Color", Color.white);
            properties.SetColor("_BaseColor", Color.white);
            if (tipGlowTexture != null)
                properties.SetTexture("_MainTex", tipGlowTexture);
            renderer.SetPropertyBlock(properties);
        }

        tipGlowParticles.Play();
    }

    private static Texture2D CreateTipGlowTexture()
    {
        const int textureSize = 64;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "Runtime Beam Tip Glow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float normalizedX = (x + 0.5f) / textureSize * 2f - 1f;
                float normalizedY = (y + 0.5f) / textureSize * 2f - 1f;
                float distance = Mathf.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, distance);
                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private void UpdateTipGlow(Vector3 position, Color color)
    {
        int particleCount = tipGlowParticles.particleCount;
        if (particleCount == 0)
        {
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                startLifetime = 100000f,
                startSize = tipGlowSize,
                startColor = color
            };
            tipGlowParticles.Emit(emitParams, 1);
            return;
        }

        int aliveCount = tipGlowParticles.GetParticles(tipGlowParticleBuffer);
        for (int index = 0; index < aliveCount; index++)
        {
            tipGlowParticleBuffer[index].position = position;
            tipGlowParticleBuffer[index].startColor = color;
            tipGlowParticleBuffer[index].startSize = tipGlowSize;
        }
        tipGlowParticles.SetParticles(tipGlowParticleBuffer, aliveCount);
    }

    private void SetNeutralMaterialTint(string propertyName)
    {
        if (particleRenderer.sharedMaterial != null &&
            particleRenderer.sharedMaterial.HasProperty(propertyName))
        {
            particleRendererProperties.SetColor(propertyName, Color.white);
        }
    }

    private void ApplyBeamColorToParticles()
    {
        int particleCount = beamParticles.GetParticles(particleBuffer);
        Color beamColor = lineRenderer.startColor;
        for (int index = 0; index < particleCount; index++)
            particleBuffer[index].startColor = beamColor;

        beamParticles.SetParticles(particleBuffer, particleCount);
    }

    private void EmitBeamParticles()
    {
        particleEmissionRemainder += Mathf.Max(0f, particlesPerSecond) * Time.deltaTime;
        int particleCount = Mathf.FloorToInt(particleEmissionRemainder);
        particleEmissionRemainder -= particleCount;

        for (int index = 0; index < particleCount; index++)
        {
            int segment = Random.Range(0, points.Length - 1);
            Vector3 segmentStart = points[segment];
            Vector3 segmentDirection = points[segment + 1] - segmentStart;
            Vector3 tangent = segmentDirection.sqrMagnitude > 0f
                ? segmentDirection.normalized
                : Vector3.zero;
            Vector3 perpendicular = Vector3.Cross(tangent, Vector3.up);
            if (perpendicular.sqrMagnitude < 0.001f)
                perpendicular = Vector3.Cross(tangent, Vector3.right);
            perpendicular.Normalize();
            Vector3 secondPerpendicular = Vector3.Cross(tangent, perpendicular).normalized;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 radialOffset = (perpendicular * Mathf.Cos(angle)
                + secondPerpendicular * Mathf.Sin(angle)) * particleRadius;

            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = Vector3.Lerp(segmentStart, points[segment + 1], Random.value)
                    + radialOffset,
                startLifetime = particleLifetime,
                startSize = particleSize,
                startColor = lineRenderer.startColor
            };
            beamParticles.Emit(emitParams, 1);
        }
    }

    private void CacheHeldBlock(Transform heldBlock)
    {
        ClearCachedHeldBlockOutline();
        cachedHeldBlock = heldBlock;
        cachedMathBlockValue = heldBlock != null
            ? heldBlock.GetComponent<MathBlockValue>()
            : null;
        cachedResizableBlock = heldBlock != null
            ? heldBlock.GetComponent<ResizableBlock>()
            : null;
        cachedCollider = heldBlock != null
            ? heldBlock.GetComponent<BoxCollider>()
            : null;
        cachedRenderer = heldBlock != null
            ? heldBlock.GetComponentInChildren<Renderer>(true)
            : null;
    }

    private Vector3 GetHeldBlockCenter()
    {
        if (cachedResizableBlock != null)
            return cachedResizableBlock.WorldCenter;

        if (cachedCollider != null)
            return cachedCollider.bounds.center;

        if (cachedRenderer != null)
            return cachedRenderer.bounds.center;

        return cachedHeldBlock != null ? cachedHeldBlock.position : transform.position;
    }

    private void DisableBeam()
    {
        ClearCachedHeldBlockOutline();

        if (lineRenderer != null)
            lineRenderer.enabled = false;

        isBeamActive = false;
        beamRevealElapsed = 0f;
        cachedHeldBlock = null;
        cachedMathBlockValue = null;
        cachedResizableBlock = null;
        cachedCollider = null;
        cachedRenderer = null;
        hasAppliedOperatorColor = false;
        particleEmissionRemainder = 0f;
        if (beamParticles != null)
            beamParticles.Clear();
        if (tipGlowParticles != null)
            tipGlowParticles.Clear();
    }

    private void ClearCachedHeldBlockOutline()
    {
        if (cachedMathBlockValue != null)
            cachedMathBlockValue.ClearHeldOutline();

        cachedMathBlockValue = null;
    }

    private void RestartBeamReveal()
    {
        isBeamActive = false;
        beamRevealElapsed = 0f;
    }

    private void OnDisable()
    {
        DisableBeam();
    }

    private void OnDestroy()
    {
        if (tipGlowTexture != null)
            Destroy(tipGlowTexture);
    }
}
