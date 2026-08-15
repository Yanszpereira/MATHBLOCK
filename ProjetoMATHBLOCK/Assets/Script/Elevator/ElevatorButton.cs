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

    private ElevatorTotemController controller;
    private InputAction interactAction;
    private Camera playerCamera;
    private Transform buttonPart;
    private Vector3 buttonRestLocalPosition;
    private Coroutine pressAnimation;

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
