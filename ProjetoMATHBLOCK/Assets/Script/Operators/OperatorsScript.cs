using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;

public class OperatorsScript : MonoBehaviour
{
    [Header("Interação")]
    public Transform playerVision;
    public float grabDistance = 3f;
    public GravityInteract pencilGun;

    [SerializeField] private Transform operatorAbsorbTarget;

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

    public void OnInteractOperatorEvent(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

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

        RaycastHit hit;

        if (!Physics.Raycast(playerVision.position, playerVision.forward, out hit, grabDistance))
            return;

        if (!hit.collider.TryGetComponent<opItem>(out var item))
            return;

        if (equippedSceneOperator != null && equippedSceneOperator != item)
        {
            equippedSceneOperator.RestoreToScene();
        }

        pencilGun.SetEquippedOperator(item.operatorType);

        PlaySelectionSound(item.operatorType, item.transform.position);

        item.ConsumeFromScene(GetAbsorbTarget());

        equippedSceneOperator = item;
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
            Debug.LogWarning($"Som FMOD nao configurado para o operador {operatorType}.");
            return;
        }

        lastSelectionSoundTime = Time.time;

        RuntimeManager.PlayOneShot(selectedSound, soundPosition);
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