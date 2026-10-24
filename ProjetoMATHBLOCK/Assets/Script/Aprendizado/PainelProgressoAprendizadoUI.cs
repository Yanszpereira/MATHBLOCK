using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Tela consultavel com o progresso global de todas as materias.</summary>
public sealed class PainelProgressoAprendizadoUI : MonoBehaviour
{
    private sealed class Linha
    {
        public ConteudoMatematico conteudo;
        public RectTransform preenchimento;
        public Image pontilhado;
        public TMP_Text percentual;
        public float valor;
    }

    private static PainelProgressoAprendizadoUI aberto;
    private readonly List<Linha> linhas = new();
    private CanvasGroup grupo;
    private RectTransform cartao;
    private TMP_Text overall;
    private bool fechando;

    public static void Mostrar()
    {
        if (aberto != null) return;
        GameObject go = new GameObject("Painel de Progresso de Aprendizado");
        aberto = go.AddComponent<PainelProgressoAprendizadoUI>();
    }

    public static bool FecharAberto()
    {
        if (aberto == null) return false;
        aberto.Fechar();
        return true;
    }

    private void Awake()
    {
        Construir();
        StartCoroutine(AnimarEntrada());
    }

    private void Construir()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        gameObject.AddComponent<GraphicRaycaster>();

        Image bloqueio = UIAprendizadoUtil.CriarImagem(transform, "Escurecimento", new Color(0f, 0f, 0f, .62f));
        bloqueio.rectTransform.anchorMin = Vector2.zero;
        bloqueio.rectTransform.anchorMax = Vector2.one;
        bloqueio.rectTransform.offsetMin = bloqueio.rectTransform.offsetMax = Vector2.zero;
        Button fecharFora = bloqueio.gameObject.AddComponent<Button>();
        fecharFora.targetGraphic = bloqueio;
        fecharFora.onClick.AddListener(Fechar);

        Image fundo = UIAprendizadoUtil.CriarImagem(transform, "Folha de progresso", Color.white, true);
        AplicarArteDoPapel(fundo);
        cartao = fundo.rectTransform;
        cartao.anchorMin = cartao.anchorMax = cartao.pivot = new Vector2(.5f, .5f);
        cartao.sizeDelta = new Vector2(780f, 610f);
        grupo = cartao.gameObject.AddComponent<CanvasGroup>();

        TMP_Text titulo = UIAprendizadoUtil.CriarTexto(cartao, "Titulo", "MEU PROGRESSO", 40f, TextAlignmentOptions.Center);
        titulo.color = new Color(.09f, .075f, .07f, 1f);
        UIAprendizadoUtil.DefinirRetangulo(titulo.rectTransform, new Vector2(0f, -30f), new Vector2(700f, 55f), new Vector2(.5f, 1f));
        TMP_Text subtitulo = UIAprendizadoUtil.CriarTexto(cartao, "Subtitulo", "TUDO O QUE VOCÊ APRENDEU ATÉ AGORA", 18f, TextAlignmentOptions.Center);
        subtitulo.color = new Color(.12f, .1f, .09f, .68f);
        UIAprendizadoUtil.DefinirRetangulo(subtitulo.rectTransform, new Vector2(0f, -84f), new Vector2(700f, 28f), new Vector2(.5f, 1f));

        float y = -145f;
        foreach (ConteudoMatematico conteudo in Enum.GetValues(typeof(ConteudoMatematico)))
        {
            CriarLinha(conteudo, y);
            y -= 92f;
        }

        overall = UIAprendizadoUtil.CriarTexto(cartao, "Overall", "PROGRESSO GERAL  0%", 22f, TextAlignmentOptions.Center);
        overall.color = new Color(.45f, .26f, .06f, 1f);
        UIAprendizadoUtil.DefinirRetangulo(overall.rectTransform, new Vector2(0f, 67f), new Vector2(500f, 32f), new Vector2(.5f, 0f));

        Image fundoFechar = UIAprendizadoUtil.CriarImagem(cartao, "Fechar", new Color(.18f, .13f, .09f, .1f), true);
        UIAprendizadoUtil.DefinirRetangulo(fundoFechar.rectTransform, new Vector2(0f, 18f), new Vector2(170f, 40f), new Vector2(.5f, 0f));
        Button botaoFechar = fundoFechar.gameObject.AddComponent<Button>();
        botaoFechar.targetGraphic = fundoFechar;
        botaoFechar.onClick.AddListener(Fechar);
        TMP_Text textoFechar = UIAprendizadoUtil.CriarTexto(fundoFechar.transform, "Texto", "> VOLTAR", 19f, TextAlignmentOptions.Center);
        textoFechar.color = new Color(.09f, .075f, .07f, 1f);
        textoFechar.rectTransform.anchorMin = Vector2.zero;
        textoFechar.rectTransform.anchorMax = Vector2.one;
        textoFechar.rectTransform.offsetMin = textoFechar.rectTransform.offsetMax = Vector2.zero;
    }

    private void CriarLinha(ConteudoMatematico conteudo, float y)
    {
        Color cor = RotulosConteudo.Cor(conteudo);
        TMP_Text nome = UIAprendizadoUtil.CriarTexto(cartao, $"Nome {conteudo}", RotulosConteudo.Obter(conteudo).ToUpperInvariant(), 22f, TextAlignmentOptions.Left);
        nome.color = cor;
        UIAprendizadoUtil.DefinirRetangulo(nome.rectTransform, new Vector2(52f, y), new Vector2(520f, 30f), new Vector2(0f, 1f));

        TMP_Text percentual = UIAprendizadoUtil.CriarTexto(cartao, $"Percentual {conteudo}", "0%", 20f, TextAlignmentOptions.Right);
        percentual.color = cor;
        UIAprendizadoUtil.DefinirRetangulo(percentual.rectTransform, new Vector2(-52f, y), new Vector2(100f, 30f), new Vector2(1f, 1f));

        Image trilho = UIAprendizadoUtil.CriarImagem(cartao, $"Trilho {conteudo}", new Color(.16f, .13f, .11f, .2f), true);
        UIAprendizadoUtil.DefinirRetangulo(trilho.rectTransform, new Vector2(52f, y - 42f), new Vector2(676f, 25f), new Vector2(0f, 1f));
        Image mascara = UIAprendizadoUtil.CriarImagem(trilho.transform, "Mascara", Color.white, true);
        RectTransform preenchimento = mascara.rectTransform;
        preenchimento.anchorMin = new Vector2(0f, 0f); preenchimento.anchorMax = new Vector2(0f, 1f);
        preenchimento.pivot = new Vector2(0f, .5f); preenchimento.anchoredPosition = Vector2.zero;
        preenchimento.sizeDelta = Vector2.zero;
        Mask mask = mascara.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
        Image pontos = UIAprendizadoUtil.CriarImagem(mascara.transform, "Pontilhado", cor);
        pontos.sprite = UIAprendizadoUtil.ObterSpritePontilhado(); pontos.type = Image.Type.Tiled;
        pontos.rectTransform.anchorMin = Vector2.zero; pontos.rectTransform.anchorMax = Vector2.one;
        pontos.rectTransform.offsetMin = pontos.rectTransform.offsetMax = Vector2.zero;

        float valor = ProgressoAprendizado.Instance != null ? ProgressoAprendizado.Instance.Obter(conteudo).valor : 0f;
        linhas.Add(new Linha { conteudo = conteudo, preenchimento = preenchimento, pontilhado = pontos, percentual = percentual, valor = valor });
    }

    private static void AplicarArteDoPapel(Image destino)
    {
        foreach (Transform candidato in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidato == null || !candidato.name.Equals("MenuPaper", StringComparison.OrdinalIgnoreCase))
                continue;
            Transform imagem = candidato.Find("Image");
            Image origem = imagem != null ? imagem.GetComponent<Image>() : null;
            if (origem == null || origem.sprite == null) continue;
            destino.sprite = origem.sprite;
            destino.type = origem.type;
            destino.preserveAspect = false;
            return;
        }
    }

    private IEnumerator AnimarEntrada()
    {
        grupo.alpha = 0f;
        cartao.localScale = Vector3.one * .92f;
        for (float t = 0f; t < .3f; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / .3f);
            grupo.alpha = p;
            cartao.localScale = Vector3.Lerp(Vector3.one * .92f, Vector3.one, p);
            yield return null;
        }

        float soma = 0f;
        foreach (Linha linha in linhas)
        {
            soma += linha.valor;
            yield return AnimarLinha(linha);
        }
        float valorOverall = linhas.Count == 0 ? 0f : soma / linhas.Count;
        overall.text = $"PROGRESSO GERAL  {Mathf.RoundToInt(valorOverall * 100f)}%";
    }

    private static IEnumerator AnimarLinha(Linha linha)
    {
        Color baseColor = RotulosConteudo.Cor(linha.conteudo);
        for (float t = 0f; t < .42f; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / .42f);
            linha.preenchimento.sizeDelta = new Vector2(676f * linha.valor * p, 0f);
            linha.percentual.text = Mathf.RoundToInt(linha.valor * p * 100f) + "%";
            linha.pontilhado.color = Color.Lerp(baseColor, Color.white, Mathf.Sin(p * Mathf.PI) * .45f);
            yield return null;
        }
        linha.preenchimento.sizeDelta = new Vector2(676f * linha.valor, 0f);
        linha.percentual.text = Mathf.RoundToInt(linha.valor * 100f) + "%";
        linha.pontilhado.color = baseColor;
        yield return new WaitForSecondsRealtime(.08f);
    }

    private void Fechar()
    {
        if (fechando) return;
        fechando = true;
        StartCoroutine(AnimarSaida());
    }

    private IEnumerator AnimarSaida()
    {
        for (float t = 0f; t < .22f; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / .22f);
            grupo.alpha = 1f - p;
            cartao.localScale = Vector3.Lerp(Vector3.one, Vector3.one * .94f, p);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (aberto == this) aberto = null;
    }
}
