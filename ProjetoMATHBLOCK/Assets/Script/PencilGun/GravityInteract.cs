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

    public float grabDistance = 10f;
    public float speed = 5f;
    public float grabCooldown = 0.3f; // tempo de espera após soltar

    public Transform camera;
    public Transform playerFront; // ponto na frente do player

    [SerializeField] private PencilOperator equippedOperator = PencilOperator.None;
    [SerializeField] private float duplicateSpawnHeight = 1.5f;
    [SerializeField] private float releaseVelocityMultiplier = 1f;
    [SerializeField] private float maxReleaseSpeed = 18f;
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
    private Vector3 carriedVelocity;
    private Vector3 lastCarriedPosition;
    private bool hasLastCarriedPosition;

    public PencilOperator EquippedOperator => equippedOperator;
    public Transform OperatorAbsorbTarget => GetOrCreateOperatorAbsorbTarget();

    private void Awake()
    {
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
        Debug.DrawRay(
            camera.position,
            camera.forward * grabDistance,
            Color.red
        );

        // se estiver segurando um objeto
        if (grabbed && grabbedObject != null)
        {
            Vector3 targetPosition = GetCarriedTargetPosition();
            grabbedObject.position = Vector3.Lerp(
                grabbedObject.position,
                targetPosition,
                Time.deltaTime * speed
            );

            UpdateCarriedVelocity();
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
        if (!Physics.Raycast(camera.position, camera.forward, out hit, grabDistance))
            return false;

        return hit.collider.CompareTag("MathBlock");
    }

    private void HandleOperatorApplication(RaycastHit hit)
    {
        var targetBlock = hit.collider.GetComponent<MathBlockValue>();
        var carriedBlock = grabbedObject.GetComponent<MathBlockValue>();
        if (carriedBlock == null)
        {
            Debug.LogWarning($"Bloco carregado {grabbedObject.name} nao possui MathBlockValue.");
            return;
        }

        if (targetBlock == null)
        {
            targetBlock = hit.collider.gameObject.AddComponent<MathBlockValue>();
        }

        int targetValue = targetBlock.CurrentValue;

        if (targetBlock.TryApplyOperator(equippedOperator, carriedBlock))
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
                $"Operacao invalida: {targetValue} {equippedOperator} {carriedBlock.CurrentValue} no bloco {hit.collider.name}"
            );
        }
    }

    private void DuplicateBlock(Transform sourceBlock)
    {
        if (sourceBlock == null)
            return;

        Vector3 spawnPosition = sourceBlock.position + Vector3.up * duplicateSpawnHeight;
        GameObject duplicatedBlock = Instantiate(sourceBlock.gameObject, spawnPosition, sourceBlock.rotation);
        duplicatedBlock.name = $"{sourceBlock.name}_Clone";
        CopyRendererColors(sourceBlock, duplicatedBlock.transform);

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
        Debug.Log("Player limpou o operador equipado.");
    }

    public Transform GetOrCreateOperatorAbsorbTarget()
    {
        if (operatorAbsorbTarget != null)
            return operatorAbsorbTarget;

        Transform targetParent = camera != null ? camera : transform;
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
        if (targetParent == camera)
            return operatorAbsorbTargetCameraLocalPosition;

        return Vector3.forward * 0.9f;
    }

    public void Pegar(RaycastHit hit)
    {
        grabbedRb = hit.transform.GetComponent<Rigidbody>();
        grabbedObject = hit.transform;
        MathBlockValue mathBlockValue = hit.transform.GetComponent<MathBlockValue>();

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
        canRaycast = false;
        carriedVelocity = Vector3.zero;
        lastCarriedPosition = grabbedObject.position;
        hasLastCarriedPosition = true;
        Debug.Log($"Bloco segurado: {grabbedObject.name}");
    }

    public void Soltar()
    {
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
        grabbed = false;
        carriedVelocity = Vector3.zero;
        hasLastCarriedPosition = false;

        canRaycast = false;
        StartCoroutine(GrabCooldown());
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
        if (playerFront == null)
            return grabbedObject.position;

        Vector3 targetPosition = playerFront.position;
        if (camera == null)
            return targetPosition;

        float verticalOffset = Mathf.Max(0f, camera.forward.y) * Vector3.Distance(camera.position, playerFront.position);
        targetPosition.y = playerFront.position.y + verticalOffset;
        return targetPosition;
    }

    IEnumerator GrabCooldown()
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(grabCooldown);
        isOnCooldown = false;
        canRaycast = true;
    }
}
