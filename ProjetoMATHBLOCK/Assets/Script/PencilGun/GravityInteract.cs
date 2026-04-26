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

    private bool grabbed;
    private bool canRaycast = true;
    private bool isOnCooldown;

    private Transform grabbedObject;
    private Rigidbody grabbedRb;

    public PencilOperator EquippedOperator => equippedOperator;

    void Update()
    {
        Debug.DrawRay(
            camera.position,
            camera.forward * grabDistance,
            Color.red
        );

        HandleBlockInputs();

        // se estiver segurando um objeto
        if (grabbed && grabbedObject != null)
        {
            grabbedObject.position = Vector3.Lerp(
                grabbedObject.position,
                playerFront.position,
                Time.deltaTime * speed
            );
        }
    }

    private void HandleBlockInputs()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            TryHandleApplyOperator();
        }
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

        int carriedValue = carriedBlock.CurrentValue;
        int targetValue = targetBlock.CurrentValue;

        if (targetBlock.TryApplyOperator(equippedOperator, carriedValue))
        {
            Debug.Log(
                $"Operacao concluida: {targetValue} {equippedOperator} {carriedValue} = {targetBlock.CurrentValue}"
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
                $"Operacao invalida: {targetValue} {equippedOperator} {carriedValue} no bloco {hit.collider.name}"
            );
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
        Debug.Log($"Bloco segurado: {grabbedObject.name}");
    }

    public void Soltar()
    {
        if (grabbedRb != null)
        {
            grabbedRb.useGravity = true;
            grabbedRb.isKinematic = false;
        }

        grabbedRb = null;
        grabbedObject = null;
        grabbed = false;

        canRaycast = false;
        StartCoroutine(GrabCooldown());
    }

    IEnumerator GrabCooldown()
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(grabCooldown);
        isOnCooldown = false;
        canRaycast = true;
    }
}
