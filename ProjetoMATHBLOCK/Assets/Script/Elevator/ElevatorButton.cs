using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public enum ElevatorButtonRole
{
    Platform,
    CallInitial,
    CallFinal
}

/// <summary>
/// Receives the shared Player/Operators action (E) and forwards interaction
/// only when the player's camera is looking at this button.
/// </summary>
public sealed class ElevatorButton : MonoBehaviour
{
    [SerializeField] private ElevatorButtonRole role;
    [SerializeField] private float interactionDistance = 4f;
    [SerializeField] private float pressDistance = 0.08f;
    [SerializeField] private float pressDuration = 0.06f;
    [SerializeField] private float releaseDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float appearDuration = 0.22f;
    [SerializeField, Range(0.01f, 0.5f)] private float appearStartScale = 0.04f;
    [SerializeField] private Texture2D appearParticleTexture;

    private ElevatorTotemController controller;
    private InputAction interactAction;
    private Camera playerCamera;
    private Transform buttonPart;
    private Vector3 buttonRestLocalPosition;
    private Vector3 restLocalScale;
    private Renderer[] fadeRenderers;
    private Material[][] originalSharedMaterials;
    private Coroutine pressAnimation;
    private Coroutine appearAnimation;
    private BlockResizeParticleEffect appearParticleEffect;
    private bool hasCachedRestScale;

    public ElevatorButtonRole Role => role;

    public void Configure(ElevatorTotemController owner, ElevatorButtonRole buttonRole)
    {
        controller = owner;
        role = buttonRole;
    }

    private void Start()
    {
        CacheButtonPart();

        PlayerInput playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput == null)
        {
            Debug.LogWarning($"Botao de elevador {name} nao encontrou PlayerInput.");
            return;
        }

        interactAction = playerInput.actions.FindAction("Player/Operators", false)
            ?? playerInput.actions.FindAction("Operators", false);
        playerCamera = Camera.main;

        if (interactAction == null)
        {
            Debug.LogWarning($"Botao de elevador {name} nao encontrou a acao Player/Operators.");
            return;
        }

        interactAction.performed += OnInteractPerformed;
    }

    private void OnEnable()
    {
        CacheButtonPart();
    }

    private void OnDestroy()
    {
        if (interactAction != null)
            interactAction.performed -= OnInteractPerformed;

        StopAppearAnimation();
    }

    public void PlayAppearAnimation()
    {
        CacheButtonPart();
        CacheFadeRenderers();

        if (!hasCachedRestScale)
        {
            restLocalScale = transform.localScale;
            hasCachedRestScale = true;
        }

        StopAppearAnimation();
        transform.localScale = restLocalScale * appearStartScale;
        SetFadeAlpha(0f);
        appearParticleEffect = BlockResizeParticleEffect.CreateForTransform(
            transform,
            appearParticleTexture,
            Color.white);
        appearAnimation = StartCoroutine(AppearRoutine());
    }

    public void StopAppearAnimation()
    {
        if (appearAnimation != null)
        {
            StopCoroutine(appearAnimation);
            appearAnimation = null;
        }

        if (appearParticleEffect != null)
        {
            appearParticleEffect.StopAndFadeOut();
            appearParticleEffect = null;
        }

        if (hasCachedRestScale)
            transform.localScale = restLocalScale;

        SetFadeAlpha(1f);
        RestoreOriginalMaterials();
    }

    private IEnumerator AppearRoutine()
    {
        float elapsed = 0f;

        while (elapsed < appearDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / appearDuration);
            t = t * t * (3f - 2f * t);
            transform.localScale = Vector3.LerpUnclamped(
                restLocalScale * appearStartScale,
                restLocalScale,
                t);
            SetFadeAlpha(t);
            yield return null;
        }

        transform.localScale = restLocalScale;
        SetFadeAlpha(1f);

        if (appearParticleEffect != null)
        {
            appearParticleEffect.StopAndFadeOut();
            appearParticleEffect = null;
        }

        appearAnimation = null;
    }

    private void CacheFadeRenderers()
    {
        if (fadeRenderers == null)
            fadeRenderers = GetComponentsInChildren<Renderer>(true);

        if (originalSharedMaterials != null)
            return;

        originalSharedMaterials = new Material[fadeRenderers.Length][];

        for (int index = 0; index < fadeRenderers.Length; index++)
        {
            Renderer renderer = fadeRenderers[index];
            if (renderer == null)
                continue;

            originalSharedMaterials[index] = renderer.sharedMaterials;

            // Use per-instance materials so the fade does not affect other buttons.
            Material[] materials = renderer.materials;
            renderer.materials = materials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                ConfigureTransparentMaterial(materials[materialIndex]);
        }
    }

    private void RestoreOriginalMaterials()
    {
        if (originalSharedMaterials == null)
            return;

        for (int index = 0; index < fadeRenderers.Length; index++)
        {
            Renderer renderer = fadeRenderers[index];
            Material[] originals = originalSharedMaterials[index];
            if (renderer == null || originals == null)
                continue;

            renderer.sharedMaterials = originals;
        }

        originalSharedMaterials = null;
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeRenderers == null)
            return;

        for (int index = 0; index < fadeRenderers.Length; index++)
        {
            Renderer renderer = fadeRenderers[index];
            if (renderer == null)
                continue;

            Material[] materials = renderer.materials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                    continue;

                if (material.HasProperty("_Color"))
                {
                    Color color = material.color;
                    color.a = alpha;
                    material.color = color;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    Color color = material.GetColor("_BaseColor");
                    color.a = alpha;
                    material.SetColor("_BaseColor", color);
                }
            }
        }
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 3f);
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (controller == null || !isActiveAndEnabled)
            return;

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (playerCamera == null)
            return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            interactionDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            if (!IsHitByThisButton(hits[i].collider.transform))
                continue;

            Debug.Log($"Botao de elevador {name} acionado com E ({role}).");
            PlayPressAnimation();
            controller.HandleButtonPress(role);
            return;
        }
    }

    private void CacheButtonPart()
    {
        if (buttonPart != null)
            return;

        buttonPart = FindChildRecursive(transform, "botao")
            ?? FindChildRecursive(transform, "Cylinder.001");

        if (buttonPart != null)
            buttonRestLocalPosition = buttonPart.localPosition;
        else
            Debug.LogWarning($"Botao de elevador {name} nao encontrou a parte vermelha 'botao'.");
    }

    private void PlayPressAnimation()
    {
        CacheButtonPart();
        if (buttonPart == null)
            return;

        if (pressAnimation != null)
            StopCoroutine(pressAnimation);

        pressAnimation = StartCoroutine(PressButtonRoutine());
    }

    private IEnumerator PressButtonRoutine()
    {
        Vector3 start = buttonPart.localPosition;
        Vector3 pressed = buttonRestLocalPosition - Vector3.up * pressDistance;

        yield return MoveButton(start, pressed, pressDuration);
        yield return MoveButton(pressed, buttonRestLocalPosition, releaseDuration);

        buttonPart.localPosition = buttonRestLocalPosition;
        pressAnimation = null;
    }

    private IEnumerator MoveButton(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            buttonPart.localPosition = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            buttonPart.localPosition = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        buttonPart.localPosition = to;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private bool IsHitByThisButton(Transform hitTransform)
    {
        return hitTransform == transform || hitTransform.IsChildOf(transform);
    }
}
