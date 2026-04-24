using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class GravityInteract : MonoBehaviour
{
    public float grabDistance = 10f;
    public float speed = 5f;
    public float grabCooldown = 0.3f; // tempo de espera após soltar

    public Transform camera;
    public Transform playerFront; // ponto na frente do player 

    private bool grabbed;
    private bool canRaycast = true;
    private bool isOnCooldown;

    private Transform grabbedObject;
    private Rigidbody grabbedRb;

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
            grabbedObject.position = Vector3.Lerp(
                grabbedObject.position,
                playerFront.position,
                Time.deltaTime * speed
            );
        }
    }

    public void OnInteractEvent(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        // bloqueia qualquer ação durante cooldown
        if (isOnCooldown)
            return;

        // se já estiver segurando, apenas solta
        if (grabbed)
        {
            Soltar();
            return; // impede pegar no mesmo input
        }

        // tenta pegar
        if (canRaycast)
        {
            RaycastHit hit;
            if (Physics.Raycast(camera.position, camera.forward, out hit, grabDistance))
            {
                if (hit.collider.CompareTag("MathBlock"))
                {
                    Pegar(hit);
                }
            }
        }
    }

    public void Pegar(RaycastHit hit)
    {
        grabbedRb = hit.transform.GetComponent<Rigidbody>();
        grabbedObject = hit.transform;

        if (grabbedRb != null)
        {
            grabbed = true;
            grabbedRb.useGravity = false;
            grabbedRb.isKinematic = true;
        }

        canRaycast = false;
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