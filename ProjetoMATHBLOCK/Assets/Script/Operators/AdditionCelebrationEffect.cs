using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Explosão luminosa executada quando dois blocos são somados.</summary>
public sealed class AdditionCelebrationEffect : MonoBehaviour
{
    private const float Lifetime = 2.2f;
    private Transform target;
    private Material particleMaterial;

    public static void Play(MathBlockValue block)
    {
        if (block == null) return;
        GameObject effectObject = new GameObject("Addition Glow Celebration");
        effectObject.transform.position = block.transform.position;
        AdditionCelebrationEffect effect = effectObject.AddComponent<AdditionCelebrationEffect>();
        effect.target = block.transform;
        effect.Build(block);
    }

    private void Build(MathBlockValue block)
    {
        Color blockColor = new Color(0.2f, 0.65f, 1f, 1f);
        block.TryGetVisualColor(out blockColor);
        Color glowColor = Color.Lerp(blockColor, Color.white, 0.30f);
        particleMaterial = CreateGlowMaterial();

        CreateBurst("Addition Sparks", glowColor, 42, 0.075f, 0.85f, 2.4f, ParticleSystemShapeType.Sphere);
        CreateBurst("Addition Halo", Color.Lerp(glowColor, Color.cyan, 0.35f), 18, 0.13f, 1.15f, 1.25f, ParticleSystemShapeType.Circle);
        CreateRisingSymbols(glowColor);
        StartCoroutine(Animate());
    }

    private void CreateBurst(string objectName, Color color, int count, float size, float life, float speed, ParticleSystemShapeType shapeType)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        ParticleSystem particles = child.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.15f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.65f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = count + 8;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = shapeType;
        shape.radius = 0.42f;
        shape.radiusThickness = shapeType == ParticleSystemShapeType.Circle ? 0f : 1f;

        ParticleSystem.ColorOverLifetimeModule colorLife = particles.colorOverLifetime;
        colorLife.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 0.22f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0f, 1f) });
        colorLife.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeLife = particles.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.15f, 1f, 1f));
        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.32f;
        noise.frequency = 1.4f;
        noise.scrollSpeed = 0.7f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = particleMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        particles.Play();
    }

    private void CreateRisingSymbols(Color color)
    {
        GameObject child = new GameObject("Rising Plus Symbols");
        child.transform.SetParent(transform, false);
        ParticleSystem particles = child.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.35f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.08f;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.08f, (short)12) });
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = Vector3.one * 0.7f;
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.y = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.material = particleMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        particles.Play();
    }

    private IEnumerator Animate()
    {
        float elapsed = 0f;
        while (elapsed < Lifetime)
        {
            elapsed += Time.deltaTime;
            if (target != null) transform.position = target.position;
            transform.rotation = Quaternion.Euler(0f, elapsed * 95f, elapsed * 32f);
            yield return null;
        }
        if (particleMaterial != null)
        {
            Texture texture = particleMaterial.mainTexture;
            Destroy(particleMaterial);
            if (texture != null) Destroy(texture);
        }
        Destroy(gameObject);
    }

    private static Material CreateGlowMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        Material material = new Material(shader) { name = "Addition Glow Particles", hideFlags = HideFlags.HideAndDontSave };
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.One);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        Texture2D glowTexture = CreateGlowTexture();
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", glowTexture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", glowTexture);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    private static Texture2D CreateGlowTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Addition Soft Particle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[size * size];
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
            float alpha = Mathf.Clamp01(1f - distance);
            alpha = alpha * alpha * (3f - 2f * alpha);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
