using UnityEngine;
using FMODUnity;

public class DoorValueVerifier : MonoBehaviour
{
    [Header("Valor exigido")]
    [SerializeField] private int requiredValue = 1;
    [SerializeField, HideInInspector] private bool useRequiredRange;
    [SerializeField, HideInInspector] private int requiredMinValue = 1;
    [SerializeField, HideInInspector] private int requiredMaxValue = 1;

    [Header("Referências")]
    [SerializeField] private GameObject acceptedPadObject;
    [SerializeField] private GameObject successParticleObject;
    [SerializeField] private DoorOpener doorOpener;

    [Header("Sons FMOD")]
    [SerializeField] private bool playSounds = true;
    [SerializeField] private EventReference correctSound;
    [SerializeField] private EventReference errorSound;

    [Header("Controle de repetição")]
    [SerializeField] private float correctSoundCooldown = 0.35f;
    [SerializeField] private float errorSoundCooldown = 0.35f;

    private float lastCorrectSoundTime = -999f;
    private float lastErrorSoundTime = -999f;

    public int RequiredValue => requiredValue;
    public int RequiredMinValue => useRequiredRange ? requiredMinValue : requiredValue;
    public int RequiredMaxValue => useRequiredRange ? requiredMaxValue : requiredValue;

    private void OnValidate()
    {
        requiredValue = Mathf.Max(0, requiredValue);
        requiredMinValue = Mathf.Max(0, requiredMinValue);
        requiredMaxValue = Mathf.Max(requiredMinValue, requiredMaxValue);
    }

    public void SetRequiredValue(int value)
    {
        SetRequiredRange(value, value);
    }

    public void SetRequiredRange(int minValue, int maxValue)
    {
        requiredMinValue = Mathf.Max(0, Mathf.Min(minValue, maxValue));
        requiredMaxValue = Mathf.Max(requiredMinValue, Mathf.Max(minValue, maxValue));
        requiredValue = requiredMinValue;
        useRequiredRange = true;

        if (requiredMinValue == requiredMaxValue)
        {
            Debug.Log($"Verificador {name}: valor exigido configurado para {requiredMinValue}.");
            Debug.Log($"[TEMP] Valor da porta: {requiredMinValue}.");
        }
        else
        {
            Debug.Log($"Verificador {name}: intervalo exigido configurado para {requiredMinValue} ate {requiredMaxValue}.");
            Debug.Log($"[TEMP] Valor da porta: {requiredMinValue} ate {requiredMaxValue}.");
        }
    }

    public void ReceiveValueFromPad(GameObject sourcePad, int receivedValue, GameObject sourceBlock)
    {
        if (!IsPadAccepted(sourcePad))
        {
            Debug.LogWarning($"Verificador {name} ignorou valor {receivedValue} de {ObjectName(sourcePad)}, pois aceita apenas o Pad {ObjectName(acceptedPadObject)}.");

            PlayErrorSound(sourcePad);

            return;
        }

        if (!IsValueAccepted(receivedValue))
        {
            LogWrongValue(receivedValue);

            PlayErrorSound(sourcePad);

            return;
        }

        HandleCorrectValue(sourcePad, receivedValue, sourceBlock);
    }

    public bool IsPadAccepted(GameObject sourcePad)
    {
        return acceptedPadObject == null || sourcePad == acceptedPadObject;
    }

    public bool IsValueAccepted(int receivedValue)
    {
        return receivedValue >= RequiredMinValue && receivedValue <= RequiredMaxValue;
    }

    public void LogWrongValue(int receivedValue)
    {
        Debug.Log($"Verificador {name}: valor errado ({receivedValue}). Intervalo necessario: {RequiredMinValue} ate {RequiredMaxValue}.");
    }

    private void HandleCorrectValue(GameObject sourcePad, int receivedValue, GameObject sourceBlock)
    {
        string blockName = sourceBlock != null ? sourceBlock.name : "bloco desconhecido";

        Debug.Log($"Verificador {name}: valor certo ({receivedValue}) recebido de {blockName}. Intervalo aceito: {RequiredMinValue} ate {RequiredMaxValue}.");

        PlayCorrectSound(sourcePad);
        PlaySuccessParticles();

        GameManager.Instance?.RegisterCorrectDoorValue(sourcePad, receivedValue, sourceBlock, this);

        if (doorOpener == null)
        {
            Debug.LogWarning($"Verificador {name}: DoorOpener nao configurado; feedback tocou, mas a porta nao abriu.");
            return;
        }

        doorOpener.OpenOnce();
    }

    private void PlayCorrectSound(GameObject sourcePad)
    {
        if (!playSounds)
            return;

        if (correctSound.IsNull)
        {
            Debug.LogWarning($"Verificador {name}: som de acerto FMOD nao configurado.");
            return;
        }

        if (Time.time < lastCorrectSoundTime + correctSoundCooldown)
            return;

        lastCorrectSoundTime = Time.time;

        Vector3 soundPosition = sourcePad != null ? sourcePad.transform.position : transform.position;

        RuntimeManager.PlayOneShot(correctSound, soundPosition);
    }

    private void PlayErrorSound(GameObject sourcePad)
    {
        if (!playSounds)
            return;

        if (errorSound.IsNull)
        {
            Debug.LogWarning($"Verificador {name}: som de erro FMOD nao configurado.");
            return;
        }

        if (Time.time < lastErrorSoundTime + errorSoundCooldown)
            return;

        lastErrorSoundTime = Time.time;

        Vector3 soundPosition = sourcePad != null ? sourcePad.transform.position : transform.position;

        RuntimeManager.PlayOneShot(errorSound, soundPosition);
    }

    private void PlaySuccessParticles()
    {
        if (successParticleObject == null)
            return;

        successParticleObject.SetActive(true);

        ParticleSystem[] particleSystems = successParticleObject.GetComponentsInChildren<ParticleSystem>();

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            particleSystem.Clear();
            particleSystem.Play();
        }
    }

    private static string ObjectName(Object target)
    {
        return target != null ? target.name : "null";
    }
}