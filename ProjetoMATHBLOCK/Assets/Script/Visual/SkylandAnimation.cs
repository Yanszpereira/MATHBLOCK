using UnityEngine;

public class SkylandAnimation : MonoBehaviour
{
    [SerializeField, Min(0f)] private float bobHeight = 0.25f;
    [SerializeField, Min(0f)] private float bobFrequency = 0.35f;

    private float phase;
    private Vector3 previousOffset;

    private void Awake()
    {
        phase = Random.Range(0, 2) * Mathf.PI;
    }

    private void Update()
    {
        float offsetY = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f + phase) * bobHeight;
        Vector3 currentOffset = Vector3.up * offsetY;
        transform.position += currentOffset - previousOffset;
        previousOffset = currentOffset;
    }
}
