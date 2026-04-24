using UnityEngine;
using UnityEngine.InputSystem;

public class OperatorsScript : MonoBehaviour
{
    public Transform playerVision;   // câmera / direção do raycast
    public Transform playerPosition; // onde vai spawnar depois
    public float grabDistance = 3f;

    private GameObject inventarioPrefab; // GUARDA O ITEM
    private GameObject prefab;

    public void OnInteractOperatorEvent(InputAction.CallbackContext context)
    {
        if (!context.performed) return;

        RaycastHit hit;

        if (Physics.Raycast(playerVision.position, playerVision.forward, out hit, grabDistance))
        {
            // 👇 verifica se é pegável
            if (hit.collider.TryGetComponent<opItem>(out var item))
            {
                if(inventarioPrefab == null)
                {
                    prefab = item.prefabOriginal;

                    Destroy(item.gameObject);

                    inventarioPrefab = prefab;

                    Debug.Log(prefab.name);
                }

                if(inventarioPrefab != null)
                {
                   Instantiate(prefab, playerPosition.position, Quaternion.identity);
                   inventarioPrefab = null;
                }
            }
        }
    }
}