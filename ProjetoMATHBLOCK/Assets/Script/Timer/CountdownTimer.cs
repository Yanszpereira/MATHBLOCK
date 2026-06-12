using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class CountdownTimer : MonoBehaviour
{
    [Header("Tempo Inicial")]
    public float tempoInicial = 300f; // 5 minutos

    [Header("Refer�ncia do Texto")]
    public TMP_Text timerText;

    private float tempoAtual;
    private bool contando = true;

    void Start()
    {
        tempoAtual = tempoInicial;
        AtualizarTexto();
    }

    void Update()
    {
        if (!contando)
            return;

        tempoAtual -= Time.deltaTime;

        if (tempoAtual <= 0)
        {
            tempoAtual = 0;
            contando = false;

            Debug.Log("Tempo esgotado!");
            // Aqui voc� pode chamar Game Over
            SceneManager.LoadScene("Fase 1");
        }

        AtualizarTexto();
    }

    void AtualizarTexto()
    {
        int minutos = Mathf.FloorToInt(tempoAtual / 60);
        int segundos = Mathf.FloorToInt(tempoAtual % 60);

        timerText.text = string.Format("{0:00}:{1:00}", minutos, segundos);
    }
}