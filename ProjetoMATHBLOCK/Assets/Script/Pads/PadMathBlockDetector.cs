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

    [Header("Valor esperado")]
    [SerializeField] private int expectedValue = 0;
    [SerializeField] private bool playErrorSoundWhenWrong = true;

    [Header("Som de erro")]
    [SerializeField] private EventReference errorSound;
    [SerializeField] private float errorSoundCooldown = 0.35f;

    private readonly Dictionary<Collider, int> detectedBlocks = new Dictionary<Collider, int>();
    private DoorValueVerifier connectedVerifier;

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

        if (!forcePrint && detectedBlocks.TryGetValue(other, out int lastValue) && lastValue == value)
            return;

        detectedBlocks[other] = value;

        Debug.Log($"Pad {name} detectou bloco {blockValue.name} com valor {value}.");

        if (playErrorSoundWhenWrong && value != expectedValue)
        {
            PlayErrorSound();
        }

        DoorValueVerifier verifier = GetConnectedVerifier();

        if (verifier == null)
        {
            Debug.LogWarning($"Pad {name} detectou valor {value}, mas nao possui DoorValueVerifier conectado.");
            return;
        }

        verifier.ReceiveValueFromPad(gameObject, value, blockValue.gameObject);
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