using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Recompensa da fase: bolinha sobe, desamassa, apresenta barras e desaparece.</summary>
public sealed class ResultadosAprendizadoUI : MonoBehaviour
{
    private sealed class Linha
    {
        public RectTransform raiz, preenchimento;
        public Image cor;
        public TMP_Text percentual;
        public Color tinta;
        public float destino;
    }
    private readonly List<Linha> linhas = new();
    private readonly List<PlayerInput> entradasBloqueadas = new();
    private readonly Dictionary<PlayerInput, string> mapasAnteriores = new();
    private float escalaAnterior = 1f;
    private CursorLockMode travaAnterior;
    private bool cursorAnterior, apresentacaoAtiva;
    private RectTransform folha;
    private CanvasGroup grupo, conteudo;
    private PapelDesamassandoUI papel;
    private const float Largura = 420f;
    public string EtapaAtual { get; private set; }

    public static IEnumerator Mostrar(string cena)
    {
        while (FindFirstObjectByType<ResultadosAprendizadoUI>() != null) yield return null;
        GameObject objeto = new GameObject("Recompensa de Aprendizado");
        var ui = objeto.AddComponent<ResultadosAprendizadoUI>();
        try { yield return ui.Executar(cena); }
        finally { if (objeto != null) Destroy(objeto); }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static void MostrarDev(string cena)
    {
        if (FindFirstObjectByType<ResultadosAprendizadoUI>() != null) return;
        var objeto = new GameObject("Recompensa de Aprendizado DEV");
        var ui = objeto.AddComponent<ResultadosAprendizadoUI>();
        ui.StartCoroutine(ui.ExecutarDev(cena));
    }
    private IEnumerator ExecutarDev(string cena)
    {
        try { yield return Executar(cena); }
        finally { Destroy(gameObject); }
    }
#endif

    private IEnumerator Executar(string cena)
    {
        escalaAnterior = Time.timeScale; travaAnterior = Cursor.lockState; cursorAnterior = Cursor.visible;
        apresentacaoAtiva = true;
        Time.timeScale = 0f;
        BloquearJogador();
        if (!Construir(cena)) { RestaurarJogador(); yield break; }
        try
        {
            EtapaAtual = "Bolinha subindo";
            papel.MostrarQuadro(0); conteudo.alpha = 0f; grupo.alpha = 0f;
            folha.localRotation = Quaternion.identity;
            double inicio = Time.realtimeSinceStartupAsDouble;
            const int quadrosSubida = 10;
            while (Time.realtimeSinceStartupAsDouble - inicio < (double)quadrosSubida / PapelDesamassandoUI.QuadrosPorSegundo)
            {
                int quadro = Mathf.Min(quadrosSubida, (int)((Time.realtimeSinceStartupAsDouble - inicio) * PapelDesamassandoUI.QuadrosPorSegundo));
                float p = (float)quadro / quadrosSubida, suave = 1f - Mathf.Pow(1f - p, 3f);
                folha.anchoredPosition = Vector2.Lerp(new Vector2(0, -760f), new Vector2(0, -28f), suave);
                grupo.alpha = Mathf.Clamp01(p * 3f);
                yield return null;
            }
            EtapaAtual = "Papel 2D - 15 FPS";
            inicio = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - inicio < (double)papel.QuantidadeQuadros / PapelDesamassandoUI.QuadrosPorSegundo)
            {
                int quadro = Mathf.Min(papel.QuantidadeQuadros - 1,
                    (int)((Time.realtimeSinceStartupAsDouble - inicio) * PapelDesamassandoUI.QuadrosPorSegundo));
                papel.MostrarQuadro(quadro);
                float p = (float)quadro / Mathf.Max(1, papel.QuantidadeQuadros - 1);
                folha.anchoredPosition = Vector2.Lerp(new Vector2(0, -28f), Vector2.zero, p);
                conteudo.alpha = quadro == papel.QuantidadeQuadros - 1 ? 1f : 0f;
                yield return null;
            }
            papel.MostrarQuadro(papel.QuantidadeQuadros - 1); grupo.alpha = conteudo.alpha = 1f;
            folha.anchoredPosition = Vector2.zero; folha.localRotation = Quaternion.identity;
            EtapaAtual = "Mostrando progresso";
            foreach (Linha linha in linhas) yield return AnimarLinha(linha);

            // Todos os conteudos ja foram apresentados: devolve camera e movimento
            // antes da espera final, sem exigir outro clique para voltar a jogar.
            RestaurarJogador();
            EtapaAtual = "Progresso completo";
            yield return new WaitForSecondsRealtime(1.35f);
            EtapaAtual = "Fade out";
            for (float t = 0; t < .55f; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.Clamp01(t / .55f);
                grupo.alpha = 1f - Mathf.SmoothStep(0, 1, p);
                folha.anchoredPosition = Vector2.up * (p * 28f);
                yield return null;
            }
            grupo.alpha = 0f;
        }
        finally { RestaurarJogador(); }
    }

    private IEnumerator AnimarLinha(Linha linha)
    {
        for (float t = 0; t < .85f; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / .85f);
            Aplicar(linha, linha.destino * Mathf.SmoothStep(0, 1, p));
            linha.cor.color = Color.Lerp(linha.tinta, Color.white, (Mathf.Sin(p * 17f) + 1f) * .2f);
            linha.raiz.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(p * 50f) * Mathf.Sin(p * Mathf.PI) * .55f);
            yield return null;
        }
        Aplicar(linha, linha.destino); linha.cor.color = linha.tinta;
        linha.raiz.localRotation = Quaternion.identity;
        yield return new WaitForSecondsRealtime(.1f);
    }

    private static void Aplicar(Linha linha, float valor)
    {
        valor = Mathf.Clamp01(valor);
        linha.preenchimento.sizeDelta = new Vector2(Largura * valor, 0f);
        linha.percentual.text = Mathf.RoundToInt(valor * 100f) + "%";
    }

    private bool Construir(string cena)
    {
        GameObject modelo = Resources.Load<GameObject>("Aprendizado/RecompensaPapel");
        if (modelo == null) { Debug.LogError("Prefab RecompensaPapel nao configurado.", this); return false; }
        Canvas canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5000;
        CanvasScaler escala = gameObject.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920, 1080); escala.matchWidthOrHeight = .5f;
        folha = Instantiate(modelo, transform, false).GetComponent<RectTransform>();
        grupo = folha.GetComponent<CanvasGroup>();
        papel = folha.GetComponent<PapelDesamassandoUI>();
        RectTransform area = folha.Find("Conteudo").GetComponent<RectTransform>();
        conteudo = area.GetComponent<CanvasGroup>();
        Texto(area, "Parabens", "Parabéns!", 48f, new Vector2(0, 245f), new Vector2(440, 64));
        Texto(area, "Fase", cena.ToUpperInvariant() + " CONCLUÍDA", 26f, new Vector2(0, 191f), new Vector2(440, 42));
        Texto(area, "Mensagem", "Olha o quanto você avançou", 23f, new Vector2(0, 139f), new Vector2(440, 42));
        List<ConteudoMatematico> conteudos = IntegradorAprendizadoCena.Configuracao.ObterDaCena(cena)
            .SelectMany(s => s.conteudos).Select(p => p.conteudo).Distinct().ToList();
        float y = 55f;
        foreach (ConteudoMatematico item in conteudos)
        {
            CriarLinha(area, RotulosConteudo.Obter(item), RotulosConteudo.Cor(item),
                ProgressoAprendizado.Instance.Obter(item).valor, y);
            y -= 110f;
        }
        CriarLinha(area, "Progresso geral", new Color(.22f, .38f, .48f),
            ProgressoAprendizado.Instance.ObterOverall(conteudos), y - 30f);
        return true;
    }

    private void CriarLinha(RectTransform pai, string nome, Color cor, float destino, float y)
    {
        RectTransform raiz = UIAprendizadoUtil.CriarRetangulo(pai, nome, new Vector2(Largura, 88));
        raiz.anchoredPosition = new Vector2(0, y);
        TMP_Text rotulo = Texto(raiz, "Materia", nome, 31f, new Vector2(-35, 20), new Vector2(350, 48));
        rotulo.alignment = TextAlignmentOptions.Left; rotulo.color = Color.Lerp(cor, Color.black, .25f);
        TMP_Text percentual = Texto(raiz, "Percentual", "0%", 25f, new Vector2(177, 20), new Vector2(68, 44));
        percentual.alignment = TextAlignmentOptions.Right;
        Image trilho = UIAprendizadoUtil.CriarImagem(raiz, "Trilho", new Color(.2f, .22f, .24f, .16f), true);
        trilho.rectTransform.sizeDelta = new Vector2(Largura, 22f); trilho.rectTransform.anchoredPosition = new Vector2(0, -17);
        Image mascara = UIAprendizadoUtil.CriarImagem(trilho.transform, "Preenchimento", Color.white, true);
        RectTransform fill = mascara.rectTransform;
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.pivot = new Vector2(0, .5f);
        fill.anchoredPosition = Vector2.zero; fill.sizeDelta = Vector2.zero;
        mascara.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Image tinta = UIAprendizadoUtil.CriarPreenchimentoPontilhado(fill, cor, out _);
        linhas.Add(new Linha { raiz = raiz, preenchimento = fill, cor = tinta, percentual = percentual, tinta = cor, destino = destino });
    }

    private static TMP_Text Texto(Transform pai, string nome, string valor, float tamanho, Vector2 posicao, Vector2 dimensoes)
    {
        TMP_Text texto = UIAprendizadoUtil.CriarTexto(pai, nome, valor, tamanho, TextAlignmentOptions.Center);
        texto.color = new Color(.19f, .16f, .13f); texto.raycastTarget = false;
        texto.rectTransform.sizeDelta = dimensoes; texto.rectTransform.anchoredPosition = posicao;
        Shadow sombra = texto.GetComponent<Shadow>(); if (sombra != null) sombra.enabled = false;
        return texto;
    }

    private void BloquearJogador()
    {
        foreach (PlayerInput entrada in FindObjectsByType<PlayerInput>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (entrada == null || !entrada.inputIsActive) continue;
            mapasAnteriores[entrada] = entrada.currentActionMap?.name;
            entrada.DeactivateInput(); entradasBloqueadas.Add(entrada);
        }
    }

    private void RestaurarJogador()
    {
        if (!apresentacaoAtiva) return;
        foreach (PlayerInput entrada in entradasBloqueadas)
            if (entrada != null && entrada.enabled && entrada.gameObject.activeInHierarchy)
            {
                entrada.ActivateInput();
                if (mapasAnteriores.TryGetValue(entrada, out string mapa) && !string.IsNullOrEmpty(mapa))
                    entrada.SwitchCurrentActionMap(mapa);
            }
        entradasBloqueadas.Clear(); mapasAnteriores.Clear();
        Time.timeScale = escalaAnterior; Cursor.lockState = travaAnterior; Cursor.visible = cursorAnterior;
        apresentacaoAtiva = false;
    }
    private void OnDestroy() => RestaurarJogador();
    private void OnDisable() => RestaurarJogador();
}
