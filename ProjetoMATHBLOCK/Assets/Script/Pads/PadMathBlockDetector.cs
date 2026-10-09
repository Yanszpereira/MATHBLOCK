using System.Collections.Generic;
using UnityEngine;
using FMODUnity;

[RequireComponent(typeof(Collider))]
public class PadMathBlockDetector : MonoBehaviour
{
    private const string DefaultMathBlockTag = "MathBlock";

    [Header("Detecção")]
    [SerializeField] private string mathBlockTag = DefaultMathBlockTag;
    [SerializeField] private bool acceptExistingProjectTag = true;
    [SerializeField] private GameObject connectedVerifierObject;
    [Header("Detecção por volume")]
    [SerializeField, Min(0.1f)] private float detectionHeight = 2f;


    [Header("Valor esperado")]
    [SerializeField] private int expectedValue = 0;
    [SerializeField] private bool playErrorSoundWhenWrong = true;

    [Header("Som de erro")]
    [SerializeField] private EventReference errorSound;
    [SerializeField] private float errorSoundCooldown = 0.35f;

    private readonly Dictionary<Collider, int> detectedBlocks = new Dictionary<Collider, int>();
    private readonly Dictionary<Collider, MathBlockValue> detectedBlockComponents =
        new Dictionary<Collider, MathBlockValue>();

    private bool lastReportedHasValue;
    private int lastReportedValue;
    private MathBlockValue lastReportedBlock;

    private DoorValueVerifier connectedVerifier;

    /// <summary>
    /// Raised after a MathBlock value is read. Door pads keep using their
    /// DoorValueVerifier, while other systems (such as elevator totems) can
    /// listen without pretending to be a door.
    /// </summary>
    public event System.Action<GameObject, int, GameObject> ValueDetected;

    /// <summary>
    /// Raised whenever this pad changes between empty, ambiguous, or one stable block value.
    /// A null value means that no single unambiguous MathBlock is currently detected.
    /// </summary>
    public event System.Action<PadMathBlockDetector, int?> CurrentValueChanged;

    public bool HasMultipleBlocks => CountUniqueDetectedBlocks() > 1;

    public bool TryGetCurrentValue(out int value)
    {
        MathBlockValue block = GetSingleDetectedBlock();
        if (block == null)
        {
            value = 0;
            return false;
        }

        value = block.CurrentValue;
        return true;
    }

    /// <summary>Returns the sole unambiguous block currently detected by the pad volume.</summary>
    public bool TryGetCurrentBlock(out MathBlockValue block)
    {
        block = GetSingleDetectedBlock();
        return block != null;
    }

    /// <summary>Used by the Fase 4 receptacle to release duplicate-submit locks after a block leaves its volume.</summary>
    public bool IsBlockCurrentlyDetected(MathBlockValue block)
    {
        if (block == null)
            return false;

        foreach (MathBlockValue detected in detectedBlockComponents.Values)
            if (detected == block)
                return true;

        return false;
    }

    private float lastErrorSoundTime = -999f;

    private void Reset()
    {
        NormalizeMathBlockTag();

        Collider padCollider = GetComponent<Collider>();
        if (padCollider != null)
        {
            padCollider.isTrigger = false;
        }
    }

    private void Awake()
    {
        NormalizeMathBlockTag();
        CacheConnectedVerifier();
    }

    private void FixedUpdate()
    {
        RefreshDetectedBlocksFromVolume();
    }

    private void RefreshDetectedBlocksFromVolume()
    {
        Collider padCollider = GetComponent<Collider>();
        if (padCollider == null || !padCollider.enabled)
            return;

        Bounds bounds = padCollider.bounds;
        float height = Mathf.Max(0.1f, detectionHeight);
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.01f, bounds.extents.x),
            height * 0.5f,
            Mathf.Max(0.01f, bounds.extents.z));
        Vector3 center = new Vector3(
            bounds.center.x,
            bounds.max.y + halfExtents.y,
            bounds.center.z);

        Collider[] overlaps = Physics.OverlapBox(
            center,
            halfExtents,
            Quaternion.identity,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        HashSet<Collider> currentColliders = new HashSet<Collider>();
        foreach (Collider overlap in overlaps)
        {
            if (overlap == null || overlap == padCollider ||
                overlap.transform.IsChildOf(transform) || !IsMathBlock(overlap))
            {
                continue;
            }

            MathBlockValue blockValue = overlap.GetComponent<MathBlockValue>()
                ?? overlap.GetComponentInParent<MathBlockValue>();
            if (blockValue == null)
                continue;

            currentColliders.Add(overlap);
            detectedBlocks[overlap] = blockValue.CurrentValue;
            detectedBlockComponents[overlap] = blockValue;
        }

        List<Collider> staleColliders = new List<Collider>();
        foreach (Collider trackedCollider in detectedBlockComponents.Keys)
        {
            if (trackedCollider == null || !currentColliders.Contains(trackedCollider))
                staleColliders.Add(trackedCollider);
        }

        foreach (Collider staleCollider in staleColliders)
        {
            detectedBlocks.Remove(staleCollider);
            detectedBlockComponents.Remove(staleCollider);
        }

        NotifyCurrentValueIfChanged();
    }

    private void OnValidate()
    {
        NormalizeMathBlockTag();

        Collider padCollider = GetComponent<Collider>();
        if (padCollider != null && padCollider.isTrigger)
        {
            padCollider.isTrigger = false;
        }
    }

    public void SetConnectedVerifier(DoorValueVerifier verifier)
    {
        connectedVerifier = verifier;
        connectedVerifierObject = verifier != null ? verifier.gameObject : null;
    }

    public void SetExpectedValue(int value)
    {
        expectedValue = value;
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryPrintBlockValue(collision.collider, forcePrint: true);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryPrintBlockValue(collision.collider, forcePrint: false);
    }

    private void OnCollisionExit(Collision collision)
    {
        detectedBlocks.Remove(collision.collider);
        detectedBlockComponents.Remove(collision.collider);
        NotifyCurrentValueIfChanged();
    }

    private void TryPrintBlockValue(Collider other, bool forcePrint)
    {
        if (!IsMathBlock(other))
            return;

        MathBlockValue blockValue = other.GetComponent<MathBlockValue>();

        if (blockValue == null)
        {
            blockValue = other.GetComponentInParent<MathBlockValue>();
        }

        if (blockValue == null)
        {
            Debug.LogWarning($"Pad {name} detectou {other.name}, mas ele nao possui MathBlockValue.");
            return;
        }

        int value = blockValue.CurrentValue;
        bool valueUnchanged =
            detectedBlocks.TryGetValue(other, out int lastValue) && lastValue == value;

        detectedBlocks[other] = value;
        detectedBlockComponents[other] = blockValue;
        NotifyCurrentValueIfChanged();

        if (!forcePrint && valueUnchanged)
            return;

        Debug.Log($"Pad {name} detectou bloco {blockValue.name} com valor {value}.");

        DoorValueVerifier verifier = GetConnectedVerifier();

        // Quando existe um DoorValueVerifier conectado, ele e a autoridade
        // para decidir se o valor esta certo ou errado e tambem para tocar
        // os sons de acerto/erro. Isso evita o Pad tocar um erro baseado no
        // expectedValue local ao mesmo tempo em que a porta aceita o valor.
        if (verifier == null && playErrorSoundWhenWrong && value != expectedValue)
        {
            PlayErrorSound();
        }

        ValueDetected?.Invoke(gameObject, value, blockValue.gameObject);

        if (verifier == null)
        {
            if (ValueDetected == null && CurrentValueChanged == null)
            {
                Debug.LogWarning($"Pad {name} detectou valor {value}, mas nao possui consumidor conectado.");
            }
            return;
        }

        verifier.ReceiveValueFromPad(gameObject, value, blockValue.gameObject);
    }

    private void OnDisable()
    {
        bool hadTrackedBlocks = detectedBlockComponents.Count > 0;
        detectedBlocks.Clear();
        detectedBlockComponents.Clear();

        if (hadTrackedBlocks)
            NotifyCurrentValueIfChanged();
    }

    private void NotifyCurrentValueIfChanged()
    {
        MathBlockValue block = GetSingleDetectedBlock();
        bool hasValue = block != null;
        int value = hasValue ? block.CurrentValue : 0;

        if (lastReportedHasValue == hasValue
            && (!hasValue || (lastReportedBlock == block && lastReportedValue == value)))
        {
            return;
        }

        lastReportedHasValue = hasValue;
        lastReportedBlock = block;
        lastReportedValue = value;

        CurrentValueChanged?.Invoke(this, hasValue ? value : (int?)null);
    }

    private MathBlockValue GetSingleDetectedBlock()
    {
        MathBlockValue singleBlock = null;

        foreach (KeyValuePair<Collider, MathBlockValue> entry in detectedBlockComponents)
        {
            MathBlockValue block = entry.Value;
            if (block == null)
                continue;

            if (singleBlock == null)
            {
                singleBlock = block;
                continue;
            }

            if (singleBlock != block)
                return null;
        }

        return singleBlock;
    }

    private int CountUniqueDetectedBlocks()
    {
        HashSet<MathBlockValue> uniqueBlocks = new HashSet<MathBlockValue>();

        foreach (KeyValuePair<Collider, MathBlockValue> entry in detectedBlockComponents)
        {
            if (entry.Value != null)
                uniqueBlocks.Add(entry.Value);
        }

        return uniqueBlocks.Count;
    }

    private void PlayErrorSound()
    {
        if (errorSound.IsNull)
        {
            Debug.LogWarning($"Pad {name} tentou tocar som de erro, mas nenhum evento FMOD foi definido.");
            return;
        }

        if (Time.time < lastErrorSoundTime + errorSoundCooldown)
            return;

        lastErrorSoundTime = Time.time;

        RuntimeManager.PlayOneShot(errorSound, transform.position);
    }

    private bool IsMathBlock(Collider other)
    {
        if (HasTag(other, mathBlockTag))
            return true;

        return acceptExistingProjectTag
            && !string.Equals(mathBlockTag, DefaultMathBlockTag, System.StringComparison.Ordinal)
            && HasTag(other, DefaultMathBlockTag);
    }

    private static bool HasTag(Collider other, string tagName)
    {
        if (other == null || string.IsNullOrWhiteSpace(tagName))
            return false;

        try
        {
            return other.CompareTag(tagName);
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private DoorValueVerifier GetConnectedVerifier()
    {
        if (connectedVerifier != null)
            return connectedVerifier;

        CacheConnectedVerifier();
        return connectedVerifier;
    }

    private void CacheConnectedVerifier()
    {
        connectedVerifier = connectedVerifierObject != null
            ? connectedVerifierObject.GetComponent<DoorValueVerifier>()
            : null;
    }

    private void NormalizeMathBlockTag()
    {
        if (string.IsNullOrWhiteSpace(mathBlockTag)
            || string.Equals(mathBlockTag, "Mathblock", System.StringComparison.OrdinalIgnoreCase))
        {
            mathBlockTag = DefaultMathBlockTag;
        }
    }
}
