using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class ShineOperator : MonoBehaviour
{
    [Header("Tecla e brilho")]
    [SerializeField] private Key particleKey = Key.F;
    [SerializeField] private Color particleColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField] private Sprite particleSprite;

    [Header("Desbloqueio")]
    [SerializeField] private GravityInteract.PencilOperator operatorType;

    [Header("Particulas")]
    [SerializeField, Min(1)] private int particlesPerPress = 10;
    [SerializeField, Min(1)] private int maxParticles = 30;
    [SerializeField, Min(0.1f)] private float particleLifetime = 0.8f;
    [SerializeField, Min(0f)] private float particleSpeed = 65f;
    [SerializeField, Min(1f)] private float particleSize = 16f;
    [SerializeField, Min(0f), Tooltip("Raio da area, em unidades do Canvas, onde as particulas podem surgir.")]
    private float particleSpawnRadius;

    private readonly List<UiParticle> particlePool = new List<UiParticle>();
    private RectTransform iconRect;
    private RectTransform canvasRect;
    private Canvas rootCanvas;
    private Sprite generatedSprite;
    private Texture2D generatedTexture;
    private int nextParticleIndex;

    private void Awake()
    {
        iconRect = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        rootCanvas = canvas != null ? canvas.rootCanvas : null;
        canvasRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;

        if (rootCanvas == null || canvasRect == null)
            Debug.LogError($"{name}: ShineOperator precisa estar em um icone dentro de um Canvas.", this);

    }

    private void Start()
    {
        if (operatorType == GravityInteract.PencilOperator.None)
            Debug.LogError($"{name}: selecione o tipo de operador no ShineOperator.", this);
    }

    private void OnEnable()
    {
        OperatorsScript.OperatorEquipped += HandleOperatorEquipped;
    }

    private void Update()
    {
        if (!IsOperatorUnlocked())
        {
            DeactivateParticles();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && particleKey != Key.None && keyboard[particleKey].wasPressedThisFrame)
            EmitBurst();

        AnimateParticles(Time.unscaledDeltaTime);
    }

    private void OnDisable()
    {
        OperatorsScript.OperatorEquipped -= HandleOperatorEquipped;
        DeactivateParticles();
    }

    private void HandleOperatorEquipped(GravityInteract.PencilOperator equippedOperator)
    {
        if (equippedOperator == operatorType && IsOperatorUnlocked())
            EmitBurst();
    }

    private void DeactivateParticles()
    {
        for (int i = 0; i < particlePool.Count; i++)
            particlePool[i].gameObject.SetActive(false);
    }

    private bool IsOperatorUnlocked()
    {
        OperatorsScript controller = OperatorsScript.Instance;
        return operatorType != GravityInteract.PencilOperator.None
            && controller != null
            && controller.IsOperatorUnlocked(operatorType);
    }

    private void EmitBurst()
    {
        if (rootCanvas == null || canvasRect == null)
            return;

        Vector3 iconWorldPosition = iconRect.TransformPoint(iconRect.rect.center);
        Camera canvasCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : rootCanvas.worldCamera;
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(canvasCamera, iconWorldPosition);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                canvasCamera,
                out Vector2 canvasPosition))
        {
            return;
        }

        int poolLimit = Mathf.Max(1, maxParticles);
        int burstSize = Mathf.Clamp(particlesPerPress, 1, poolLimit);
        for (int i = 0; i < burstSize; i++)
        {
            UiParticle particle = GetParticle(poolLimit);
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            particle.rect.anchoredPosition = canvasPosition + Random.insideUnitCircle * particleSpawnRadius;
            particle.rect.sizeDelta = Vector2.one * particleSize;
            particle.rect.localRotation = Quaternion.identity;
            particle.image.sprite = particleSprite != null ? particleSprite : GetGeneratedSprite();
            particle.image.color = particleColor;
            particle.image.enabled = true;
            particle.gameObject.SetActive(true);
            particle.transform.SetAsLastSibling();

            particle.velocity = direction * particleSpeed * Random.Range(0.65f, 1.25f);
            particle.angularSpeed = Random.Range(-180f, 180f);
            particle.lifetime = Mathf.Max(0.1f, particleLifetime) * Random.Range(0.75f, 1.25f);
            particle.age = 0f;
        }
    }

    private UiParticle GetParticle(int poolLimit)
    {
        if (particlePool.Count < poolLimit)
        {
            GameObject particleObject = new GameObject(
                "HUD Shine Particle",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            particleObject.transform.SetParent(rootCanvas.transform, false);

            Image image = particleObject.GetComponent<Image>();
            image.raycastTarget = false;
            UiParticle particle = new UiParticle(particleObject, image);
            particle.rect.anchorMin = Vector2.one * 0.5f;
            particle.rect.anchorMax = Vector2.one * 0.5f;
            particle.rect.pivot = Vector2.one * 0.5f;
            particlePool.Add(particle);
            return particle;
        }

        UiParticle reusedParticle = particlePool[nextParticleIndex];
        nextParticleIndex = (nextParticleIndex + 1) % particlePool.Count;
        return reusedParticle;
    }

    private void AnimateParticles(float deltaTime)
    {
        for (int i = 0; i < particlePool.Count; i++)
        {
            UiParticle particle = particlePool[i];
            if (!particle.gameObject.activeSelf)
                continue;

            particle.age += deltaTime;
            float progress = Mathf.Clamp01(particle.age / particle.lifetime);
            if (progress >= 1f)
            {
                particle.gameObject.SetActive(false);
                continue;
            }

            particle.rect.anchoredPosition += particle.velocity * deltaTime;
            particle.rect.Rotate(0f, 0f, particle.angularSpeed * deltaTime);
            Color color = particleColor;
            color.a *= 1f - progress;
            particle.image.color = color;
        }
    }

    private Sprite GetGeneratedSprite()
    {
        if (generatedSprite != null)
            return generatedSprite;

        const int textureSize = 32;
        generatedTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name = "Runtime HUD Sparkle",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[textureSize * textureSize];
        Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        float halfSize = textureSize * 0.5f;
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                Vector2 offset = new Vector2(x, y) - center;
                float distance = offset.magnitude / halfSize;
                float angle = Mathf.Atan2(offset.y, offset.x);
                float spike = 0.5f + 0.5f * Mathf.Cos(angle * 4f);
                float outerRadius = Mathf.Lerp(0.28f, 0.5f, Mathf.Pow(spike, 8f));
                float alpha = Mathf.Clamp01((outerRadius - distance) * 32f);
                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        generatedTexture.SetPixels(pixels);
        generatedTexture.Apply(false, true);
        generatedSprite = Sprite.Create(
            generatedTexture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f));
        generatedSprite.name = "Runtime HUD Sparkle";
        generatedSprite.hideFlags = HideFlags.HideAndDontSave;
        return generatedSprite;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < particlePool.Count; i++)
        {
            if (particlePool[i].gameObject != null)
                Destroy(particlePool[i].gameObject);
        }

        if (generatedSprite != null)
            Destroy(generatedSprite);
        if (generatedTexture != null)
            Destroy(generatedTexture);
    }

    private sealed class UiParticle
    {
        public readonly GameObject gameObject;
        public readonly RectTransform rect;
        public readonly Image image;
        public Vector2 velocity;
        public float angularSpeed;
        public float lifetime;
        public float age;

        public Transform transform => gameObject.transform;

        public UiParticle(GameObject gameObject, Image image)
        {
            this.gameObject = gameObject;
            rect = gameObject.GetComponent<RectTransform>();
            this.image = image;
        }
    }
}
