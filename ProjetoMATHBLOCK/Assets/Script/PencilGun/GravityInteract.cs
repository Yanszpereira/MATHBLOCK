using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

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

    [Header("Gravity")]
    public float grabDistance = 10f;
    public float speed = 5f;
    public float grabCooldown = 0.3f;

    [Header("References")]
    public Transform camera;
    public Transform playerFront;

    [Header("Operators")]
    [SerializeField] private PencilOperator equippedOperator = PencilOperator.None;

    [Header("Duplicate")]
    [SerializeField] private float duplicateSpawnHeight = 1.5f;

    [Header("Throw")]
    [SerializeField] private float releaseVelocityMultiplier = 1f;
    [SerializeField] private float maxReleaseSpeed = 18f;

    [Header("Carried Block")]
    [SerializeField] private float minCarriedBlockDistance = 1f;
    [SerializeField] private float maxCarriedBlockDistance = 15f;
    [SerializeField] private float carriedBlockScrollSpeed = 0.45f;

    [Header("Player")]
    [SerializeField] private PlayerMovement playerMovement;

    [Header("Operator Absorb")]
    [SerializeField] private Transform operatorAbsorbTarget;
    [SerializeField] private Vector3 operatorAbsorbTargetCameraLocalPosition =
        new Vector3(0f, -0.55f, 0.45f);

    private PlayerInput playerInput;

    private InputAction applyOperatorAction;
    private InputAction duplicateBlockAction;
    private InputAction undoBlockOperationAction;

    private bool grabbed;
    private bool canRaycast = true;
    private bool isOnCooldown;

    private Transform grabbedObject;
    private Rigidbody grabbedRb;

    private Vector3 carriedVelocity;
    private Vector3 lastCarriedPosition;

    private bool hasLastCarriedPosition;

    private float currentCarriedBlockDistance;

    public PencilOperator EquippedOperator => equippedOperator;

    public Transform OperatorAbsorbTarget =>
        GetOrCreateOperatorAbsorbTarget();

    private void Awake()
    {
        if (playerMovement == null)
        {
            playerMovement = GetComponentInParent<PlayerMovement>();
        }

        playerInput = GetComponentInParent<PlayerInput>();

        if (playerInput != null)
        {
            applyOperatorAction =
                playerInput.actions.FindAction("Operators", false);

            duplicateBlockAction =
                playerInput.actions.FindAction("DuplicateBlock", false);

            undoBlockOperationAction =
                playerInput.actions.FindAction("UndoBlockOperation", false);

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

    private void Update()
    {
        Debug.DrawRay(
            camera.position,
            camera.forward * grabDistance,
            Color.red
        );

        // SEGURANDO BLOCO
        if (grabbed && grabbedObject != null)
        {
            HandleCarriedBlockZoom();

            Vector3 targetPosition =
                GetCarriedTargetPosition();

            grabbedObject.position =
                Vector3.Lerp(
                    grabbedObject.position,
                    targetPosition,
                    Time.deltaTime * speed
                );

            UpdateCarriedVelocity();
        }
    }

    // =========================================================
    // INPUTS
    // =========================================================

    private void OnApplyOperatorInput(
        InputAction.CallbackContext context
    )
    {
        TryHandleApplyOperator();
    }

    private void OnDuplicateBlockInput(
        InputAction.CallbackContext context
    )
    {
        TryHandleDuplicateBlock();
    }

    private void OnUndoBlockOperationInput(
        InputAction.CallbackContext context
    )
    {
        TryHandleUndoBlockOperation();
    }

    public void OnInteractEvent(
        InputAction.CallbackContext context
    )
    {
        if (context.performed)
        {
            TryHandleGrabOrDrop();
        }
    }

    // =========================================================
    // GRAVITY INTERACT
    // =========================================================

    private void TryHandleGrabOrDrop()
    {
        if (isOnCooldown)
            return;

        // JA ESTA SEGURANDO
        if (grabbed)
        {
            // verifica se esta olhando para um bloco
            if (TryGetMathBlockHit(out RaycastHit hit))
            {
                // olhando para OUTRO bloco
                // nao pode soltar
                if (hit.transform != grabbedObject)
                {
                    return;
                }
            }

            // solta normalmente
            Soltar();
            return;
        }

        // NAO ESTA SEGURANDO
        if (!TryGetMathBlockHit(out RaycastHit newHit))
            return;

        if (canRaycast)
        {
            Pegar(newHit);
        }
    }

    // =========================================================
    // OPERATORS
    // =========================================================

    private void TryHandleApplyOperator()
    {
        // precisa estar segurando
        if (
            isOnCooldown ||
            !grabbed ||
            grabbedObject == null ||
            equippedOperator == PencilOperator.None
        )
        {
            return;
        }

        // precisa estar olhando para um bloco
        if (!TryGetMathBlockHit(out RaycastHit hit))
            return;

        // nao pode ser o mesmo bloco segurado
        if (hit.transform == grabbedObject)
            return;

        HandleOperatorApplication(hit);
    }

    private void HandleOperatorApplication(
        RaycastHit hit
    )
    {
        var targetBlock =
            hit.collider.GetComponent<MathBlockValue>();

        var carriedBlock =
            grabbedObject.GetComponent<MathBlockValue>();

        if (carriedBlock == null)
        {
            Debug.LogWarning(
                $"Bloco carregado {grabbedObject.name} nao possui MathBlockValue."
            );

            return;
        }

        if (targetBlock == null)
        {
            targetBlock =
                hit.collider.gameObject.AddComponent<MathBlockValue>();
        }

        int targetValue = targetBlock.CurrentValue;

        if (
            targetBlock.TryApplyOperator(
                equippedOperator,
                carriedBlock
            )
        )
        {
            Debug.Log(
                $"Operacao concluida: {targetValue} {equippedOperator} {carriedBlock.CurrentValue} = {targetBlock.CurrentValue}"
            );

            Destroy(grabbedObject.gameObject);

            grabbedObject = null;
            grabbedRb = null;
            grabbed = false;

            canRaycast = true;
        }
        else
        {
            Debug.LogWarning(
                $"Operacao invalida: {targetValue} {equippedOperator} {carriedBlock.CurrentValue}"
            );
        }
    }

    // =========================================================
    // DUPLICATE
    // =========================================================

    private void TryHandleDuplicateBlock()
    {
        if (
            isOnCooldown ||
            grabbed ||
            playerMovement == null ||
            playerMovement.AvailableBlockDuplications <= 0
        )
        {
            return;
        }

        if (!TryGetMathBlockHit(out RaycastHit hit))
            return;

        DuplicateBlock(hit.transform);
    }

    private void DuplicateBlock(
        Transform sourceBlock
    )
    {
        if (sourceBlock == null)
            return;

        Vector3 spawnPosition =
            sourceBlock.position +
            Vector3.up * duplicateSpawnHeight;

        GameObject duplicatedBlock =
            Instantiate(
                sourceBlock.gameObject,
                spawnPosition,
                sourceBlock.rotation
            );

        duplicatedBlock.name =
            $"{sourceBlock.name}_Clone";

        CopyRendererColors(
            sourceBlock,
            duplicatedBlock.transform
        );

        MathBlockValue duplicatedValue =
            duplicatedBlock.GetComponent<MathBlockValue>();

        if (duplicatedValue != null)
        {
            duplicatedValue.InitializeDuplicatedBlock();
        }

        Rigidbody duplicatedRb =
            duplicatedBlock.GetComponent<Rigidbody>();

        if (duplicatedRb != null)
        {
            duplicatedRb.isKinematic = false;
            duplicatedRb.useGravity = true;
            duplicatedRb.linearVelocity = Vector3.zero;
            duplicatedRb.angularVelocity = Vector3.zero;
        }

        if (!playerMovement.TryConsumeBlockDuplication())
        {
            Destroy(duplicatedBlock);
            return;
        }

        Debug.Log(
            $"Bloco duplicado: {sourceBlock.name}"
        );
    }

    // =========================================================
    // UNDO
    // =========================================================

    private void TryHandleUndoBlockOperation()
    {
        if (isOnCooldown || grabbed)
            return;

        if (!TryGetMathBlockHit(out RaycastHit hit))
            return;

        MathBlockValue targetBlock =
            hit.collider.GetComponent<MathBlockValue>();

        if (targetBlock == null)
        {
            Debug.LogWarning(
                $"Bloco {hit.collider.name} nao possui MathBlockValue."
            );

            return;
        }

        if (!targetBlock.TryUndoLastOperation(duplicateSpawnHeight))
        {
            Debug.Log(
                $"Bloco {targetBlock.name} nao possui operacoes para desfazer."
            );
        }
    }

    // =========================================================
    // RAYCAST
    // =========================================================

    private bool TryGetMathBlockHit(
        out RaycastHit hit
    )
    {
        if (
            !Physics.Raycast(
                camera.position,
                camera.forward,
                out hit,
                grabDistance
            )
        )
        {
            return false;
        }

        return hit.collider.CompareTag("MathBlock");
    }

    // =========================================================
    // PICKUP
    // =========================================================

    public void Pegar(RaycastHit hit)
    {
        grabbedRb =
            hit.transform.GetComponent<Rigidbody>();

        grabbedObject = hit.transform;

        MathBlockValue mathBlockValue =
            hit.transform.GetComponent<MathBlockValue>();

        if (grabbedRb == null)
        {
            grabbedObject = null;
            return;
        }

        grabbedRb.isKinematic = true;
        grabbedRb.useGravity = false;

        if (mathBlockValue != null)
        {
            mathBlockValue.ResetRotationToOriginal();
        }

        grabbed = true;

        currentCarriedBlockDistance =
            GetInitialCarriedBlockDistance();

        carriedVelocity = Vector3.zero;

        lastCarriedPosition =
            grabbedObject.position;

        hasLastCarriedPosition = true;

        Debug.Log(
            $"Bloco segurado: {grabbedObject.name}"
        );
    }

    public void Soltar()
    {
        if (grabbedRb != null)
        {
            Vector3 releaseVelocity =
                Vector3.ClampMagnitude(
                    carriedVelocity *
                    releaseVelocityMultiplier,
                    maxReleaseSpeed
                );

            grabbedRb.useGravity = true;
            grabbedRb.isKinematic = false;
            grabbedRb.linearVelocity =
                releaseVelocity;
        }

        grabbedRb = null;
        grabbedObject = null;

        grabbed = false;

        carriedVelocity = Vector3.zero;

        hasLastCarriedPosition = false;

        StartCoroutine(GrabCooldown());
    }

    // =========================================================
    // MOVEMENT
    // =========================================================

    private void UpdateCarriedVelocity()
    {
        if (
            grabbedObject == null ||
            Time.deltaTime <= 0f
        )
        {
            return;
        }

        Vector3 currentPosition =
            grabbedObject.position;

        if (hasLastCarriedPosition)
        {
            carriedVelocity =
                (currentPosition - lastCarriedPosition)
                / Time.deltaTime;
        }

        lastCarriedPosition = currentPosition;

        hasLastCarriedPosition = true;
    }

    private Vector3 GetCarriedTargetPosition()
    {
        if (camera == null)
        {
            if (playerFront != null)
            {
                return playerFront.position;
            }

            return grabbedObject.position;
        }

        return
            camera.position +
            (camera.forward *
            currentCarriedBlockDistance);
    }

    private void HandleCarriedBlockZoom()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        float scrollY =
            mouse.scroll.ReadValue().y;

        if (Mathf.Approximately(scrollY, 0f))
            return;

        float scrollDirection =
            Mathf.Sign(scrollY);

        currentCarriedBlockDistance +=
            scrollDirection *
            carriedBlockScrollSpeed;

        currentCarriedBlockDistance =
            Mathf.Clamp(
                currentCarriedBlockDistance,
                minCarriedBlockDistance,
                maxCarriedBlockDistance
            );
    }

    private float GetInitialCarriedBlockDistance()
    {
        float initialDistance =
            playerFront != null && camera != null
            ? Vector3.Distance(
                camera.position,
                playerFront.position
            )
            : minCarriedBlockDistance;

        return Mathf.Clamp(
            initialDistance,
            minCarriedBlockDistance,
            maxCarriedBlockDistance
        );
    }

    // =========================================================
    // MATERIALS
    // =========================================================

    private void CopyRendererColors(
        Transform sourceBlock,
        Transform duplicatedBlock
    )
    {
        Renderer[] sourceRenderers =
            sourceBlock.GetComponentsInChildren<Renderer>();

        Renderer[] duplicatedRenderers =
            duplicatedBlock.GetComponentsInChildren<Renderer>();

        int rendererCount =
            Mathf.Min(
                sourceRenderers.Length,
                duplicatedRenderers.Length
            );

        for (int i = 0; i < rendererCount; i++)
        {
            Material[] sourceMaterials =
                sourceRenderers[i].materials;

            Material[] duplicatedMaterials =
                duplicatedRenderers[i].materials;

            int materialCount =
                Mathf.Min(
                    sourceMaterials.Length,
                    duplicatedMaterials.Length
                );

            for (int j = 0; j < materialCount; j++)
            {
                CopyMaterialColor(
                    sourceMaterials[j],
                    duplicatedMaterials[j]
                );
            }
        }
    }

    private static void CopyMaterialColor(
        Material sourceMaterial,
        Material duplicatedMaterial
    )
    {
        if (
            sourceMaterial == null ||
            duplicatedMaterial == null
        )
        {
            return;
        }

        if (
            sourceMaterial.HasProperty("_BaseColor") &&
            duplicatedMaterial.HasProperty("_BaseColor")
        )
        {
            duplicatedMaterial.SetColor(
                "_BaseColor",
                sourceMaterial.GetColor("_BaseColor")
            );
        }

        if (
            sourceMaterial.HasProperty("_Color") &&
            duplicatedMaterial.HasProperty("_Color")
        )
        {
            duplicatedMaterial.SetColor(
                "_Color",
                sourceMaterial.GetColor("_Color")
            );
        }
    }

    // =========================================================
    // OPERATOR EQUIP
    // =========================================================

    public void SetEquippedOperator(
        PencilOperator newOperator
    )
    {
        PencilOperator previousOperator =
            equippedOperator;

        equippedOperator = newOperator;

        Debug.Log(
            $"Player trocou operador: {previousOperator} -> {equippedOperator}"
        );
    }

    public void ClearEquippedOperator()
    {
        equippedOperator =
            PencilOperator.None;

        Debug.Log(
            "Player limpou o operador equipado."
        );
    }

    // =========================================================
    // OPERATOR ABSORB
    // =========================================================

    public Transform GetOrCreateOperatorAbsorbTarget()
    {
        if (operatorAbsorbTarget != null)
            return operatorAbsorbTarget;

        Transform targetParent =
            camera != null
            ? camera
            : transform;

        Transform existingTarget =
            targetParent.Find(
                "OperatorAbsorbTarget"
            );

        if (existingTarget != null)
        {
            operatorAbsorbTarget =
                existingTarget;

            return operatorAbsorbTarget;
        }

        GameObject targetObject =
            new GameObject(
                "OperatorAbsorbTarget"
            );

        targetObject.transform.SetParent(
            targetParent,
            false
        );

        targetObject.transform.localPosition =
            GetOperatorAbsorbTargetLocalPosition(
                targetParent
            );

        targetObject.transform.localRotation =
            Quaternion.identity;

        operatorAbsorbTarget =
            targetObject.transform;

        return operatorAbsorbTarget;
    }

    private Vector3 GetOperatorAbsorbTargetLocalPosition(
        Transform targetParent
    )
    {
        if (targetParent == camera)
        {
            return operatorAbsorbTargetCameraLocalPosition;
        }

        return Vector3.forward * 0.9f;
    }

    // =========================================================
    // COOLDOWN
    // =========================================================

    IEnumerator GrabCooldown()
    {
        isOnCooldown = true;

        yield return new WaitForSeconds(
            grabCooldown
        );

        isOnCooldown = false;
        canRaycast = true;
    }
}