using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class VoidRespawner : MonoBehaviour
{
    private const string VoidTag = "Void";
    private const string GroundTag = "Ground";

    [Header("Respawn")]
    [SerializeField] private Vector3 respawnCenter = Vector3.zero;
    [SerializeField] private Vector3 playerRespawnOffset = new Vector3(0f, 5f, 0f);
    [SerializeField] private Vector3 blockRespawnOffset = new Vector3(0f, 10f, 0f);
    [SerializeField] private bool resetBlockRotation = true;
    [SerializeField] private float respawnCooldown = 0.25f;
    [SerializeField] private int groundSampleAttempts = 32;
    [SerializeField] private float groundRaycastHeight = 50f;

    [Header("Player Fade")]
    [SerializeField, Min(0.05f)] private float playerFadeOutDuration = 0.3f;
    [SerializeField, Min(0.05f)] private float playerFadeInDuration = 0.45f;
    [SerializeField, Min(0f)] private float blackScreenHoldDuration = 0.08f;
    [SerializeField] private Color playerFadeColor = Color.black;

    [Header("Fallback Trigger")]
    [SerializeField] private Vector3 fallbackTriggerSize = new Vector3(120f, 8f, 120f);

    private readonly Dictionary<int, float> lastRespawnTimes = new Dictionary<int, float>();
    private readonly HashSet<int> respawningPlayers = new HashSet<int>();
    private Vector3 initialPlayerPosition;
    private Quaternion initialPlayerRotation;
    private bool hasCapturedPlayerSpawn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadedHandler()
    {
        GlobalSceneBootstrap.Register(InstallOnTaggedVoids);
    }

    private static void InstallOnTaggedVoids(Scene scene)
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

    private void Start()
    {
        CaptureInitialPlayerSpawn();
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
        if (respawningPlayers.Contains(instanceId) || !CanRespawn(instanceId))
            return;

        respawningPlayers.Add(instanceId);
        StartCoroutine(RespawnPlayerWithFade(player, instanceId));
    }

    private IEnumerator RespawnPlayerWithFade(PlayerMovement player, int instanceId)
    {
        CanvasGroup fade = CreatePlayerFadeOverlay();
        yield return Fade(fade, 0f, 1f, playerFadeOutDuration);

        if (player != null)
            TeleportPlayer(player);

        if (blackScreenHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(blackScreenHoldDuration);

        yield return Fade(fade, 1f, 0f, playerFadeInDuration);
        if (fade != null)
            Destroy(fade.gameObject);

        respawningPlayers.Remove(instanceId);
    }

    private void TeleportPlayer(PlayerMovement player)
    {
        if (player == null)
            return;

        if (!hasCapturedPlayerSpawn)
            CaptureInitialPlayerSpawn(player);

        Vector3 targetPosition = hasCapturedPlayerSpawn
            ? initialPlayerPosition
            : GetRespawnPosition(playerRespawnOffset);
        CharacterController controller = player.controller != null
            ? player.controller
            : player.GetComponent<CharacterController>();

        player.ResetVerticalMovement();

        if (controller != null)
        {
            controller.enabled = false;
            controller.transform.SetPositionAndRotation(targetPosition, initialPlayerRotation);
            controller.enabled = true;
        }
        else
        {
            player.transform.SetPositionAndRotation(targetPosition, initialPlayerRotation);
        }

        if (player.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();
    }

    private CanvasGroup CreatePlayerFadeOverlay()
    {
        GameObject canvasObject = new GameObject(
            "Void Respawn Fade",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasGroup group = canvasObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = true;
        group.blocksRaycasts = true;

        GameObject imageObject = new GameObject("Fade Color", typeof(RectTransform), typeof(Image));
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.SetParent(canvasObject.transform, false);
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.color = playerFadeColor;
        image.raycastTarget = true;
        return group;
    }

    private static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
            yield break;

        duration = Mathf.Max(0.05f, duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = normalized * normalized * (3f - 2f * normalized);
            group.alpha = Mathf.Lerp(from, to, eased);
            yield return null;
        }

        group.alpha = to;
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

        Vector3 fallbackCenter = hasCapturedPlayerSpawn ? initialPlayerPosition : respawnCenter;
        return fallbackCenter + offset;
    }

    private void CaptureInitialPlayerSpawn()
    {
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
            CaptureInitialPlayerSpawn(player);
    }

    private void CaptureInitialPlayerSpawn(PlayerMovement player)
    {
        if (player == null || hasCapturedPlayerSpawn)
            return;

        initialPlayerPosition = player.transform.position;
        initialPlayerRotation = player.transform.rotation;
        hasCapturedPlayerSpawn = true;
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
