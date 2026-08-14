using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class MenuController : MonoBehaviour
{
    [Header("Folha Inicial")]
    [SerializeField] private Animation startPaper;

    [Header("Menus")]
    [SerializeField] private GameObject menuCreditos;
    [SerializeField] private GameObject menuOpcoes;
    [SerializeField] private GameObject menuSair;

    [Header("Blocker")]
    [SerializeField] private GameObject blocker;

    [Header("Configuração")]
    [SerializeField] private float tempoAnimacao = 1f;

    [Header("Cena do Jogo")]
    [SerializeField] private string nomeCenaJogo = "Game";

    public void AbrirCreditos()
    {
        StartCoroutine(AbrirMenu(menuCreditos));
    }

    public void AbrirOpcoes()
    {
        StartCoroutine(AbrirMenu(menuOpcoes));
    }

    public void AbrirSair()
    {
        StartCoroutine(AbrirMenu(menuSair));
    }

    public void FecharCreditos()
    {
        StartCoroutine(FecharMenu(menuCreditos));
    }

    public void FecharOpcoes()
    {
        StartCoroutine(FecharMenu(menuOpcoes));
    }

    public void FecharSair()
    {
        StartCoroutine(FecharMenu(menuSair));
    }

    private IEnumerator AbrirMenu(GameObject menu)
    {
        menu.SetActive(true);

        if (blocker != null)
            blocker.SetActive(true);

        startPaper.Play("OpenPaper");

        yield return new WaitForSeconds(tempoAnimacao);
    }

    private IEnumerator FecharMenu(GameObject menu)
    {
        startPaper.Play("ClosePaper");

        yield return new WaitForSeconds(tempoAnimacao);

        menu.SetActive(false);

        if (blocker != null)
            blocker.SetActive(false);
    }

    public void IniciarJogo()
    {
        SceneManager.LoadScene(nomeCenaJogo);
    }

    public void FecharJogo()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void VoltarJogo()
    {
        StartCoroutine(FecharStart());
    }

    private IEnumerator FecharStart()
    {
        if (startPaper != null)
            startPaper.Play("ClosePaper");

        yield return new WaitForSeconds(tempoAnimacao);

        if (blocker != null)
            blocker.SetActive(false);
    }
}