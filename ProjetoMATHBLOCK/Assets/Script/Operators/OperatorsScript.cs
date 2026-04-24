using UnityEngine;
using UnityEngine.InputSystem;

public class OperatorsScript : MonoBehaviour
{
    public Transform playerVision;   // camera / direcao do raycast
    public float grabDistance = 3f;
    public GravityInteract pencilGun;

    private opItem equippedSceneOperator;

    public void OnInteractOperatorEvent(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        if (pencilGun == null)
        {
            Debug.LogWarning("OperatorsScript sem referencia para GravityInteract.");
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
        item.ConsumeFromScene();
        equippedSceneOperator = item;
    }
}
