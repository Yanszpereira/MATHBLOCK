using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class VoidRespawner : MonoBehaviour
{
    private const string VoidTag = "Void";
    private const string GroundTag = "Ground";

    [Header("Respawn")]
    [SerializeField] private Vector3 respawnCenter = Vector3.zero;
    [SerializeField] private Vector3 playerRespawnOffset = new Vector3(0f, 10f, 0f);
    [SerializeField] private Vector3 blockRespawnOffset = new Vector3(0f, 10f, 0f);
    [SerializeField] private bool resetBlockRotation = true;
    [SerializeField] private float respawnCooldown = 0.25f;
    [SerializeField] private int groundSampleAttempts = 32;
    [SerializeField] private float groundRaycastHeight = 50f;

    [Header("Fallback Trigger")]
    [SerializeField] private Vector3 fallbackTriggerSize = new Vector3(120f, 8f, 120f);

    private readonly Dictionary<int, float> lastRespawnTimes = new Dictionary<int, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallOnTaggedVoids()
    {
        GameObject[] voidObjects;
        try
        {
            voidObjects = GameObject.FindGameObjectsWithTag(VoidTag);
        }
        catch (UnityException)
        {
            return;
        }

        for (int index = 0; index < voidObjects.Length; index++)
        {
            GameObject voidObject = voidObjects[index];
            if (voidObject != null && !voidObject.TryGetComponent(out VoidRespawner _))
            {
                voidObject.AddComponent<VoidRespawner>();
            }
        }
    }

    private void Awake()
    {
        ConfigureTrigger();
    }

    private void Reset()
    {
        ConfigureTrigger();
    }

    private void OnValidate()
    {
        Collider[] colliders = GetComponents<Collider>();
        for (int index = 0; index < colliders.Length; index++)
        {
            colliders[index].isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRespawn(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryRespawn(other);
    }

    private void ConfigureTrigger()
    {
        Collider[] colliders = GetComponents<Collider>();
        if (colliders.Length == 0)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.size = fallbackTriggerSize;
            boxCollider.isTrigger = true;
        }
        else
        {
            for (int index = 0; index < colliders.Length; index++)
            {
                colliders[index].isTrigger = true;
            }
        }

        if (!TryGetComponent(out Rigidbody rb))
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void TryRespawn(Collider other)
    {
        if (other == null)
            return;

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();
        if (player != null)
        {
            RespawnPlayer(player);
            return;
        }

        MathBlockValue block = other.GetComponentInParent<MathBlockValue>();
        if (block != null)
        {
            RespawnBlock(block);
        }
    }

    private bool CanRespawn(int instanceId)
    {
        if (lastRespawnTimes.TryGetValue(instanceId, out float lastTime) && Time.time - lastTime < respawnCooldown)
            return false;

        lastRespawnTimes[instanceId] = Time.time;
        return true;
    }

    private void RespawnPlayer(PlayerMovement player)
    {
        int instanceId = player.gameObject.GetInstanceID();
        if (!CanRespawn(instanceId))
            return;

        Vector3 targetPosition = GetRespawnPosition(playerRespawnOffset);
        CharacterController controller = player.controller != null
            ? player.controller
            : player.GetComponent<CharacterController>();

        player.ResetVerticalMovement();

        if (controller != null)
        {
            controller.enabled = false;
            controller.transform.position = targetPosition;
            controller.enabled = true;
        }
        else
        {
            player.transform.position = targetPosition;
        }

        if (player.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();
    }

    private void RespawnBlock(MathBlockValue block)
    {
        int instanceId = block.gameObject.GetInstanceID();
        if (!CanRespawn(instanceId))
            return;

        Vector3 targetPosition = GetRespawnPosition(blockRespawnOffset);
        Transform blockTransform = block.transform;

        if (block.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = targetPosition;
            if (resetBlockRotation)
            {
                rb.rotation = Quaternion.identity;
            }

            rb.WakeUp();
        }
        else
        {
            blockTransform.position = targetPosition;
            if (resetBlockRotation)
            {
                blockTransform.rotation = Quaternion.identity;
            }
        }

        Physics.SyncTransforms();
    }

    private Vector3 GetRespawnPosition(Vector3 offset)
    {
        if (TryGetRandomGroundPoint(out Vector3 groundPoint))
        {
            return groundPoint + offset;
        }

        return respawnCenter + offset;
    }

    private bool TryGetRandomGroundPoint(out Vector3 point)
    {
        point = respawnCenter;

        GameObject[] groundObjects;
        try
        {
            groundObjects = GameObject.FindGameObjectsWithTag(GroundTag);
        }
        catch (UnityException)
        {
            return false;
        }

        List<Collider> groundColliders = new List<Collider>();
        HashSet<Collider> groundColliderSet = new HashSet<Collider>();
        Bounds groundBounds = default;
        bool hasBounds = false;

        for (int objectIndex = 0; objectIndex < groundObjects.Length; objectIndex++)
        {
            Collider[] colliders = groundObjects[objectIndex].GetComponentsInChildren<Collider>();
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                Collider groundCollider = colliders[colliderIndex];
                if (groundCollider == null || groundCollider.isTrigger)
                    continue;

                Bounds bounds = groundCollider.bounds;
                groundColliders.Add(groundCollider);
                groundColliderSet.Add(groundCollider);

                if (hasBounds)
                {
                    groundBounds.Encapsulate(bounds);
                }
                else
                {
                    groundBounds = bounds;
                    hasBounds = true;
                }
            }
        }

        if (!hasBounds || groundColliders.Count == 0)
            return false;

        float rayDistance = groundBounds.size.y + groundRaycastHeight * 2f;
        for (int attempt = 0; attempt < groundSampleAttempts; attempt++)
        {
            float x = Random.Range(groundBounds.min.x, groundBounds.max.x);
            float z = Random.Range(groundBounds.min.z, groundBounds.max.z);
            Vector3 rayOrigin = new Vector3(x, groundBounds.max.y + groundRaycastHeight, z);

            RaycastHit[] hits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                rayDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            float nearestDistance = float.MaxValue;
            bool foundGround = false;
            Vector3 selectedPoint = point;
            for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
            {
                RaycastHit hit = hits[hitIndex];
                if (!groundColliderSet.Contains(hit.collider) || hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                selectedPoint = hit.point;
                foundGround = true;
            }

            if (foundGround)
            {
                point = selectedPoint;
                return true;
            }
        }

        Collider fallbackCollider = groundColliders[Random.Range(0, groundColliders.Count)];
        if (fallbackCollider != null)
        {
            Bounds bounds = fallbackCollider.bounds;
            point = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            return true;
        }

        return false;
    }
}
