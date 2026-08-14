using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FMODUnity;

public class OperatorsScript : MonoBehaviour
{
    [Header("Interação")]
    public Transform playerVision;
    public float grabDistance = 10f;
    public GravityInteract pencilGun;

    [SerializeField] private Transform operatorAbsorbTarget;

    [Header("HUD Icons")]
    public Image additionIcon;
    public Image subtractionIcon;
    public Image multiplicationIcon;
    public Image divisionIcon;

    private const float equippedAlpha = 1f;
    private const float unequippedAlpha = 0.7f;

    [Header("Sons FMOD dos operadores")]
    [SerializeField] private bool playOperatorSelectionSounds = true;

    [SerializeField] private EventReference additionSound;
    [SerializeField] private EventReference subtractionSound;
    [SerializeField] private EventReference multiplicationSound;
    [SerializeField] private EventReference divisionSound;

    [Header("Controle de repetição")]
    [SerializeField] private float selectionSoundCooldown = 0.15f;

    private opItem equippedSceneOperator;
    private float lastSelectionSoundTime = -999f;
    private InputAction interactOperatorAction;
    private int lastInteractionFrame = -1;

    private void Awake()
    {
        ResolveReferences();
        ResolveInputAction();
        SetAllIconsAlpha(unequippedAlpha);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResolveInputAction();
        if (interactOperatorAction != null)
        {
            interactOperatorAction.performed += OnInteractOperatorEvent;
        }
    }

    private void OnDisable()
    {
        if (interactOperatorAction != null)
        {
            interactOperatorAction.performed -= OnInteractOperatorEvent;
        }
    }

    private void Update()
    {
        // Mantém o esquema original da Fase 1: interação direta pela tecla E.
        // O bloqueio por frame em TryInteractWithOperator evita chamada duplicada
        // quando o PlayerInput também dispara o evento Operators.
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            TryInteractWithOperator();
    }

    public void OnInteractOperatorEvent(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        TryInteractWithOperator();
    }

    private void TryInteractWithOperator()
    {
        if (lastInteractionFrame == Time.frameCount)
            return;

        lastInteractionFrame = Time.frameCount;
        ResolveReferences();

        if (pencilGun == null)
        {
            Debug.LogWarning("OperatorsScript sem referencia para GravityInteract.");
            return;
        }

        if (playerVision == null)
        {
            Debug.LogWarning("OperatorsScript sem referencia para playerVision.");
            return;
        }

        if (!TryGetLookedAtOperator(out opItem item))
            return;

        if (equippedSceneOperator != null && equippedSceneOperator != item)
        {
            equippedSceneOperator.RestoreToScene();
        }

        pencilGun.SetEquippedOperator(item.operatorType);

        PlaySelectionSound(item.operatorType, item.transform.position);

        item.ConsumeFromScene(GetAbsorbTarget());

        equippedSceneOperator = item;
        UpdateHudIcons(item.operatorType);
    }

    private void ResolveReferences()
    {
        if (pencilGun == null)
        {
            pencilGun = GetComponentInParent<GravityInteract>();

            if (pencilGun == null)
                pencilGun = FindFirstObjectByType<GravityInteract>();
        }

        if (playerVision == null && pencilGun != null)
        {
            playerVision = pencilGun.interactionCamera;
        }

        if (playerVision == null && Camera.main != null)
        {
            playerVision = Camera.main.transform;
        }

        ResolveHudIcons();
    }

    // Na Fase 2 este componente é adicionado em runtime e não recebe as
    // referências do Inspector. Localiza os indicadores no Canvas da cena.
    private void ResolveHudIcons()
    {
        if (additionIcon != null
            && subtractionIcon != null
            && multiplicationIcon != null
            && divisionIcon != null)
        {
            return;
        }

        Image[] sceneImages = FindObjectsByType<Image>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        Dictionary<string, Image> iconsByName = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        foreach (Image image in sceneImages)
        {
            if (image != null && !iconsByName.ContainsKey(image.name))
                iconsByName.Add(image.name, image);
        }

        additionIcon ??= FindIcon(iconsByName, "Soma", "Adicao", "Adição");
        subtractionIcon ??= FindIcon(iconsByName, "Subtracao", "Subtração");
        multiplicationIcon ??= FindIcon(iconsByName, "Multiplicacao", "Multiplicação");
        divisionIcon ??= FindIcon(iconsByName, "Divisao", "Divisão");
    }

    private static Image FindIcon(Dictionary<string, Image> iconsByName, params string[] names)
    {
        foreach (string iconName in names)
        {
            if (iconsByName.TryGetValue(iconName, out Image icon))
                return icon;
        }

        return null;
    }

    private void ResolveInputAction()
    {
        if (interactOperatorAction != null)
            return;

        PlayerInput playerInput = pencilGun != null
            ? pencilGun.GetComponentInParent<PlayerInput>()
            : null;

        if (playerInput == null)
            playerInput = FindFirstObjectByType<PlayerInput>();

        if (playerInput != null && playerInput.actions != null)
            interactOperatorAction = playerInput.actions.FindAction("Operators", throwIfNotFound: false);
    }

    private bool TryGetLookedAtOperator(out opItem item)
    {
        item = null;

        RaycastHit[] hits = Physics.RaycastAll(
            playerVision.position,
            playerVision.forward,
            grabDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        float nearestBlockingDistance = float.PositiveInfinity;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            Transform hitTransform = hit.collider.transform;

            // Ignore apenas os colliders do jogador. O objeto que possui este
            // script também é o contêiner dos operadores em várias cenas; ignorar
            // seus filhos fazia o raycast descartar todos os itens selecionáveis.
            if (IsPlayerCollider(hitTransform))
                continue;

            item = ResolveTaggedOperator(hit.collider, hit.point);
            if (item != null)
            {
                // Alguns pedestais da Fase 1 possuem collider imediatamente à frente
                // do operador. Aceita o item somente quando ele está junto do bloqueio,
                // evitando selecionar operadores através de paredes.
                if (float.IsPositiveInfinity(nearestBlockingDistance) ||
                    hit.distance <= nearestBlockingDistance + 1.25f)
                {
                    return true;
                }

                item = null;
                return false;
            }

            if (!hit.collider.isTrigger)
                nearestBlockingDistance = Mathf.Min(nearestBlockingDistance, hit.distance);
        }

        return false;
    }

    private bool IsPlayerCollider(Transform hitTransform)
    {
        if (hitTransform == null || pencilGun == null)
            return false;

        Transform playerRoot = pencilGun.transform.root;
        return hitTransform == playerRoot || hitTransform.IsChildOf(playerRoot);
    }

    private opItem ResolveTaggedOperator(Collider hitCollider, Vector3 hitPoint)
    {
        if (hitCollider == null)
            return null;

        opItem directItem = hitCollider.GetComponentInParent<opItem>();
        if (directItem != null && IsInOperatorHierarchy(directItem.transform))
            return directItem;

        Transform taggedRoot = hitCollider.transform;
        while (taggedRoot != null && !taggedRoot.CompareTag("Operator"))
            taggedRoot = taggedRoot.parent;

        if (taggedRoot == null)
            return null;

        opItem[] candidates = taggedRoot.GetComponentsInChildren<opItem>(true);
        opItem closest = null;
        float closestDistance = float.PositiveInfinity;

        foreach (opItem candidate in candidates)
        {
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                continue;

            float distance = Vector3.SqrMagnitude(candidate.transform.position - hitPoint);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = candidate;
            }
        }

        return closest;
    }

    private static bool IsInOperatorHierarchy(Transform target)
    {
        while (target != null)
        {
            if (target.CompareTag("Operator"))
                return true;

            target = target.parent;
        }

        return false;
    }

    private void PlaySelectionSound(GravityInteract.PencilOperator operatorType, Vector3 soundPosition)
    {
        if (!playOperatorSelectionSounds)
            return;

        if (Time.time < lastSelectionSoundTime + selectionSoundCooldown)
            return;

        EventReference selectedSound = GetSoundForOperator(operatorType);

        if (selectedSound.IsNull)
        {
            string fallbackEventPath = GetFallbackEventPath(operatorType);
            if (string.IsNullOrEmpty(fallbackEventPath))
                return;

            lastSelectionSoundTime = Time.time;
            RuntimeManager.PlayOneShot(fallbackEventPath, soundPosition);
            return;
        }

        lastSelectionSoundTime = Time.time;

        RuntimeManager.PlayOneShot(selectedSound, soundPosition);
    }

    private static string GetFallbackEventPath(GravityInteract.PencilOperator operatorType)
    {
        switch (operatorType)
        {
            case GravityInteract.PencilOperator.Addition:
                return "event:/soma";

            case GravityInteract.PencilOperator.Subtraction:
                return "event:/subtracao";

            case GravityInteract.PencilOperator.Multiplication:
                return "event:/multiplication";

            case GravityInteract.PencilOperator.Division:
                return "event:/divisao";

            default:
                return null;
        }
    }

    private EventReference GetSoundForOperator(GravityInteract.PencilOperator operatorType)
    {
        switch (operatorType)
        {
            case GravityInteract.PencilOperator.Addition:
                return additionSound;

            case GravityInteract.PencilOperator.Subtraction:
                return subtractionSound;

            case GravityInteract.PencilOperator.Multiplication:
                return multiplicationSound;

            case GravityInteract.PencilOperator.Division:
                return divisionSound;

            default:
                return default;
        }
    }

    private void SetIconAlpha(Image img, float alpha)
    {
        if (img == null)
            return;

        Color c = img.color;
        c.a = Mathf.Clamp01(alpha);
        img.color = c;
    }

    private void SetAllIconsAlpha(float alpha)
    {
        SetIconAlpha(additionIcon, alpha);
        SetIconAlpha(subtractionIcon, alpha);
        SetIconAlpha(multiplicationIcon, alpha);
        SetIconAlpha(divisionIcon, alpha);
    }

    private void UpdateHudIcons(GravityInteract.PencilOperator equipped)
    {
        SetAllIconsAlpha(unequippedAlpha);

        switch (equipped)
        {
            case GravityInteract.PencilOperator.Addition:
                SetIconAlpha(additionIcon, equippedAlpha);
                break;

            case GravityInteract.PencilOperator.Subtraction:
                SetIconAlpha(subtractionIcon, equippedAlpha);
                break;

            case GravityInteract.PencilOperator.Multiplication:
                SetIconAlpha(multiplicationIcon, equippedAlpha);
                break;

            case GravityInteract.PencilOperator.Division:
                SetIconAlpha(divisionIcon, equippedAlpha);
                break;
        }
    }

    private Transform GetAbsorbTarget()
    {
        if (pencilGun != null)
        {
            operatorAbsorbTarget = pencilGun.GetOrCreateOperatorAbsorbTarget();
            return operatorAbsorbTarget;
        }

        if (operatorAbsorbTarget != null)
            return operatorAbsorbTarget;

        return null;
    }
}
