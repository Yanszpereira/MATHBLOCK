using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Resultado compacto: apresenta um conteudo por vez, nunca uma lista gigante.</summary>
public sealed class ResultadosAprendizadoUI : MonoBehaviour
{
    private CanvasGroup grupo;
    private RectTransform toast;
    private RectTransform fill;
    private Image fillImage;
    private TMP_Text chamada;
    private TMP_Text materia;
    private TMP_Text percentual;
    private bool continuar;
    private readonly List<PlayerInput> entradasBloqueadas = new();
    private float escalaAnterior = 1f;
    private CursorLockMode travaAnterior;
    private bool cursorAnterior;
    private bool apresentacaoAtiva;
    private const float Largura = 500f;

    public static IEnumerator Mostrar(string cena)
    {
        GameObject go = new GameObject("Resultado de Aprendizado");
        ResultadosAprendizadoUI ui = go.AddComponent<ResultadosAprendizadoUI>();
        yield return ui.Executar(cena);
        Destroy(go);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static void MostrarDev(string cena)
    {
        if (FindFirstObjectByType<ResultadosAprendizadoUI>() != null) return;
        GameObject go = new GameObject("Resultado de Aprendizado DEV");
        ResultadosAprendizadoUI ui = go.AddComponent<ResultadosAprendizadoUI>();
        ui.StartCoroutine(ui.ExecutarDev(cena));
    }

    private IEnumerator ExecutarDev(string cena)
    {
        yield return Executar(cena);
        Destroy(gameObject);
    }
#endif

    private IEnumerator Executar(string cena)
    {
        escalaAnterior = Time.timeScale;
        travaAnterior = Cursor.lockState;
        cursorAnterior = Cursor.visible;
        apresentacaoAtiva = true;
        Time.timeScale = 0f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        BloquearJogador();
        Construir();

        List<ConteudoMatematico> conteudos = IntegradorAprendizadoCena.Configuracao.ObterDaCena(cena)
            .SelectMany(s => s.conteudos).Select(p => p.conteudo).Distinct().ToList();
        foreach (ConteudoMatematico conteudo in conteudos)
        {
            chamada.text = "FASE CONCLUÍDA • VOCÊ MANDOU BEM EM";
            materia.text = RotulosConteudo.Obter(conteudo).ToUpperInvariant();
            yield return AnimarConteudo(conteudo, ProgressoAprendizado.Instance.Obter(conteudo).valor, 1.05f);
        }

        chamada.text = "SEU DESEMPENHO NESTA FASE";
        materia.text = "PROGRESSO GERAL";
        yield return AnimarConteudo(conteudos.Count > 0 ? conteudos[0] : ConteudoMatematico.Operadores,
            ProgressoAprendizado.Instance.ObterOverall(conteudos), 1.2f);

        Button botao = CriarBotao();
        float limite = Time.realtimeSinceStartup + 3.5f;
        while (!continuar && Time.realtimeSinceStartup < limite) yield return null;
        botao.gameObject.SetActive(false);
        for (float t = 0; t < .35f; t += Time.unscaledDeltaTime) { grupo.alpha = 1f - t / .35f; yield return null; }

        RestaurarJogador();
    }

    private IEnumerator AnimarConteudo(ConteudoMatematico conteudo, float destino, float espera)
    {
        Color cor = RotulosConteudo.Cor(conteudo); fillImage.color = cor; percentual.color = cor;
        grupo.alpha = 0f; toast.localScale = Vector3.one * .93f; toast.anchoredPosition = new Vector2(0, 22f); Aplicar(0f);
        for (float t = 0; t < .3f; t += Time.unscaledDeltaTime)
        {
            float p = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / .3f), 3f); grupo.alpha = p;
            toast.localScale = Vector3.Lerp(Vector3.one * .93f, Vector3.one, p);
            toast.anchoredPosition = Vector2.Lerp(new Vector2(0, 22f), Vector2.zero, p); yield return null;
        }
        for (float t = 0; t < .8f; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0, 1, t / .8f); Aplicar(destino * p);
            fillImage.color = Color.Lerp(cor, Color.white, (Mathf.Sin(t * 15f) + 1f) * .2f);
            yield return null;
        }
        Aplicar(destino);
        fillImage.color = cor;
        for (float t = 0; t < .22f; t += Time.unscaledDeltaTime)
        { toast.anchoredPosition = Vector2.right * Mathf.Sin(t * 90f) * (1f - t / .22f) * 5f; yield return null; }
        toast.anchoredPosition = Vector2.zero;
        yield return new WaitForSecondsRealtime(espera);
        for (float t = 0; t < .25f; t += Time.unscaledDeltaTime) { grupo.alpha = 1f - t / .25f; yield return null; }
    }

    private void Aplicar(float valor)
    {
        valor = Mathf.Clamp01(valor); fill.sizeDelta = new Vector2(Largura * valor, 0f);
        percentual.text = Mathf.RoundToInt(valor * 100f) + "%";
    }

    private void Construir()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); gameObject.AddComponent<GraphicRaycaster>();

        toast = UIAprendizadoUtil.CriarRetangulo(transform, "Resumo compacto", new Vector2(570, 150));
        toast.anchorMin = toast.anchorMax = toast.pivot = new Vector2(.5f, .5f);
        grupo = toast.gameObject.AddComponent<CanvasGroup>();
        chamada = UIAprendizadoUtil.CriarTexto(toast, "Chamada", "FASE CONCLUÍDA", 19, TextAlignmentOptions.Center);
        chamada.color = new Color(1, 1, 1, .78f);
        UIAprendizadoUtil.DefinirRetangulo(chamada.rectTransform, new Vector2(0, -4), new Vector2(560, 28), new Vector2(.5f, 1));
        materia = UIAprendizadoUtil.CriarTexto(toast, "Materia", "MATEMÁTICA", 38, TextAlignmentOptions.Center);
        UIAprendizadoUtil.DefinirRetangulo(materia.rectTransform, new Vector2(0, -32), new Vector2(560, 50), new Vector2(.5f, 1));
        percentual = UIAprendizadoUtil.CriarTexto(toast, "Percentual", "0%", 25, TextAlignmentOptions.Right);
        UIAprendizadoUtil.DefinirRetangulo(percentual.rectTransform, new Vector2(2, 18), new Vector2(70, 30), new Vector2(1, 0));
        Image trilho = UIAprendizadoUtil.CriarImagem(toast, "Trilho", new Color(0.02f, .03f, .07f, .82f), true);
        UIAprendizadoUtil.DefinirRetangulo(trilho.rectTransform, new Vector2(35, 20), new Vector2(Largura, 15), new Vector2(0, 0));
        Image mascara = UIAprendizadoUtil.CriarImagem(trilho.transform, "Mascara do preenchimento", Color.white, true);
        fill = mascara.rectTransform;
        fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(0, 1); fill.pivot = new Vector2(0, .5f);
        fill.anchoredPosition = Vector2.zero; fill.sizeDelta = Vector2.zero;
        Mask mask = mascara.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
        fillImage = UIAprendizadoUtil.CriarPreenchimentoPontilhado(mascara.transform, Color.green, out _);
    }

    private void BloquearJogador()
    {
        foreach (PlayerInput entrada in FindObjectsByType<PlayerInput>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (entrada == null || !entrada.inputIsActive) continue;
            entrada.DeactivateInput();
            entradasBloqueadas.Add(entrada);
        }
    }

    private void RestaurarJogador()
    {
        if (!apresentacaoAtiva) return;
        foreach (PlayerInput entrada in entradasBloqueadas)
            if (entrada != null && entrada.enabled && entrada.gameObject.activeInHierarchy)
                entrada.ActivateInput();
        entradasBloqueadas.Clear();
        Time.timeScale = escalaAnterior;
        Cursor.lockState = travaAnterior;
        Cursor.visible = cursorAnterior;
        apresentacaoAtiva = false;
    }

    private void OnDestroy() => RestaurarJogador();

    private Button CriarBotao()
    {
        Image imagem = UIAprendizadoUtil.CriarImagem(toast, "Continuar", new Color(1, 1, 1, .12f), true);
        UIAprendizadoUtil.DefinirRetangulo(imagem.rectTransform, new Vector2(0, -18), new Vector2(145, 38), new Vector2(.5f, 0));
        Button b = imagem.gameObject.AddComponent<Button>(); b.targetGraphic = imagem; b.onClick.AddListener(() => continuar = true);
        TMP_Text texto = UIAprendizadoUtil.CriarTexto(imagem.transform, "Texto", "CONTINUAR", 18, TextAlignmentOptions.Center);
        texto.rectTransform.anchorMin = Vector2.zero; texto.rectTransform.anchorMax = Vector2.one;
        texto.rectTransform.offsetMin = texto.rectTransform.offsetMax = Vector2.zero;
        return b;
    }
}
