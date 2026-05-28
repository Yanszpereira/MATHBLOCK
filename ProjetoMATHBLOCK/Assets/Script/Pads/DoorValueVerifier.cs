using UnityEngine;

public class DoorValueVerifier : MonoBehaviour
{
    [SerializeField] private int requiredValue = 1;

    // Tag que o pad precisa ter
    [SerializeField] private string acceptedPadTag = "VerifierPad";

    [SerializeField] private GameObject successParticleObject;
    [SerializeField] private GameObject door;

    public int RequiredValue => requiredValue;

    public void ReceiveValueFromPad(GameObject sourcePad, int receivedValue, GameObject sourceBlock)
    {
        // Verifica se o pad possui a tag correta
        if (sourcePad == null || !sourcePad.CompareTag(acceptedPadTag))
        {
            string sourcePadName = sourcePad != null ? sourcePad.name : "Pad desconhecido";

            Debug.LogWarning(
                $"Verificador {name} ignorou valor {receivedValue} de {sourcePadName}, pois aceita apenas Pads com a tag '{acceptedPadTag}'."
            );

            return;
        }

        string blockName = sourceBlock != null ? sourceBlock.name : "bloco desconhecido";

        if (receivedValue == requiredValue)
        {
            Debug.Log($"Verificador {name}: valor certo ({receivedValue}) recebido de {blockName}.");

            PlaySuccessParticles();

            if (door != null)
            {
                door.SetActive(false);
            }
        }
        else
        {
            Debug.Log(
                $"Verificador {name}: valor errado ({receivedValue}). Valor necessário: {requiredValue}."
            );
        }
    }

    private void PlaySuccessParticles()
    {
        if (successParticleObject == null)
            return;

        successParticleObject.SetActive(true);

        ParticleSystem[] particleSystems =
            successParticleObject.GetComponentsInChildren<ParticleSystem>();

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            particleSystem.Clear();
            particleSystem.Play();
        }
    }
}