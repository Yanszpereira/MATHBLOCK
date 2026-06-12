using System.Collections.Generic;
using UnityEngine;

public class CloudSpawnerManager : MonoBehaviour
{
    public enum Axis
    {
        X,
        Y,
        Z
    }

    [SerializeField] private GameObject cloudPrefab;
    [SerializeField] private float minSpawnInterval = 2f;
    [SerializeField] private float maxSpawnInterval = 5f;
    [SerializeField] private int maxClouds = 10;
    [SerializeField] private Axis spawnVariationAxis = Axis.Z;
    [SerializeField] private float spawnRange = 10f;
    [SerializeField] private float upwardSpawnRange = 5f;
    [SerializeField] private Axis despawnCheckAxis = Axis.X;
    [SerializeField] private float despawnLimitValue = 30f;
    [SerializeField] private bool despawnWhenGreaterThan = true;

    private readonly List<GameObject> spawnedClouds = new List<GameObject>();
    private float spawnTimer;
    private float nextSpawnTime;
    private bool hasWarnedMissingPrefab;

    private void Awake()
    {
        ScheduleNextSpawn();
    }

    private void Update()
    {
        spawnTimer += Time.deltaTime;

        if (spawnTimer >= nextSpawnTime)
        {
            spawnTimer = 0f;
            ScheduleNextSpawn();
            TrySpawnCloud();
        }

        DespawnCloudsPastLimit();
    }

    private void TrySpawnCloud()
    {
        if (cloudPrefab == null)
        {
            if (!hasWarnedMissingPrefab)
            {
                Debug.LogWarning($"{nameof(CloudSpawnerManager)} on {name} has no cloud prefab assigned.", this);
                hasWarnedMissingPrefab = true;
            }

            return;
        }

        RemoveNullCloudReferences();

        if (spawnedClouds.Count >= maxClouds)
        {
            return;
        }

        Vector3 spawnPosition = transform.position;
        float offset = Random.Range(-spawnRange, spawnRange);
        SetAxisValue(ref spawnPosition, spawnVariationAxis, GetAxisValue(spawnPosition, spawnVariationAxis) + offset);
        spawnPosition.y += Random.Range(0f, upwardSpawnRange);

        GameObject cloud = Instantiate(cloudPrefab, spawnPosition, cloudPrefab.transform.rotation);
        spawnedClouds.Add(cloud);
    }

    private void DespawnCloudsPastLimit()
    {
        for (int i = spawnedClouds.Count - 1; i >= 0; i--)
        {
            GameObject cloud = spawnedClouds[i];

            if (cloud == null)
            {
                spawnedClouds.RemoveAt(i);
                continue;
            }

            float axisPosition = GetAxisValue(cloud.transform.position, despawnCheckAxis);
            bool shouldDespawn = despawnWhenGreaterThan
                ? axisPosition > despawnLimitValue
                : axisPosition < despawnLimitValue;

            if (shouldDespawn)
            {
                CloudMover cloudMover = cloud.GetComponent<CloudMover>();

                if (cloudMover != null)
                {
                    cloudMover.FadeOutAndDestroy();
                }
                else
                {
                    Destroy(cloud);
                }

                spawnedClouds.RemoveAt(i);
            }
        }
    }

    private void RemoveNullCloudReferences()
    {
        for (int i = spawnedClouds.Count - 1; i >= 0; i--)
        {
            if (spawnedClouds[i] == null)
            {
                spawnedClouds.RemoveAt(i);
            }
        }
    }

    private static float GetAxisValue(Vector3 position, Axis axis)
    {
        switch (axis)
        {
            case Axis.X:
                return position.x;
            case Axis.Y:
                return position.y;
            case Axis.Z:
                return position.z;
            default:
                return position.x;
        }
    }

    private static void SetAxisValue(ref Vector3 position, Axis axis, float value)
    {
        switch (axis)
        {
            case Axis.X:
                position.x = value;
                break;
            case Axis.Y:
                position.y = value;
                break;
            case Axis.Z:
                position.z = value;
                break;
        }
    }

    private void OnValidate()
    {
        maxClouds = Mathf.Max(1, maxClouds);
        minSpawnInterval = Mathf.Max(0.1f, minSpawnInterval);
        maxSpawnInterval = Mathf.Max(0.1f, maxSpawnInterval);

        if (maxSpawnInterval < minSpawnInterval)
        {
            maxSpawnInterval = minSpawnInterval;
        }

        spawnRange = Mathf.Max(0f, spawnRange);
        upwardSpawnRange = Mathf.Max(0f, upwardSpawnRange);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 spawnStart = transform.position;
        Vector3 spawnEnd = transform.position;

        SetAxisValue(ref spawnStart, spawnVariationAxis, GetAxisValue(spawnStart, spawnVariationAxis) - spawnRange);
        SetAxisValue(ref spawnEnd, spawnVariationAxis, GetAxisValue(spawnEnd, spawnVariationAxis) + spawnRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(spawnStart, spawnEnd);
        Gizmos.DrawWireSphere(spawnStart, 0.4f);
        Gizmos.DrawWireSphere(spawnEnd, 0.4f);

        Vector3 upwardSpawnEnd = transform.position + Vector3.up * upwardSpawnRange;
        Gizmos.DrawLine(transform.position, upwardSpawnEnd);
        Gizmos.DrawWireSphere(upwardSpawnEnd, 0.4f);

        Vector3 despawnCenter = transform.position;
        SetAxisValue(ref despawnCenter, despawnCheckAxis, despawnLimitValue);

        Vector3 despawnStart = despawnCenter;
        Vector3 despawnEnd = despawnCenter;
        Axis lineAxis = GetPerpendicularAxis(despawnCheckAxis);
        SetAxisValue(ref despawnStart, lineAxis, GetAxisValue(despawnStart, lineAxis) - spawnRange);
        SetAxisValue(ref despawnEnd, lineAxis, GetAxisValue(despawnEnd, lineAxis) + spawnRange);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(despawnStart, despawnEnd);
        Gizmos.DrawWireCube(despawnCenter, Vector3.one);
    }

    private static Axis GetPerpendicularAxis(Axis axis)
    {
        return axis == Axis.Z ? Axis.X : Axis.Z;
    }

    private void ScheduleNextSpawn()
    {
        nextSpawnTime = Random.Range(minSpawnInterval, maxSpawnInterval);
    }
}
