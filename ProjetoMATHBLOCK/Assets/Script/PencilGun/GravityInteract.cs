using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using System.Collections;
using System.Collections.Generic;

public class GravityInteract : MonoBehaviour
{
    public enum PencilOperator
    {
        None,
        Addition,
        Subtraction,
        Multiplication,
        Division
    }

    public float grabDistance = 10f;
    public float speed = 5f;
    public float grabCooldown = 0.3f; // tempo de espera após soltar

    [FormerlySerializedAs("camera")]
    public Transform interactionCamera;
    public Transform playerFront; // ponto na frente do player

    [SerializeField] private PencilOperator equippedOperator = PencilOperator.None;
    [SerializeField] private float duplicateSpawnHeight = 1.5f;
    [SerializeField] private float releaseVelocityMultiplier = 1f;
    [SerializeField] private float maxReleaseSpeed = 18f;
    [SerializeField] private float minCarriedBlockDistance = 1f;
    [SerializeField] private float maxCarriedBlockDistance = 15f;
    [SerializeField] private float carriedBlockScrollSpeed = 0.45f;
    [SerializeField, Range(0f, 1f)] private float carriedBlockCollisionOpacity = 0.3f;
    [SerializeField] private float carriedBlockOpacityLerpSpeed = 8f;
    [SerializeField] private float hammerApplySpeedThreshold = 10.14f;
    [SerializeField, Range(0f, 360f)] private float hammerAllowedDirectionAngle = 230f;
    [SerializeField] private string hammerImpactEffectObjectName = "efeitomarretada";
    [SerializeField] private float hammerImpactEffectDuration = 1f;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Transform operatorAbsorbTarget;
    [SerializeField] private Vector3 operatorAbsorbTargetCameraLocalPosition = new Vector3(0f, -0.55f, 0.45f);

    private PlayerInput playerInput;
    private InputAction applyOperatorAction;
    private InputAction duplicateBlockAction;
    private InputAction undoBlockOperationAction;
    private bool grabbed;
    private bool canRaycast = true;
    private bool isOnCooldown;

    private Transform grabbedObject;
    private Rigidbody grabbedRb;
    private MathBlockValue grabbedBlockValue;
    private Vector3 carriedVelocity;
    private Vector3 lastCarriedPosition;
    private bool hasLastCarriedPosition;
    private float currentCarriedBlockDistance;
    private readonly List<CollisionIgnorePair> ignoredCarriedBlockCollisions = new List<CollisionIgnorePair>();
    private readonly List<CarriedRendererState> carriedRendererStates = new List<CarriedRendererState>();
    private float currentCarriedBlockOpacity = 1f;
    private float targetCarriedBlockOpacity = 1f;
    private GameObject hammerImpactEffectObject;
    private Coroutine hammerImpactDisableRoutine;

    public PencilOperator EquippedOperator => equippedOperator;
    public Transform OperatorAbsorbTarget => GetOrCreateOperatorAbsorbTarget();

    private void Awake()
    {
        if (interactionCamera == null && Camera.main != null)
        {
            interactionCamera = Camera.main.transform;
        }

        if (playerMovement == null)
        {
            playerMovement = GetComponentInParent<PlayerMovement>();
        }

        playerInput = GetComponentInParent<PlayerInput>();
        if (playerInput != null)
        {
            applyOperatorAction = playerInput.actions.FindAction("Operators", throwIfNotFound: false);
            duplicateBlockAction = playerInput.actions.FindAction("DuplicateBlock", throwIfNotFound: false);
            undoBlockOperationAction = playerInput.actions.FindAction("UndoBlockOperation", throwIfNotFound: false);

            if (applyOperatorAction != null)
            {
                applyOperatorAction.performed += OnApplyOperatorInput;
            }

            if (duplicateBlockAction != null)
            {
                duplicateBlockAction.performed += OnDuplicateBlockInput;
            }

            if (undoBlockOperationAction != null)
            {
                undoBlockOperationAction.performed += OnUndoBlockOperationInput;
            }
        }
    }

    private void OnDestroy()
    {
        ClearCarriedOperationPreview();
        RestoreCarriedBlockOpacity();
        RestoreCarriedBlockCollisions();

        if (applyOperatorAction != null)
        {
            applyOperatorAction.performed -= OnApplyOperatorInput;
        }

        if (duplicateBlockAction != null)
        {
            duplicateBlockAction.performed -= OnDuplicateBlockInput;
        }

        if (undoBlockOperationAction != null)
        {
            undoBlockOperationAction.performed -= OnUndoBlockOperationInput;
        }
    }

    void Update()
    {
        if (interactionCamera != null)
        {
            Debug.DrawRay(
                interactionCamera.position,
                interactionCamera.forward * grabDistance,
                Color.red
            );
        }

        // se estiver segurando um objeto
        if (grabbed && grabbedObject != null)
        {
            HandleCarriedBlockZoom();

            Vector3 targetPosition = GetCarriedTargetPosition();
            grabbedObject.position = Vector3.Lerp(
                grabbedObject.position,
                targetPosition,
                Time.deltaTime * speed
            );

            UpdateCarriedVelocity();
            UpdateCarriedBlockCollisionOpacity(Time.deltaTime);
        }
    }

    private void OnApplyOperatorInput(InputAction.CallbackContext context)
    {
        TryHandleApplyOperator();
    }

    private void OnDuplicateBlockInput(InputAction.CallbackContext context)
    {
        TryHandleDuplicateBlock();
    }

    private void OnUndoBlockOperationInput(InputAction.CallbackContext context)
    {
        TryHandleUndoBlockOperation();
    }

    public void OnInteractEvent(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            TryHandleGrabOrDrop();
        }
    }

    private void TryHandleGrabOrDrop()
    {
        if (isOnCooldown)
            return;

        if (grabbed)
        {
            if (TryGetMathBlockHit(out RaycastHit lookedBlockHit) && lookedBlockHit.transform != grabbedObject)
                return;

            Soltar();
            return;
        }

        if (!TryGetMathBlockHit(out RaycastHit hit))
            return;

        if (canRaycast)
        {
            Pegar(hit);
        }
    }

    private void TryHandleApplyOperator()
    {
        if (isOnCooldown || !grabbed || grabbedObject == null || equippedOperator == PencilOperator.None)
            return;

        if (!TryGetMathBlockHit(out RaycastHit hit))
            return;

        if (hit.transform == grabbedObject)
            return;

        HandleOperatorApplication(hit);
    }

    private bool TryHandleHammerApplication(MathBlockValue overlappedBlock)
    {
        if (isOnCooldown || !grabbed || grabbedObject == null || grabbedBlockValue == null || equippedOperator == PencilOperator.None)
            return false;

        if (overlappedBlock == null)
            return false;

        if (carriedVelocity.magnitude < hammerApplySpeedThreshold)
            return false;

        if (!IsHammerDirectionAllowed(carriedVelocity))
            return false;

        Vector3 impactPosition = grabbedObject.position;
        Vector3 impactDirection = carriedVelocity.sqrMagnitude > Mathf.Epsilon ? carriedVelocity.normalized : Vector3.up;
        Color impactColor = GetBlockVisualColor(overlappedBlock);
        return ApplyOperatorToBlock(
            overlappedBlock,
            $"marretada em {overlappedBlock.name}",
            true,
            impactPosition,
            impactColor,
            impactDirection
        );
    }

    private bool IsHammerDirectionAllowed(Vector3 velocity)
    {
        if (velocity.sqrMagnitude <= Mathf.Epsilon)
            return false;

        float halfAngle = hammerAllowedDirectionAngle * 0.5f;
        float angleFromDown = Vector3.Angle(velocity, Vector3.down);
        return angleFromDown <= halfAngle;
    }

    private void TryHandleDuplicateBlock()
    {
        if (isOnCooldown || grabbed || playerMovement == null || playerMovement.AvailableBlockDuplications <= 0)
            return;

        if (!TryGetMathBlockHit(out RaycastHit hit))
            return;

        DuplicateBlock(hit.transform);
    }

    private void TryHandleUndoBlockOperation()
    {
        if (isOnCooldown || grabbed)
            return;

        if (!TryGetMathBlockHit(out RaycastHit hit))
            return;

        MathBlockValue targetBlock = hit.collider.GetComponent<MathBlockValue>();
        if (targetBlock == null)
        {
            Debug.LogWarning($"Bloco {hit.collider.name} nao possui MathBlockValue para desfazer operacao.");
            return;
        }

        if (!targetBlock.TryUndoLastOperation(duplicateSpawnHeight))
        {
            Debug.Log($"Bloco {targetBlock.name} nao possui operacoes para desfazer.");
        }
    }

    private bool TryGetMathBlockHit(out RaycastHit hit)
    {
        hit = default;
        if (interactionCamera == null)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(interactionCamera.position, interactionCamera.forward, grabDistance);
        float nearestDistance = float.MaxValue;
        bool hasHit = false;

        for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
        {
            RaycastHit candidateHit = hits[hitIndex];
            Collider candidateCollider = candidateHit.collider;
            if (candidateCollider == null || !candidateCollider.CompareTag("MathBlock"))
                continue;

            if (IsColliderFromGrabbedObject(candidateCollider))
                continue;

            if (candidateHit.distance >= nearestDistance)
                continue;

            nearestDistance = candidateHit.distance;
            hit = candidateHit;
            hasHit = true;
        }

        return hasHit;
    }

    private bool IsColliderFromGrabbedObject(Collider targetCollider)
    {
        if (targetCollider == null || grabbedObject == null)
            return false;

        Transform targetTransform = targetCollider.transform;
        return targetTransform == grabbedObject || targetTransform.IsChildOf(grabbedObject);
    }

    private void HandleOperatorApplication(RaycastHit hit)
    {
        var targetBlock = hit.collider.GetComponent<MathBlockValue>();
        if (targetBlock == null)
        {
            targetBlock = hit.collider.gameObject.AddComponent<MathBlockValue>();
        }

        ApplyOperatorToBlock(targetBlock, hit.collider.name);
    }

    private bool ApplyOperatorToBlock(
        MathBlockValue targetBlock,
        string targetLabel,
        bool spawnHammerImpactEffect = false,
        Vector3 hammerImpactPosition = default,
        Color hammerImpactColor = default(Color),
        Vector3 hammerImpactDirection = default(Vector3))
    {
        if (targetBlock == null)
            return false;

        if (grabbedObject == null)
            return false;

        string blockName = targetBlock.name;
        string contextLabel = string.IsNullOrWhiteSpace(targetLabel) ? blockName : targetLabel;
        var carriedBlock = grabbedBlockValue != null ? grabbedBlockValue : grabbedObject.GetComponent<MathBlockValue>();
        if (carriedBlock == null)
        {
            Debug.LogWarning($"Bloco carregado {grabbedObject.name} nao possui MathBlockValue.");
            return false;
        }

        int targetValue = targetBlock.CurrentValue;
        ClearCarriedOperationPreview();
        RestoreCarriedBlockOpacity();

        if (targetBlock.TryApplyOperator(equippedOperator, carriedBlock))
        {
            if (spawnHammerImpactEffect)
            {
                PlayHammerImpactEffect(hammerImpactPosition, hammerImpactColor, hammerImpactDirection);
            }

            Debug.Log(
                $"Operacao concluida ({contextLabel}): {targetValue} {equippedOperator} {carriedBlock.CurrentValue} = {targetBlock.CurrentValue}"
            );

            RestoreCarriedBlockCollisions();
            RestoreCarriedBlockOpacity();
            Destroy(grabbedObject.gameObject);
            grabbedObject = null;
            grabbedRb = null;
            grabbedBlockValue = null;
            grabbed = false;
            canRaycast = true;
            return true;
        }

        Debug.LogWarning(
            $"Operacao invalida ({contextLabel}): {targetValue} {equippedOperator} {carriedBlock.CurrentValue} no bloco {blockName}"
        );
        return false;
    }

    private void PlayHammerImpactEffect(Vector3 position, Color color, Vector3 direction)
    {
        if (!TryGetHammerImpactEffectObject(out GameObject effectObject))
        {
            Debug.LogWarning($"Nao foi possivel localizar o sistema de particulas '{hammerImpactEffectObjectName}'.");
            return;
        }

        if (hammerImpactDisableRoutine != null)
        {
            StopCoroutine(hammerImpactDisableRoutine);
            hammerImpactDisableRoutine = null;
        }

        effectObject.SetActive(true);
        effectObject.transform.SetPositionAndRotation(position, Quaternion.FromToRotation(Vector3.up, direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector3.up));

        if (!TryGetHammerImpactParticleSystem(effectObject, out ParticleSystem particles))
        {
            Debug.LogWarning($"O objeto '{hammerImpactEffectObjectName}' nao possui ParticleSystem.");
            effectObject.SetActive(false);
            return;
        }

        ConfigureHammerImpactParticleSystem(particles, color, direction);
        particles.Clear(true);
        particles.Play(true);
        hammerImpactDisableRoutine = StartCoroutine(DisableHammerImpactEffectAfterDelay(effectObject, hammerImpactEffectDuration));
    }

    private Color GetBlockVisualColor(MathBlockValue blockValue)
    {
        if (blockValue == null)
            return Color.white;

        if (blockValue.TryGetVisualColor(out Color color))
            return color;

        return Color.white;
    }

    private IEnumerator DisableHammerImpactEffectAfterDelay(GameObject effectObject, float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, delay));

        if (effectObject != null)
        {
            ParticleSystem particles = effectObject.GetComponentInChildren<ParticleSystem>(true);
            if (particles != null)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            effectObject.SetActive(false);
        }

        hammerImpactDisableRoutine = null;
    }

    private bool TryGetHammerImpactEffectObject(out GameObject effectObject)
    {
        if (hammerImpactEffectObject != null)
        {
            effectObject = hammerImpactEffectObject;
            return true;
        }

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject candidate = allObjects[i];
            if (candidate == null || candidate.name != hammerImpactEffectObjectName)
                continue;

            if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
                continue;

            hammerImpactEffectObject = candidate;
            effectObject = hammerImpactEffectObject;
            return true;
        }

        effectObject = null;
        return false;
    }

    private bool TryGetHammerImpactParticleSystem(GameObject effectObject, out ParticleSystem particles)
    {
        particles = null;
        if (effectObject == null)
            return false;

        particles = effectObject.GetComponentInChildren<ParticleSystem>(true);
        if (particles != null)
            return true;

        return false;
    }

    private void ConfigureHammerImpactParticleSystem(ParticleSystem particles, Color color, Vector3 direction)
    {
        if (particles == null)
            return;

        Color vividColor = GetHammerImpactColor(color);
        Vector3 safeDirection = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : Vector3.up;

        ParticleSystem.MainModule main = particles.main;
        main.duration = hammerImpactEffectDuration;
        main.loop = false;
        main.prewarm = false;
        main.startLifetime = hammerImpactEffectDuration;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        Color startColor = vividColor;
        startColor.a = 0.35f;
        main.startColor = startColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.playOnAwake = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.22f;
        shape.radiusThickness = 1f;
        shape.angle = 24f;
        shape.rotation = Quaternion.FromToRotation(Vector3.up, safeDirection).eulerAngles;
        shape.alignToDirection = true;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 220, 260, 1, 0.01f)
        });

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(safeDirection.x * 1.1f);
        velocity.y = new ParticleSystem.MinMaxCurve(safeDirection.y * 1.1f);
        velocity.z = new ParticleSystem.MinMaxCurve(safeDirection.z * 1.1f);

        ParticleSystem.ForceOverLifetimeModule force = particles.forceOverLifetime;
        force.enabled = false;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(vividColor, 0f),
                new GradientColorKey(vividColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.35f, 0f),
                new GradientAlphaKey(0.22f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.15f, 1f),
            new Keyframe(0.65f, 0.85f),
            new Keyframe(1f, 0f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
    }

    private static Color GetHammerImpactColor(Color sourceColor)
    {
        Color.RGBToHSV(sourceColor, out float hue, out float saturation, out float value);
        Color baseColor = Color.HSVToRGB(hue, Mathf.Clamp01(saturation * 1.55f), Mathf.Clamp01(value * 1.2f));
        Color accentColor = Color.HSVToRGB(Mathf.Repeat(hue + 0.09f, 1f), 1f, 1f);
        Color vividColor = Color.Lerp(baseColor, accentColor, 0.22f);
        vividColor.a = sourceColor.a;
        return vividColor;
    }

    private void DuplicateBlock(Transform sourceBlock)
    {
        if (sourceBlock == null)
            return;

        Vector3 spawnPosition = sourceBlock.position + Vector3.up * duplicateSpawnHeight;
        GameObject duplicatedBlock = Instantiate(sourceBlock.gameObject, spawnPosition, sourceBlock.rotation);
        duplicatedBlock.name = $"{sourceBlock.name}_Clone";
        CopyRendererColors(sourceBlock, duplicatedBlock.transform);

        MathBlockValue duplicatedValue = duplicatedBlock.GetComponent<MathBlockValue>();
        if (duplicatedValue != null)
        {
            duplicatedValue.InitializeDuplicatedBlock();
        }

        Rigidbody duplicatedRigidbody = duplicatedBlock.GetComponent<Rigidbody>();
        if (duplicatedRigidbody != null)
        {
            duplicatedRigidbody.isKinematic = false;
            duplicatedRigidbody.useGravity = true;
            duplicatedRigidbody.linearVelocity = Vector3.zero;
            duplicatedRigidbody.angularVelocity = Vector3.zero;
        }

        if (!playerMovement.TryConsumeBlockDuplication())
        {
            Destroy(duplicatedBlock);
            return;
        }

        Debug.Log($"Bloco duplicado: {sourceBlock.name}. Duplicacoes restantes: {playerMovement.AvailableBlockDuplications}");
    }

    private void CopyRendererColors(Transform sourceBlock, Transform duplicatedBlock)
    {
        Renderer[] sourceRenderers = sourceBlock.GetComponentsInChildren<Renderer>();
        Renderer[] duplicatedRenderers = duplicatedBlock.GetComponentsInChildren<Renderer>();
        int rendererCount = Mathf.Min(sourceRenderers.Length, duplicatedRenderers.Length);

        for (int rendererIndex = 0; rendererIndex < rendererCount; rendererIndex++)
        {
            Material[] sourceMaterials = sourceRenderers[rendererIndex].materials;
            Material[] duplicatedMaterials = duplicatedRenderers[rendererIndex].materials;
            int materialCount = Mathf.Min(sourceMaterials.Length, duplicatedMaterials.Length);

            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                CopyMaterialColor(sourceMaterials[materialIndex], duplicatedMaterials[materialIndex]);
            }
        }
    }

    private static void CopyMaterialColor(Material sourceMaterial, Material duplicatedMaterial)
    {
        if (sourceMaterial == null || duplicatedMaterial == null)
            return;

        if (sourceMaterial.HasProperty("_BaseColor") && duplicatedMaterial.HasProperty("_BaseColor"))
        {
            duplicatedMaterial.SetColor("_BaseColor", sourceMaterial.GetColor("_BaseColor"));
        }

        if (sourceMaterial.HasProperty("_Color") && duplicatedMaterial.HasProperty("_Color"))
        {
            duplicatedMaterial.SetColor("_Color", sourceMaterial.GetColor("_Color"));
        }
    }

    public void SetEquippedOperator(PencilOperator newOperator)
    {
        PencilOperator previousOperator = equippedOperator;
        equippedOperator = newOperator;

        if (previousOperator == newOperator)
        {
            Debug.Log($"Player manteve o operador equipado: {equippedOperator}");
            return;
        }

        Debug.Log($"Player trocou operador: {previousOperator} -> {equippedOperator}");
    }

    public void ClearEquippedOperator()
    {
        equippedOperator = PencilOperator.None;
        ClearCarriedOperationPreview();
        Debug.Log("Player limpou o operador equipado.");
    }

    public Transform GetOrCreateOperatorAbsorbTarget()
    {
        if (operatorAbsorbTarget != null)
            return operatorAbsorbTarget;

        Transform targetParent = interactionCamera != null ? interactionCamera : transform;
        Transform existingTarget = targetParent.Find("OperatorAbsorbTarget");
        if (existingTarget != null)
        {
            operatorAbsorbTarget = existingTarget;
            return operatorAbsorbTarget;
        }

        GameObject targetObject = new GameObject("OperatorAbsorbTarget");
        targetObject.transform.SetParent(targetParent, false);
        targetObject.transform.localPosition = GetOperatorAbsorbTargetLocalPosition(targetParent);
        targetObject.transform.localRotation = Quaternion.identity;
        operatorAbsorbTarget = targetObject.transform;
        return operatorAbsorbTarget;
    }

    private Vector3 GetOperatorAbsorbTargetLocalPosition(Transform targetParent)
    {
        if (targetParent == interactionCamera)
            return operatorAbsorbTargetCameraLocalPosition;

        return Vector3.forward * 0.9f;
    }

    public void Pegar(RaycastHit hit)
    {
        RestoreCarriedBlockCollisions();

        grabbedRb = hit.transform.GetComponent<Rigidbody>();
        grabbedObject = hit.transform;
        MathBlockValue mathBlockValue = hit.transform.GetComponent<MathBlockValue>();
        grabbedBlockValue = mathBlockValue;

        if (grabbedRb == null)
        {
            grabbedObject = null;
            grabbedBlockValue = null;
            return;
        }

        grabbedRb.isKinematic = true;
        grabbedRb.useGravity = false;
        CacheCarriedBlockRenderers(grabbedObject);
        IgnoreCarriedBlockCollisions(grabbedObject);
        if (mathBlockValue != null)
        {
            mathBlockValue.RestoreOriginalRotation();
        }

        grabbed = true;
        canRaycast = false;
        currentCarriedBlockDistance = GetInitialCarriedBlockDistance();
        carriedVelocity = Vector3.zero;
        lastCarriedPosition = grabbedObject.position;
        hasLastCarriedPosition = true;
        Debug.Log($"Bloco segurado: {grabbedObject.name}");
    }

    public void Soltar()
    {
        ClearCarriedOperationPreview();
        RestoreCarriedBlockCollisions();
        RestoreCarriedBlockOpacity();

        if (grabbedRb != null)
        {
            Vector3 releaseVelocity = Vector3.ClampMagnitude(
                carriedVelocity * releaseVelocityMultiplier,
                maxReleaseSpeed
            );

            grabbedRb.useGravity = true;
            grabbedRb.isKinematic = false;
            grabbedRb.linearVelocity = releaseVelocity;
        }

        grabbedRb = null;
        grabbedObject = null;
        grabbedBlockValue = null;
        grabbed = false;
        carriedVelocity = Vector3.zero;
        hasLastCarriedPosition = false;

        canRaycast = false;
        StartCoroutine(GrabCooldown());
    }

    private void IgnoreCarriedBlockCollisions(Transform carriedBlock)
    {
        if (carriedBlock == null)
            return;

        Collider[] carriedColliders = carriedBlock.GetComponentsInChildren<Collider>();
        if (carriedColliders == null || carriedColliders.Length == 0)
            return;

        GameObject[] mathBlocks = GameObject.FindGameObjectsWithTag("MathBlock");
        foreach (Collider carriedCollider in carriedColliders)
        {
            if (carriedCollider == null)
                continue;

            foreach (GameObject mathBlock in mathBlocks)
            {
                if (mathBlock == null || mathBlock.transform == carriedBlock || mathBlock.transform.IsChildOf(carriedBlock))
                    continue;

                Collider[] targetColliders = mathBlock.GetComponentsInChildren<Collider>();
                foreach (Collider targetCollider in targetColliders)
                {
                    if (targetCollider == null || targetCollider == carriedCollider)
                        continue;

                    Physics.IgnoreCollision(carriedCollider, targetCollider, true);
                    ignoredCarriedBlockCollisions.Add(new CollisionIgnorePair(carriedCollider, targetCollider));
                }
            }
        }
    }

    private void RestoreCarriedBlockCollisions()
    {
        for (int i = 0; i < ignoredCarriedBlockCollisions.Count; i++)
        {
            CollisionIgnorePair pair = ignoredCarriedBlockCollisions[i];
            if (pair.First != null && pair.Second != null)
            {
                Physics.IgnoreCollision(pair.First, pair.Second, false);
            }
        }

        ignoredCarriedBlockCollisions.Clear();
    }

    private void CacheCarriedBlockRenderers(Transform carriedBlock)
    {
        RestoreCarriedBlockOpacity();

        if (carriedBlock == null)
            return;

        Renderer[] renderers = carriedBlock.GetComponentsInChildren<Renderer>();
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Renderer targetRenderer = renderers[rendererIndex];
            if (targetRenderer == null || IsCarriedBlockLabelRenderer(targetRenderer))
                continue;

            Material[] materials = targetRenderer.materials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                    continue;

                carriedRendererStates.Add(new CarriedRendererState(targetRenderer, material));
            }
        }

        currentCarriedBlockOpacity = 1f;
        targetCarriedBlockOpacity = 1f;
    }

    private void UpdateCarriedBlockCollisionOpacity(float deltaTime)
    {
        bool isOverlapping = TryGetCarriedBlockOverlap(out MathBlockValue overlappedBlock);
        if (isOverlapping && TryHandleHammerApplication(overlappedBlock))
        {
            return;
        }

        targetCarriedBlockOpacity = isOverlapping ? carriedBlockCollisionOpacity : 1f;
        UpdateCarriedOperationPreview(overlappedBlock);
        UpdateCarriedBlockOpacity(deltaTime);
    }

    private bool TryGetCarriedBlockOverlap(out MathBlockValue overlappedBlock)
    {
        overlappedBlock = null;

        if (grabbedObject == null)
            return false;

        Collider[] carriedColliders = grabbedObject.GetComponentsInChildren<Collider>();
        if (carriedColliders == null || carriedColliders.Length == 0)
            return false;

        GameObject[] mathBlocks = GameObject.FindGameObjectsWithTag("MathBlock");
        foreach (Collider carriedCollider in carriedColliders)
        {
            if (carriedCollider == null || !carriedCollider.enabled)
                continue;

            foreach (GameObject mathBlock in mathBlocks)
            {
                if (mathBlock == null || mathBlock.transform == grabbedObject || mathBlock.transform.IsChildOf(grabbedObject))
                    continue;

                Collider[] targetColliders = mathBlock.GetComponentsInChildren<Collider>();
                foreach (Collider targetCollider in targetColliders)
                {
                    if (targetCollider == null || !targetCollider.enabled || targetCollider == carriedCollider)
                        continue;

                    if (Physics.ComputePenetration(
                        carriedCollider,
                        carriedCollider.transform.position,
                        carriedCollider.transform.rotation,
                        targetCollider,
                        targetCollider.transform.position,
                        targetCollider.transform.rotation,
                        out _,
                        out _))
                    {
                        overlappedBlock = mathBlock.GetComponent<MathBlockValue>();
                        if (overlappedBlock == null)
                        {
                            overlappedBlock = mathBlock.GetComponentInParent<MathBlockValue>();
                        }

                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void UpdateCarriedOperationPreview(MathBlockValue targetBlock)
    {
        if (grabbedBlockValue == null || targetBlock == null || equippedOperator == PencilOperator.None)
        {
            ClearCarriedOperationPreview();
            return;
        }

        if (TryCalculateOperationPreview(targetBlock.CurrentValue, grabbedBlockValue.CurrentValue, equippedOperator, out int previewResult))
        {
            grabbedBlockValue.SetPreviewValue(previewResult);
        }
        else
        {
            ClearCarriedOperationPreview();
        }
    }

    private void ClearCarriedOperationPreview()
    {
        if (grabbedBlockValue != null)
        {
            grabbedBlockValue.ClearPreviewValue();
        }
    }

    private static bool TryCalculateOperationPreview(
        int targetValue,
        int carriedValue,
        PencilOperator operatorType,
        out int result)
    {
        result = targetValue;

        switch (operatorType)
        {
            case PencilOperator.Addition:
                result = targetValue + carriedValue;
                return true;

            case PencilOperator.Subtraction:
                result = targetValue - carriedValue;
                return result >= 0;

            case PencilOperator.Multiplication:
                result = targetValue * carriedValue;
                return true;

            case PencilOperator.Division:
                if (carriedValue <= 0 || targetValue % carriedValue != 0)
                    return false;

                result = targetValue / carriedValue;
                return true;

            default:
                return false;
        }
    }

    private void UpdateCarriedBlockOpacity(float deltaTime)
    {
        if (carriedRendererStates.Count == 0)
            return;

        float previousOpacity = currentCarriedBlockOpacity;
        float lerpFactor = 1f - Mathf.Exp(-Mathf.Max(0.01f, carriedBlockOpacityLerpSpeed) * deltaTime);
        currentCarriedBlockOpacity = Mathf.Lerp(currentCarriedBlockOpacity, targetCarriedBlockOpacity, lerpFactor);
        if (Mathf.Abs(currentCarriedBlockOpacity - targetCarriedBlockOpacity) < 0.01f)
        {
            currentCarriedBlockOpacity = targetCarriedBlockOpacity;
        }

        bool shouldUseTransparentMaterial = currentCarriedBlockOpacity < 0.999f || targetCarriedBlockOpacity < 0.999f;
        bool reachedOpaque = previousOpacity < 0.999f && currentCarriedBlockOpacity >= 0.999f && targetCarriedBlockOpacity >= 0.999f;

        for (int stateIndex = 0; stateIndex < carriedRendererStates.Count; stateIndex++)
        {
            CarriedRendererState state = carriedRendererStates[stateIndex];
            if (state.Renderer == null || state.Material == null)
                continue;

            if (shouldUseTransparentMaterial)
            {
                ConfigureTransparentMaterial(state.Material);
                state.ApplyAlpha(currentCarriedBlockOpacity);
            }
            else if (reachedOpaque)
            {
                state.RestoreMaterialState();
                state.ApplyAlpha(1f);
            }
        }
    }

    private void RestoreCarriedBlockOpacity()
    {
        for (int stateIndex = 0; stateIndex < carriedRendererStates.Count; stateIndex++)
        {
            CarriedRendererState state = carriedRendererStates[stateIndex];
            if (state.Renderer == null || state.Material == null)
                continue;

            state.RestoreMaterialState();
            state.ApplyAlpha(1f);
        }

        carriedRendererStates.Clear();
        currentCarriedBlockOpacity = 1f;
        targetCarriedBlockOpacity = 1f;
    }

    private static bool IsCarriedBlockLabelRenderer(Renderer targetRenderer)
    {
        if (targetRenderer == null)
            return false;

        Transform current = targetRenderer.transform;
        while (current != null)
        {
            if (current.name == "ValueLabels" || current.GetComponent<TextMesh>() != null)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        material.SetOverrideTag("RenderType", "Transparent");

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private void UpdateCarriedVelocity()
    {
        if (grabbedObject == null || Time.deltaTime <= 0f)
            return;

        Vector3 currentPosition = grabbedObject.position;
        if (hasLastCarriedPosition)
        {
            carriedVelocity = (currentPosition - lastCarriedPosition) / Time.deltaTime;
        }

        lastCarriedPosition = currentPosition;
        hasLastCarriedPosition = true;
    }

    private Vector3 GetCarriedTargetPosition()
    {
        if (interactionCamera == null)
        {
            if (playerFront != null)
                return playerFront.position;

            return grabbedObject.position;
        }

        return interactionCamera.position + (interactionCamera.forward * currentCarriedBlockDistance);
    }

    private void HandleCarriedBlockZoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollY, 0f))
            return;

        float scrollDirection = Mathf.Sign(scrollY);
        currentCarriedBlockDistance += scrollDirection * carriedBlockScrollSpeed;
        currentCarriedBlockDistance = Mathf.Clamp(
            currentCarriedBlockDistance,
            minCarriedBlockDistance,
            maxCarriedBlockDistance
        );
    }

    private float GetInitialCarriedBlockDistance()
    {
        float initialDistance = playerFront != null && interactionCamera != null
            ? Vector3.Distance(interactionCamera.position, playerFront.position)
            : minCarriedBlockDistance;

        return Mathf.Clamp(initialDistance, minCarriedBlockDistance, maxCarriedBlockDistance);
    }

    IEnumerator GrabCooldown()
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(grabCooldown);
        isOnCooldown = false;
        canRaycast = true;
    }

    private struct CollisionIgnorePair
    {
        public readonly Collider First;
        public readonly Collider Second;

        public CollisionIgnorePair(Collider first, Collider second)
        {
            First = first;
            Second = second;
        }
    }

    private struct CarriedRendererState
    {
        public readonly Renderer Renderer;
        public readonly Material Material;
        private readonly bool hasBaseColor;
        private readonly Color baseColor;
        private readonly bool hasColor;
        private readonly Color color;
        private readonly bool hasSurface;
        private readonly float surface;
        private readonly bool hasMode;
        private readonly float mode;
        private readonly bool hasSrcBlend;
        private readonly float srcBlend;
        private readonly bool hasDstBlend;
        private readonly float dstBlend;
        private readonly bool hasZWrite;
        private readonly float zWrite;
        private readonly int renderQueue;
        private readonly string renderTypeTag;
        private readonly bool hadTransparentKeyword;
        private readonly bool hadAlphaTestKeyword;
        private readonly bool hadAlphaBlendKeyword;
        private readonly bool hadAlphaPremultiplyKeyword;
        private readonly bool hasPropertyBlockColors;
        private readonly Color propertyBlockBaseColor;
        private readonly Color propertyBlockColor;

        public CarriedRendererState(Renderer targetRenderer, Material material)
        {
            Renderer = targetRenderer;
            Material = material;
            hasBaseColor = material.HasProperty("_BaseColor");
            baseColor = hasBaseColor ? material.GetColor("_BaseColor") : Color.white;
            hasColor = material.HasProperty("_Color");
            color = hasColor ? material.GetColor("_Color") : Color.white;
            hasSurface = material.HasProperty("_Surface");
            surface = hasSurface ? material.GetFloat("_Surface") : 0f;
            hasMode = material.HasProperty("_Mode");
            mode = hasMode ? material.GetFloat("_Mode") : 0f;
            hasSrcBlend = material.HasProperty("_SrcBlend");
            srcBlend = hasSrcBlend ? material.GetFloat("_SrcBlend") : 0f;
            hasDstBlend = material.HasProperty("_DstBlend");
            dstBlend = hasDstBlend ? material.GetFloat("_DstBlend") : 0f;
            hasZWrite = material.HasProperty("_ZWrite");
            zWrite = hasZWrite ? material.GetFloat("_ZWrite") : 0f;
            renderQueue = material.renderQueue;
            renderTypeTag = material.GetTag("RenderType", false, string.Empty);
            hadTransparentKeyword = material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
            hadAlphaTestKeyword = material.IsKeywordEnabled("_ALPHATEST_ON");
            hadAlphaBlendKeyword = material.IsKeywordEnabled("_ALPHABLEND_ON");
            hadAlphaPremultiplyKeyword = material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON");

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            hasPropertyBlockColors = !propertyBlock.isEmpty;
            propertyBlockBaseColor = hasPropertyBlockColors && hasBaseColor
                ? propertyBlock.GetColor("_BaseColor")
                : Color.white;
            propertyBlockColor = hasPropertyBlockColors && hasColor
                ? propertyBlock.GetColor("_Color")
                : Color.white;
        }

        public void ApplyAlpha(float alpha)
        {
            if (Material == null)
                return;

            if (hasBaseColor)
            {
                Color nextBaseColor = baseColor;
                nextBaseColor.a = alpha;
                Material.SetColor("_BaseColor", nextBaseColor);
            }

            if (hasColor)
            {
                Color nextColor = color;
                nextColor.a = alpha;
                Material.SetColor("_Color", nextColor);
            }

            if (Renderer != null && hasPropertyBlockColors)
            {
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                Renderer.GetPropertyBlock(propertyBlock);

                if (hasBaseColor)
                {
                    Color nextPropertyBaseColor = propertyBlockBaseColor;
                    nextPropertyBaseColor.a = alpha;
                    propertyBlock.SetColor("_BaseColor", nextPropertyBaseColor);
                }

                if (hasColor)
                {
                    Color nextPropertyColor = propertyBlockColor;
                    nextPropertyColor.a = alpha;
                    propertyBlock.SetColor("_Color", nextPropertyColor);
                }

                Renderer.SetPropertyBlock(propertyBlock);
            }
        }

        public void RestoreMaterialState()
        {
            if (Material == null)
                return;

            if (hasBaseColor)
            {
                Material.SetColor("_BaseColor", baseColor);
            }

            if (hasColor)
            {
                Material.SetColor("_Color", color);
            }

            if (hasSurface)
            {
                Material.SetFloat("_Surface", surface);
            }

            if (hasMode)
            {
                Material.SetFloat("_Mode", mode);
            }

            if (hasSrcBlend)
            {
                Material.SetFloat("_SrcBlend", srcBlend);
            }

            if (hasDstBlend)
            {
                Material.SetFloat("_DstBlend", dstBlend);
            }

            if (hasZWrite)
            {
                Material.SetFloat("_ZWrite", zWrite);
            }

            if (!hadTransparentKeyword)
            {
                Material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }

            RestoreKeyword(Material, "_ALPHATEST_ON", hadAlphaTestKeyword);
            RestoreKeyword(Material, "_ALPHABLEND_ON", hadAlphaBlendKeyword);
            RestoreKeyword(Material, "_ALPHAPREMULTIPLY_ON", hadAlphaPremultiplyKeyword);
            Material.SetOverrideTag("RenderType", renderTypeTag);
            Material.renderQueue = renderQueue;

            if (Renderer != null && hasPropertyBlockColors)
            {
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                Renderer.GetPropertyBlock(propertyBlock);

                if (hasBaseColor)
                {
                    propertyBlock.SetColor("_BaseColor", propertyBlockBaseColor);
                }

                if (hasColor)
                {
                    propertyBlock.SetColor("_Color", propertyBlockColor);
                }

                Renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static void RestoreKeyword(Material material, string keyword, bool wasEnabled)
        {
            if (wasEnabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }
    }
}
