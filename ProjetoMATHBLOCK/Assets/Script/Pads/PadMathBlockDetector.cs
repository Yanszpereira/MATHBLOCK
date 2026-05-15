using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PadMathBlockDetector : MonoBehaviour
{
    [SerializeField] private string mathBlockTag = "MathBlock";
    [SerializeField] private bool acceptExistingProjectTag = true;
    [SerializeField] private GameObject connectedVerifierObject;

    private readonly Dictionary<Collider, int> detectedBlocks = new Dictionary<Collider, int>();

    private void Reset()
    {
        Collider padCollider = GetComponent<Collider>();
        if (padCollider != null)
        {
            padCollider.isTrigger = false;
        }
    }

    private void OnValidate()
    {
        Collider padCollider = GetComponent<Collider>();
        if (padCollider != null && padCollider.isTrigger)
        {
            padCollider.isTrigger = false;
        }
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

        if (connectedVerifierObject == null)
        {
            return;
        }

        DoorValueVerifier connectedVerifier = connectedVerifierObject.GetComponent<DoorValueVerifier>();
        if (connectedVerifier == null)
        {
            Debug.LogWarning($"Pad {name} tem um verificador conectado, mas ele nao possui DoorValueVerifier.");
            return;
        }

        connectedVerifier.ReceiveValueFromPad(gameObject, value, blockValue.gameObject);
    }

    private bool IsMathBlock(Collider other)
    {
        if (HasTag(other, mathBlockTag))
            return true;

        return acceptExistingProjectTag && HasTag(other, "MathBlock");
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
}
