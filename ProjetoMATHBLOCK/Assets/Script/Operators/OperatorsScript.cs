using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FMODUnity;

public class OperatorsScript : MonoBehaviour
{
    public static OperatorsScript Instance { get; private set; }
    public static event Action<GravityInteract.PencilOperator> OperatorEquipped;

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
    private InputAction selectAdditionAction;
    private InputAction selectSubtractionAction;
    private InputAction selectMultiplicationAction;
    private InputAction selectDivisionAction;
    private int lastInteractionFrame = -1;
    private bool lastInteractionSucceeded;
    private int unlockedOperatorMask;
    private readonly Dictionary<Image, Vector3> hudIconBaseScales = new();
    private static Sprite hudGlowSprite;
    private Coroutine hudZoomRoutine;

    private void Awake()
    {
        Instance = this;
        ResolveReferences();
        ResolveInputAction();
        SetAllIconsAlpha(unequippedAlpha);
        AnimateHudIconScales(GravityInteract.PencilOperator.None, true);
    }

    private void Start()
    {
        ResetOperatorAvailability();
    }

    private void OnEnable()
    {
        Instance = this;
        ResolveReferences();
        ResolveInputAction();
        if (interactOperatorAction != null)
        {
            interactOperatorAction.performed += OnInteractOperatorEvent;
        }

        SubscribeSelectionActions();
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;

        if (interactOperatorAction != null)
        {
            interactOperatorAction.performed -= OnInteractOperatorEvent;
        }

        UnsubscribeSelectionActions();
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

    public bool TryInteractWithOperatorFromUI()
    {
        return TryInteractWithOperator();
    }

    private void OnSelectAddition(InputAction.CallbackContext context)
    {
        if (context.performed)
            SelectOperator(GravityInteract.PencilOperator.Addition);
    }

    private void OnSelectSubtraction(InputAction.CallbackContext context)
    {
        if (context.performed)
            SelectOperator(GravityInteract.PencilOperator.Subtraction);
    }

    private void OnSelectMultiplication(InputAction.CallbackContext context)
    {
        if (context.performed)
            SelectOperator(GravityInteract.PencilOperator.Multiplication);
    }

    private void OnSelectDivision(InputAction.CallbackContext context)
    {
        if (context.performed)
            SelectOperator(GravityInteract.PencilOperator.Division);
    }

    public void SelectAdditionFromUI() => SelectOperator(GravityInteract.PencilOperator.Addition);
    public void SelectSubtractionFromUI() => SelectOperator(GravityInteract.PencilOperator.Subtraction);
    public void SelectMultiplicationFromUI() => SelectOperator(GravityInteract.PencilOperator.Multiplication);
    public void SelectDivisionFromUI() => SelectOperator(GravityInteract.PencilOperator.Division);

    public bool SelectOperator(GravityInteract.PencilOperator operatorType)
    {
        ResolveReferences();

        if (!IsOperatorUnlocked(operatorType))
        {
            Debug.Log($"Operador {operatorType} ainda nao foi equipado pelo jogador.", this);
            return false;
        }

        Vector3 soundPosition = playerVision != null
            ? playerVision.position
            : transform.position;

        return SelectOperator(operatorType, null, soundPosition);
    }

    public bool IsOperatorUnlocked(GravityInteract.PencilOperator operatorType)
    {
        if (operatorType == GravityInteract.PencilOperator.None)
            return false;

        int operatorBit = 1 << (int)operatorType;
        return (unlockedOperatorMask & operatorBit) != 0;
    }

    private bool TryInteractWithOperator()
    {
        if (lastInteractionFrame == Time.frameCount)
            return lastInteractionSucceeded;

        lastInteractionFrame = Time.frameCount;
        lastInteractionSucceeded = false;
        ResolveReferences();

        if (pencilGun == null)
        {
            Debug.LogWarning("OperatorsScript sem referencia para GravityInteract.");
            return false;
        }

        if (playerVision == null)
        {
            Debug.LogWarning("OperatorsScript sem referencia para playerVision.");
            return false;
        }

        if (!TryGetLookedAtOperator(out opItem item))
            return false;

        lastInteractionSucceeded = SelectOperator(item.operatorType, item, item.transform.position);
        return lastInteractionSucceeded;
    }

    private bool SelectOperator(
        GravityInteract.PencilOperator operatorType,
        opItem sceneOperator,
        Vector3 soundPosition)
    {
        if (pencilGun == null || operatorType == GravityInteract.PencilOperator.None)
            return false;

        // Interagir diretamente com o item da cena é o momento em que o
        // operador é obtido. Depois disso, os atalhos podem selecioná-lo.
        bool isSceneInteraction = sceneOperator != null;
        if (!isSceneInteraction && !IsOperatorUnlocked(operatorType))
        {
            OperatorAchievement.ShowLockedOperator();
            Debug.Log($"Operador bloqueado até ser coletado no cenário: {operatorType}.");
            return false;
        }

        bool isFirstUnlock = isSceneInteraction && !IsOperatorUnlocked(operatorType);
        if (isSceneInteraction)
            UnlockOperator(operatorType);

        if (isFirstUnlock)
            OperatorAchievement.ShowOperatorUnlocked(operatorType);

        if (equippedSceneOperator != null && equippedSceneOperator != sceneOperator)
            equippedSceneOperator.RestoreToScene();

        pencilGun.SetEquippedOperator(operatorType);
        OperatorEquipped?.Invoke(operatorType);
        PlaySelectionSound(operatorType, soundPosition);

        if (sceneOperator != null)
            sceneOperator.ConsumeFromScene(GetAbsorbTarget());

        equippedSceneOperator = sceneOperator;
        UpdateHudIcons(operatorType);
        Debug.Log($"Operador selecionado: {operatorType} ({(sceneOperator != null ? "cena" : "atalho")}).");
        return true;
    }

    public bool HasEquippedBefore(GravityInteract.PencilOperator operatorType)
    {
        return IsOperatorUnlocked(operatorType);
    }

    private void ResetOperatorAvailability()
    {
        unlockedOperatorMask = 0;
        equippedSceneOperator = null;

        if (pencilGun != null)
            pencilGun.ClearEquippedOperator();

        SetAllIconsAlpha(unequippedAlpha);
        AnimateHudIconScales(GravityInteract.PencilOperator.None);
    }

    private void UnlockOperator(GravityInteract.PencilOperator operatorType)
    {
        if (operatorType == GravityInteract.PencilOperator.None)
            return;

        unlockedOperatorMask |= 1 << (int)operatorType;
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
        PlayerInput playerInput = pencilGun != null
            ? pencilGun.GetComponentInParent<PlayerInput>()
            : null;

        if (playerInput == null)
            playerInput = FindFirstObjectByType<PlayerInput>();

        if (playerInput != null && playerInput.actions != null)
        {
            interactOperatorAction ??= playerInput.actions.FindAction("Operators", throwIfNotFound: false);
            selectAdditionAction ??= playerInput.actions.FindAction("SelectAddition", throwIfNotFound: false);
            selectSubtractionAction ??= playerInput.actions.FindAction("SelectSubtraction", throwIfNotFound: false);
            selectMultiplicationAction ??= playerInput.actions.FindAction("SelectMultiplication", throwIfNotFound: false);
            selectDivisionAction ??= playerInput.actions.FindAction("SelectDivision", throwIfNotFound: false);
        }
    }

    private void SubscribeSelectionActions()
    {
        if (selectAdditionAction != null)
            selectAdditionAction.performed += OnSelectAddition;
        if (selectSubtractionAction != null)
            selectSubtractionAction.performed += OnSelectSubtraction;
        if (selectMultiplicationAction != null)
            selectMultiplicationAction.performed += OnSelectMultiplication;
        if (selectDivisionAction != null)
            selectDivisionAction.performed += OnSelectDivision;
    }

    private void UnsubscribeSelectionActions()
    {
        if (selectAdditionAction != null)
            selectAdditionAction.performed -= OnSelectAddition;
        if (selectSubtractionAction != null)
            selectSubtractionAction.performed -= OnSelectSubtraction;
        if (selectMultiplicationAction != null)
            selectMultiplicationAction.performed -= OnSelectMultiplication;
        if (selectDivisionAction != null)
            selectDivisionAction.performed -= OnSelectDivision;
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
        SetIconSelected(additionIcon, false, "Addition Glow");
        SetIconSelected(subtractionIcon, false, "Subtraction Glow");
        SetIconSelected(multiplicationIcon, false, "Multiplication Glow");
        SetIconSelected(divisionIcon, false, "Division Glow");
    }

    private void UpdateHudIcons(GravityInteract.PencilOperator equipped)
    {
        SetAllIconsAlpha(unequippedAlpha);

        switch (equipped)
        {
            case GravityInteract.PencilOperator.Addition:
                SetIconAlpha(additionIcon, equippedAlpha);
                SetIconSelected(additionIcon, true, "Addition Glow", equipped);
                break;

            case GravityInteract.PencilOperator.Subtraction:
                SetIconAlpha(subtractionIcon, equippedAlpha);
                SetIconSelected(subtractionIcon, true, "Subtraction Glow", equipped);
                break;

            case GravityInteract.PencilOperator.Multiplication:
                SetIconAlpha(multiplicationIcon, equippedAlpha);
                SetIconSelected(multiplicationIcon, true, "Multiplication Glow", equipped);
                break;

            case GravityInteract.PencilOperator.Division:
                SetIconAlpha(divisionIcon, equippedAlpha);
                SetIconSelected(divisionIcon, true, "Division Glow", equipped);
                break;
        }

        AnimateHudIconScales(equipped);
    }

    private void SetIconSelected(
        Image icon,
        bool selected,
        string glowName,
        GravityInteract.PencilOperator operatorType = GravityInteract.PencilOperator.None)
    {
        if (icon == null)
            return;

        if (!hudIconBaseScales.TryGetValue(icon, out Vector3 baseScale))
        {
            baseScale = icon.transform.localScale;
            hudIconBaseScales.Add(icon, baseScale);
        }

        Transform glowTransform = icon.transform.Find(glowName);
        if (glowTransform == null)
        {
            GameObject glowObject = new GameObject(
                glowName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            glowTransform = glowObject.transform;
            glowTransform.SetParent(icon.transform, false);
            glowTransform.SetAsFirstSibling();

            RectTransform glowRect = (RectTransform)glowTransform;
            glowRect.anchorMin = Vector2.zero;
            glowRect.anchorMax = Vector2.one;
            glowRect.offsetMin = new Vector2(-12f, -12f);
            glowRect.offsetMax = new Vector2(12f, 12f);
        }

        Image glow = glowTransform.GetComponent<Image>();
        if (glow == null)
            return;

        if (glow.sprite == null)
            glow.sprite = hudGlowSprite != null ? hudGlowSprite : CreateHudGlowSprite();
        glow.preserveAspect = false;
        glow.raycastTarget = false;
        glow.transform.SetAsFirstSibling();

        if (selected)
        {
            Color color = GetOperatorColor(operatorType);
            color.a = 0.8f;
            glow.color = color;
            glow.enabled = glow.sprite != null;
            glow.rectTransform.localScale = Vector3.one * 1.25f;
        }
        else
        {
            glow.enabled = false;
            glow.rectTransform.localScale = Vector3.one;
        }
    }

    private void AnimateHudIconScales(GravityInteract.PencilOperator equipped, bool immediate = false)
    {
        if (hudZoomRoutine != null)
        {
            StopCoroutine(hudZoomRoutine);
            hudZoomRoutine = null;
        }

        Image[] icons = { additionIcon, subtractionIcon, multiplicationIcon, divisionIcon };
        Vector3[] startScales = new Vector3[icons.Length];
        Vector3[] targetScales = new Vector3[icons.Length];

        for (int i = 0; i < icons.Length; i++)
        {
            Image icon = icons[i];
            if (icon == null)
                continue;

            if (!hudIconBaseScales.TryGetValue(icon, out Vector3 baseScale))
            {
                baseScale = icon.transform.localScale;
                hudIconBaseScales.Add(icon, baseScale);
            }

            startScales[i] = icon.transform.localScale;
            bool selected = IsHudIconSelected(icon, equipped);
            float scaleMultiplier = equipped == GravityInteract.PencilOperator.None
                ? 1f
                : selected ? 1.25f : 0.88f;
            targetScales[i] = baseScale * scaleMultiplier;
        }

        if (immediate)
        {
            for (int i = 0; i < icons.Length; i++)
                if (icons[i] != null)
                    icons[i].transform.localScale = targetScales[i];
            return;
        }

        hudZoomRoutine = StartCoroutine(AnimateHudIconScales(startScales, targetScales, icons));
    }

    private IEnumerator AnimateHudIconScales(
        Vector3[] startScales,
        Vector3[] targetScales,
        Image[] icons)
    {
        const float duration = 0.24f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);

            for (int i = 0; i < icons.Length; i++)
                if (icons[i] != null)
                    icons[i].transform.localScale = Vector3.Lerp(startScales[i], targetScales[i], t);

            yield return null;
        }

        for (int i = 0; i < icons.Length; i++)
            if (icons[i] != null)
                icons[i].transform.localScale = targetScales[i];

        hudZoomRoutine = null;
    }

    private bool IsHudIconSelected(Image icon, GravityInteract.PencilOperator equipped)
    {
        return equipped switch
        {
            GravityInteract.PencilOperator.Addition => icon == additionIcon,
            GravityInteract.PencilOperator.Subtraction => icon == subtractionIcon,
            GravityInteract.PencilOperator.Multiplication => icon == multiplicationIcon,
            GravityInteract.PencilOperator.Division => icon == divisionIcon,
            _ => false
        };
    }

    private Color GetOperatorColor(GravityInteract.PencilOperator operatorType)
    {
        PencilTipOperatorColor colorSource = pencilGun != null
            ? pencilGun.GetComponent<PencilTipOperatorColor>()
            : null;

        if (colorSource != null && colorSource.TryGetOperatorColor(operatorType, out Color color))
            return color;

        return operatorType switch
        {
            GravityInteract.PencilOperator.Addition => new Color(0.1f, 1f, 0.25f, 1f),
            GravityInteract.PencilOperator.Subtraction => new Color(1f, 0.45f, 0.05f, 1f),
            GravityInteract.PencilOperator.Multiplication => new Color(0.1f, 0.35f, 1f, 1f),
            GravityInteract.PencilOperator.Division => new Color(1f, 0.08f, 0.08f, 1f),
            _ => Color.white
        };
    }

    private static Sprite CreateHudGlowSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Operator HUD Glow",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = Vector2.one * ((size - 1) * 0.5f);
        float radius = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2f) * 0.8f;
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }

        texture.Apply(false, true);
        hudGlowSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f);
        hudGlowSprite.name = "Operator HUD Glow";
        hudGlowSprite.hideFlags = HideFlags.HideAndDontSave;
        return hudGlowSprite;
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
