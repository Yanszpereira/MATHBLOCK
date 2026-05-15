using UnityEngine;

public class opItem : MonoBehaviour
{
    public GravityInteract.PencilOperator operatorType;

    [Header("Visual Wobble")]
    [SerializeField] private bool useWobble = true;
    [SerializeField] private float wobbleAmplitude = 0.15f;
    [SerializeField] private float wobbleSpeed = 1.5f;

    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private float wobbleTimeOffset;

    private void Awake()
    {
        originalPosition = transform.position;
        originalRotation = transform.rotation;
        wobbleTimeOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (!useWobble)
            return;

        float verticalOffset = Mathf.Sin((Time.time * wobbleSpeed) + wobbleTimeOffset) * wobbleAmplitude;
        transform.position = originalPosition + Vector3.up * verticalOffset;
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
