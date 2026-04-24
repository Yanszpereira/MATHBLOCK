using UnityEngine;

public class opItem : MonoBehaviour
{
    public GravityInteract.PencilOperator operatorType;

    private Vector3 originalPosition;
    private Quaternion originalRotation;

    private void Awake()
    {
        originalPosition = transform.position;
        originalRotation = transform.rotation;
    }

    public void ConsumeFromScene()
    {
        gameObject.SetActive(false);
    }

    public void RestoreToScene()
    {
        transform.SetPositionAndRotation(originalPosition, originalRotation);
        gameObject.SetActive(true);
    }
}
