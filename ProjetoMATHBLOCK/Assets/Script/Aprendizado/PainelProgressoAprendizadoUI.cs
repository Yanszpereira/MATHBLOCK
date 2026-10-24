using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Consulta de progresso com a arte de papel e a tipografia do menu original.</summary>
public sealed class PainelProgressoAprendizadoUI : MonoBehaviour
{
    private const float LarguraBarra = 540f;
    private const float DuracaoAnimacaoPainel = .25f;
    private const float EscalaInicialPainel = .96f;
    private static readonly Color Tinta = new Color(.15f, .12f, .10f, 1f);
    private sealed class Linha
    {
        public RectTransform preenchimento;
        public Image pontilhado;
        public TMP_Text percentual;
        public Color cor;
        public float valor;
    }

    private static PainelProgressoAprendizadoUI aberto;
    public static bool EstaAberto => aberto != null;
    private readonly List<Linha> linhas = new();
    private CanvasGroup grupo;
    private RectTransform cartao;
    private RectTransform canvasRect;
    private TMP_Text overall;
    private Button voltar;
    private Button origemVoltar;
    private Image origemPapel;
    private Sprite papelRecortado;
    private Action aoFechar;
    private Coroutine animacao;
    private Vector2 tamanhoPainel = new(700f, 840f);
    private float fatorEntrada = EscalaInicialPainel;
    private bool fechando;

    public static void Mostrar(Button botaoOrigem = null, Image papelOrigem = null, Action retorno = null)
    {
        if (aberto != null) return;
        // Configurar referencias antes de OnEnable evita procurar um Back de outro submenu.
        GameObject go = new GameObject("Painel de Progresso de Aprendizado");
        go.SetActive(false);
        aberto = go.AddComponent<PainelProgressoAprendizadoUI>();
        aberto.origemVoltar = botaoOrigem;
        aberto.origemPapel = papelOrigem;
        aberto.aoFechar = retorno;
        go.SetActive(true);
    }

    public static bool FecharAberto()
    {
        if (aberto == null) return false;
        aberto.Fechar();
        return true;
    }

    private void OnEnable()
    {
        Construir();
        animacao = StartCoroutine(AnimarEntrada());
    }

    private void Construir()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        canvasRect = (RectTransform)transform;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        gameObject.AddComponent<GraphicRaycaster>();
        grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;

        Image fundo = UIAprendizadoUtil.CriarImagem(transform, "Folha de progresso", Color.white);
        AplicarArteDoPapel(fundo);
        // A folha intercepta cliques vazios, evitando fechar por clicar entre as barras.
        fundo.raycastTarget = true;
        cartao = fundo.rectTransform;
        Retangulo(cartao, Vector2.zero, tamanhoPainel, new Vector2(.5f, .5f));

        Texto(cartao, "Titulo", "MEU PROGRESSO", 42f, Tinta, TextAlignmentOptions.Center,
            new Vector2(0f, -88f), new Vector2(540f, 58f), new Vector2(.5f, 1f));
        Texto(cartao, "Subtitulo", "Tudo o que você aprendeu até agora", 23f,
            new Color(.38f, .33f, .28f, 1f), TextAlignmentOptions.Center,
            new Vector2(0f, -137f), new Vector2(540f, 36f), new Vector2(.5f, 1f));

        float y = -216f;
        foreach (ConteudoMatematico conteudo in Enum.GetValues(typeof(ConteudoMatematico)))
        {
            CriarLinha(conteudo, y);
            y -= 116f;
        }

        overall = Texto(cartao, "Overall", "PROGRESSO GERAL  0%", 26f, Tinta,
            TextAlignmentOptions.Center, new Vector2(0f, 145f), new Vector2(540f, 40f), new Vector2(.5f, 0f));
        CriarBotaoVoltar();
        Canvas.ForceUpdateCanvases();
        AjustarAoEspaco();
    }

    private void CriarLinha(ConteudoMatematico conteudo, float y)
    {
        Color cor = RotulosConteudo.Cor(conteudo);
        Color corTexto = Color.Lerp(Tinta, cor, .48f);
        Texto(cartao, $"Nome {conteudo}", RotulosConteudo.Obter(conteudo).ToUpperInvariant(),
            30f, corTexto, TextAlignmentOptions.MidlineLeft,
            new Vector2(-270f, y), new Vector2(440f, 42f), new Vector2(.5f, 1f), new Vector2(0f, .5f));
        TMP_Text percentual = Texto(cartao, $"Percentual {conteudo}", "0%", 28f, corTexto,
            TextAlignmentOptions.MidlineRight, new Vector2(270f, y), new Vector2(90f, 42f),
            new Vector2(.5f, 1f), new Vector2(1f, .5f));

        Image trilho = UIAprendizadoUtil.CriarImagem(cartao, $"Trilho {conteudo}",
            new Color(.22f, .18f, .15f, .13f), true);
        trilho.raycastTarget = false;
        Retangulo(trilho.rectTransform, new Vector2(0f, y - 42f), new Vector2(LarguraBarra, 24f), new Vector2(.5f, 1f));
        Image mascara = UIAprendizadoUtil.CriarImagem(trilho.transform, "Mascara", Color.white, true);
        mascara.raycastTarget = false;
        RectTransform preenchimento = mascara.rectTransform;
        preenchimento.anchorMin = new Vector2(0f, 0f);
        preenchimento.anchorMax = new Vector2(0f, 1f);
        preenchimento.pivot = new Vector2(0f, .5f);
        preenchimento.anchoredPosition = Vector2.zero;
        preenchimento.sizeDelta = Vector2.zero;
        mascara.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Image baseCor = UIAprendizadoUtil.CriarImagem(mascara.transform, "Cor da materia",
            new Color(cor.r, cor.g, cor.b, .24f));
        Esticar(baseCor.rectTransform);
        baseCor.raycastTarget = false;
        Image pontos = UIAprendizadoUtil.CriarImagem(mascara.transform, "Pontilhado", cor);
        pontos.sprite = UIAprendizadoUtil.ObterSpritePontilhado();
        pontos.type = Image.Type.Tiled;
        pontos.raycastTarget = false;
        Esticar(pontos.rectTransform);

        float valor = ProgressoAprendizado.Instance != null ? ProgressoAprendizado.Instance.Obter(conteudo).valor : 0f;
        linhas.Add(new Linha { preenchimento = preenchimento, pontilhado = pontos,
            percentual = percentual, valor = Mathf.Clamp01(valor), cor = cor });
    }

    private void AplicarArteDoPapel(Image destino)
    {
        if (origemPapel == null || origemPapel.sprite == null) return;
        Sprite original = origemPapel.sprite;
        Rect r = original.rect;
        // Margens transparentes medidas na arte paper.png. O bitmap permanece original.
        Rect areaPapel = new Rect(r.x + r.width * .15f, r.y + r.height * .09f,
            r.width * .70f, r.height * .85f);
        papelRecortado = Sprite.Create(original.texture, areaPapel, new Vector2(.5f, .5f), original.pixelsPerUnit);
        papelRecortado.name = "Papel original - area util";
        destino.sprite = papelRecortado;
        destino.type = Image.Type.Simple;
        destino.preserveAspect = false;
    }

    private void CriarBotaoVoltar()
    {
        GameObject go;
        if (origemVoltar != null)
        {
            go = Instantiate(origemVoltar.gameObject, cartao);
            voltar = go.GetComponent<Button>();
        }
        else
        {
            go = new GameObject("Voltar", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HoverScale));
            go.transform.SetParent(cartao, false);
            go.GetComponent<Image>().color = Color.clear;
            voltar = go.GetComponent<Button>();
        }
        go.name = "Voltar";
        RectTransform rect = (RectTransform)go.transform;
        Retangulo(rect, new Vector2(0f, 76f), new Vector2(240f, 54f), new Vector2(.5f, 0f));
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            label = UIAprendizadoUtil.CriarTexto(go.transform, "Texto", "", 30f, TextAlignmentOptions.Center);
        label.text = "< VOLTAR";
        label.enableAutoSizing = false;
        label.fontSize = 30f;
        label.color = Tinta;
        label.alignment = TextAlignmentOptions.Midline;
        label.margin = Vector4.zero;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.localRotation = Quaternion.identity;
        label.rectTransform.localScale = Vector3.one;
        Esticar(label.rectTransform);
        foreach (Graphic graphic in go.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
        label.raycastTarget = true;
        Image imagem = go.GetComponent<Image>();
        if (imagem != null) imagem.enabled = false;
        voltar.targetGraphic = label;
        voltar.onClick = new Button.ButtonClickedEvent();
        voltar.onClick.AddListener(Fechar);
        foreach (Shadow sombra in label.GetComponents<Shadow>()) sombra.enabled = false;
        go.GetComponent<HoverScale>()?.ReconfigurarBaseVisual();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
    }

    private TMP_Text Texto(Transform pai, string nome, string conteudo, float tamanho, Color cor,
        TextAlignmentOptions alinhamento, Vector2 pos, Vector2 size, Vector2 anchor, Vector2? pivot = null)
    {
        TMP_Text t = UIAprendizadoUtil.CriarTexto(pai, nome, conteudo, tamanho, alinhamento);
        if (origemVoltar != null)
        {
            TMP_Text fonteOriginal = origemVoltar.GetComponentInChildren<TMP_Text>(true);
            if (fonteOriginal != null) t.font = fonteOriginal.font;
        }
        t.color = cor;
        t.enableAutoSizing = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        foreach (Shadow sombra in t.GetComponents<Shadow>()) sombra.enabled = false;
        Retangulo(t.rectTransform, pos, size, anchor, pivot);
        return t;
    }

    private static void Retangulo(RectTransform r, Vector2 pos, Vector2 size, Vector2 anchor, Vector2? pivot = null)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot ?? new Vector2(.5f, .5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    private static void Esticar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(.5f, .5f);
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private void LateUpdate()
    {
        AjustarAoEspaco();
        // No menu principal nao existe a acao de pausa usada pelas fases.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MainMenu" &&
            ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
             (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)))
            Fechar();
    }

    private void AjustarAoEspaco()
    {
        if (cartao == null || canvasRect == null) return;
        float escala = Mathf.Min(1f, (canvasRect.rect.width - 64f) / tamanhoPainel.x,
            (canvasRect.rect.height - 64f) / tamanhoPainel.y);
        cartao.localScale = Vector3.one * Mathf.Max(.1f, escala) * fatorEntrada;
    }

    private IEnumerator AnimarEntrada()
    {
        for (float t = 0f; t < DuracaoAnimacaoPainel; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / DuracaoAnimacaoPainel);
            grupo.alpha = p;
            fatorEntrada = Mathf.Lerp(EscalaInicialPainel, 1f, p);
            yield return null;
        }
        grupo.alpha = 1f;
        fatorEntrada = 1f;
        float soma = 0f;
        foreach (Linha linha in linhas)
        {
            soma += linha.valor;
            yield return AnimarLinha(linha);
        }
        overall.text = $"PROGRESSO GERAL  {Mathf.RoundToInt(soma / Mathf.Max(1, linhas.Count) * 100f)}%";
        animacao = null;
    }

    private static IEnumerator AnimarLinha(Linha linha)
    {
        for (float t = 0f; t < .38f; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / .38f);
            linha.preenchimento.sizeDelta = new Vector2(LarguraBarra * linha.valor * p, 0f);
            linha.percentual.text = Mathf.RoundToInt(linha.valor * p * 100f) + "%";
            linha.pontilhado.color = Color.Lerp(linha.cor, Color.white, Mathf.Sin(p * Mathf.PI) * .3f);
            yield return null;
        }
        linha.preenchimento.sizeDelta = new Vector2(LarguraBarra * linha.valor, 0f);
        linha.percentual.text = Mathf.RoundToInt(linha.valor * 100f) + "%";
        linha.pontilhado.color = linha.cor;
        yield return new WaitForSecondsRealtime(.06f);
    }

    private void Fechar()
    {
        if (fechando) return;
        fechando = true;
        if (animacao != null) StopCoroutine(animacao);
        grupo.interactable = false;
        animacao = StartCoroutine(AnimarSaida());
    }

    private IEnumerator AnimarSaida()
    {
        float alpha = grupo.alpha;
        float escala = fatorEntrada;
        for (float t = 0f; t < DuracaoAnimacaoPainel; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.SmoothStep(0f, 1f, t / DuracaoAnimacaoPainel);
            grupo.alpha = alpha * (1f - p);
            fatorEntrada = Mathf.Lerp(escala, EscalaInicialPainel, p);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (aberto == this) aberto = null;
        if (papelRecortado != null) Destroy(papelRecortado);
        aoFechar?.Invoke();
    }
}
