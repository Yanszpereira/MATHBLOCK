using UnityEngine;

public class opItem : MonoBehaviour
{
    public GravityInteract.PencilOperator operatorType;

    [Header("Absorb Effect")]
    [SerializeField] private OperatorAbsorbEffect absorbEffectPrefab;
    [SerializeField] private OperatorAbsorbWaveEffect absorbWaveEffectPrefab;

    [Header("Visual Wobble")]
    [SerializeField] private bool useWobble = true;
    [SerializeField] private float wobbleAmplitude = 0.15f;
    [SerializeField] private float wobbleSpeed = 1.5f;

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float wobbleTimeOffset;
    private Renderer[] cachedRenderers;
    private Collider[] cachedColliders;
    private bool configuredUseWobble;

    private void Awake()
    {
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        configuredUseWobble = useWobble;
        wobbleTimeOffset = Random.Range(0f, Mathf.PI * 2f);
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
    }

    private void Update()
    {
        if (!useWobble)
            return;

        float verticalOffset = Mathf.Sin((Time.time * wobbleSpeed) + wobbleTimeOffset) * wobbleAmplitude;
        transform.position = originalPosition + Vector3.up * verticalOffset;
    }

    public void ConsumeFromScene(Transform absorbTarget)
    {
        SpawnAbsorbEffect(absorbTarget);
        SetScenePresence(false);
    }

    public void RestoreToScene()
    {
        transform.SetPositionAndRotation(originalPosition, originalRotation);
        SetScenePresence(true);
    }

    private void SpawnAbsorbEffect(Transform absorbTarget)
    {
        Color effectColor = GetAbsorbEffectColor();
        SpawnWaveEffect(effectColor);

        OperatorAbsorbEffect effect = null;
        if (absorbEffectPrefab != null)
        {
            effect = Instantiate(absorbEffectPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            GameObject effectObject = new GameObject("OperatorAbsorbEffect");
            effectObject.transform.position = transform.position;
            effectObject.AddComponent<ParticleSystem>();
            effect = effectObject.AddComponent<OperatorAbsorbEffect>();
        }

        effect.Init(absorbTarget, effectColor);
    }

    private void SpawnWaveEffect(Color effectColor)
    {
        OperatorAbsorbWaveEffect waveEffect = null;
        if (absorbWaveEffectPrefab != null)
        {
            waveEffect = Instantiate(absorbWaveEffectPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            GameObject waveObject = new GameObject("OperatorAbsorbWaveEffect");
            waveObject.transform.position = transform.position;
            waveObject.AddComponent<ParticleSystem>();
            waveEffect = waveObject.AddComponent<OperatorAbsorbWaveEffect>();
        }

        waveEffect.Init(effectColor);
    }

    private Color GetAbsorbEffectColor()
    {
        switch (operatorType)
        {
            case GravityInteract.PencilOperator.Addition:
                return new Color(0.15f, 1f, 0.25f);

            case GravityInteract.PencilOperator.Subtraction:
                return new Color(1f, 0.72f, 0.08f);

            case GravityInteract.PencilOperator.Multiplication:
                return new Color(0.1f, 0.45f, 1f);

            case GravityInteract.PencilOperator.Division:
                return new Color(1f, 0.08f, 0.06f);

            default:
                return Color.white;
        }
    }

    private void SetScenePresence(bool isVisible)
    {
        useWobble = isVisible && configuredUseWobble;

        if (cachedRenderers == null || cachedRenderers.Length == 0)
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (cachedColliders == null || cachedColliders.Length == 0)
        {
            cachedColliders = GetComponentsInChildren<Collider>(true);
        }

        foreach (Renderer targetRenderer in cachedRenderers)
        {
            if (targetRenderer != null)
            {
                targetRenderer.enabled = isVisible;
            }
        }

        foreach (Collider targetCollider in cachedColliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = isVisible;
            }
        }
    }
}
