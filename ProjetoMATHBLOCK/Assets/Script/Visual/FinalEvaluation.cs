using UnityEngine;
using TMPro;

public class FinalEvaluation : MonoBehaviour
{
    [SerializeField] private TMP_Text notaFinalText;

    private bool notaAplicada;

    void Start()
    {
        if (notaFinalText == null)
        {
            GameObject objetoNotaFinal = GameObject.Find("NotaFinal");
            if (objetoNotaFinal != null)
                notaFinalText = objetoNotaFinal.GetComponentInChildren<TMP_Text>(true);
        }

        if (notaFinalText == null)
            notaFinalText = GetComponentInChildren<TMP_Text>(true);

        if (notaFinalText == null)
            Debug.LogWarning("FinalEvaluation não encontrou um texto TextMeshPro para exibir em NotaFinal.", this);

        AtualizarNotaFinal();
    }

    void Update()
    {
        if (!notaAplicada)
            AtualizarNotaFinal();
    }

    private void AtualizarNotaFinal()
    {
        if (notaFinalText == null ||
            !ResultadosDasFases.TryObterResultadoFinal(out ResultadoFinal resultadoFinal))
            return;

        notaFinalText.text = resultadoFinal.Nota;
        notaAplicada = true;
    }
}
