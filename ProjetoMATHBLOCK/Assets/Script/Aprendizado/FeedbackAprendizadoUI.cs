using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>Barra capsular global, discreta e nao bloqueante.</summary>
public sealed class FeedbackAprendizadoUI : MonoBehaviour
{
    private readonly Queue<(ConteudoMatematico conteudo, float anterior, float atual)> fila = new();
    private CanvasGroup grupo;
    private RectTransform raiz;
    private RectTransform preenchimento;
    private Image preenchimentoImagem;
    private TMP_Text materia;
    private TMP_Text percentual;
    private Coroutine rotina;
    private GameObject painelDev;
    private const float Largura = 560f;

    public static void GarantirInstancia()
    {
        if (FindFirstObjectByType<FeedbackAprendizadoUI>() == null)
            new GameObject("Feedback de Aprendizado").AddComponent<FeedbackAprendizadoUI>();
    }

    private void Awake() => Construir();
    private void OnEnable() => ProgressoAprendizado.OnProgressoAtualizado += Enfileirar;
    private void OnDisable() => ProgressoAprendizado.OnProgressoAtualizado -= Enfileirar;

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Keyboard teclado = Keyboard.current;
        bool ctrl = teclado != null && (teclado.leftCtrlKey.isPressed || teclado.rightCtrlKey.isPressed);
        if (ctrl && teclado.dKey.wasPressedThisFrame && painelDev != null)
            painelDev.SetActive(!painelDev.activeSelf);
#endif
    }

    private void Enfileirar(ConteudoMatematico conteudo, float anterior, float atual)
    {
        fila.Enqueue((conteudo, anterior, atual));
        if (rotina == null) rotina = StartCoroutine(ExibirFila());
    }

    private IEnumerator ExibirFila()
    {
        while (fila.Count > 0)
        {
            var item = fila.Dequeue();
            Color cor = RotulosConteudo.Cor(item.conteudo);
            preenchimentoImagem.color = cor;
            string corHex = ColorUtility.ToHtmlStringRGB(cor);
            materia.text = $"SEU PROGRESSO EM: <color=#{corHex}><b>{RotulosConteudo.Obter(item.conteudo).ToUpperInvariant()}</b></color>";
            grupo.alpha = 0f;
            raiz.anchoredPosition = new Vector2(0f, -35f);
            raiz.localScale = Vector3.one * .96f;
            Aplicar(item.anterior);

            for (float t = 0; t < .28f; t += Time.unscaledDeltaTime)
            {
                float p = Suave(t / .28f);
                grupo.alpha = p;
                raiz.anchoredPosition = Vector2.Lerp(new Vector2(0f, -35f), new Vector2(0f, -25f), p);
                raiz.localScale = Vector3.Lerp(Vector3.one * .96f, Vector3.one, p);
                yield return null;
            }

            float proximaParticula = 0f;
            for (float t = 0; t < .95f; t += Time.unscaledDeltaTime)
            {
                float p = Suave(t / .95f);
                float valor = Mathf.Lerp(item.anterior, item.atual, p);
                Aplicar(valor);
                float pulso = (Mathf.Sin(t * 15f) + 1f) * .5f;
                preenchimentoImagem.color = Color.Lerp(cor, Color.white, pulso * .62f);
                raiz.anchoredPosition = new Vector2(Mathf.Sin(t * 72f) * 1.7f * (1f - p * .45f), -25f);
                if (t >= proximaParticula)
                {
                    proximaParticula += .055f;
                    CriarParticula(cor, valor);
                }
                yield return null;
            }
            Aplicar(item.atual);
            preenchimentoImagem.color = cor;
            yield return ShakeFinal();
            yield return new WaitForSecondsRealtime(1.35f);

            for (float t = 0; t < .38f; t += Time.unscaledDeltaTime)
            {
                float p = Suave(t / .38f);
                grupo.alpha = 1f - p;
                raiz.anchoredPosition = new Vector2(0f, -25f - 8f * p);
                yield return null;
            }
        }
        grupo.alpha = 0f;
        rotina = null;
    }

    private void Aplicar(float valor)
    {
        valor = Mathf.Clamp01(valor);
        preenchimento.sizeDelta = new Vector2(Largura * valor, 0f);
        percentual.text = Mathf.RoundToInt(valor * 100f) + "%";
    }

    private void CriarParticula(Color cor, float progresso)
    {
        Image imagem = UIAprendizadoUtil.CriarImagem(raiz, "Particula", cor, true);
        RectTransform r = imagem.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, 0f);
        r.sizeDelta = Vector2.one * Random.Range(4f, 8f);
        r.anchoredPosition = new Vector2(-Largura * .5f + Largura * progresso, 18f);
        StartCoroutine(AnimarParticula(imagem, r, cor));
    }

    private IEnumerator AnimarParticula(Image imagem, RectTransform r, Color cor)
    {
        Vector2 origem = r.anchoredPosition;
        Vector2 destino = origem + new Vector2(Random.Range(-16f, 16f), Random.Range(16f, 35f));
        float giro = Random.Range(-100f, 100f);
        for (float t = 0; t < .42f; t += Time.unscaledDeltaTime)
        {
            float p = t / .42f;
            r.anchoredPosition = Vector2.Lerp(origem, destino, p) + Vector2.down * (18f * p * p);
            r.localRotation = Quaternion.Euler(0, 0, giro * p);
            imagem.color = new Color(cor.r, cor.g, cor.b, 1f - p);
            yield return null;
        }
        Destroy(imagem.gameObject);
    }

    private IEnumerator ShakeFinal()
    {
        for (float t = 0; t < .22f; t += Time.unscaledDeltaTime)
        {
            float intensidade = (1f - t / .22f) * 5f;
            raiz.anchoredPosition = new Vector2(Mathf.Sin(t * 95f) * intensidade, -25f);
            yield return null;
        }
        raiz.anchoredPosition = new Vector2(0f, -25f);
    }

    private void Construir()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 250;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f); gameObject.AddComponent<GraphicRaycaster>();

        raiz = UIAprendizadoUtil.CriarRetangulo(transform, "Barra Global", new Vector2(620f, 68f));
        raiz.anchorMin = raiz.anchorMax = raiz.pivot = new Vector2(.5f, 1f);
        raiz.anchoredPosition = new Vector2(0f, -25f);
        grupo = raiz.gameObject.AddComponent<CanvasGroup>(); grupo.alpha = 0f; grupo.blocksRaycasts = false;

        materia = UIAprendizadoUtil.CriarTexto(raiz, "Materia", "SEU PROGRESSO EM: OPERADORES", 19f, TextAlignmentOptions.Left);
        materia.color = new Color(1f, 1f, 1f, .94f);
        UIAprendizadoUtil.DefinirRetangulo(materia.rectTransform, new Vector2(30f, -1f), new Vector2(430f, 25f), new Vector2(0f, 1f));
        percentual = UIAprendizadoUtil.CriarTexto(raiz, "Percentual", "0%", 17f, TextAlignmentOptions.Right);
        percentual.color = new Color(1f, 1f, 1f, .8f);
        UIAprendizadoUtil.DefinirRetangulo(percentual.rectTransform, new Vector2(-30f, -2f), new Vector2(80f, 24f), new Vector2(1f, 1f));

        Image trilho = UIAprendizadoUtil.CriarImagem(raiz, "Vazio", new Color(.42f, .44f, .48f, .36f), true);
        UIAprendizadoUtil.DefinirRetangulo(trilho.rectTransform, new Vector2(30f, 5f), new Vector2(Largura, 28f), new Vector2(0f, 0f));
        preenchimentoImagem = UIAprendizadoUtil.CriarImagem(trilho.transform, "Progresso", Color.green, true);
        preenchimento = preenchimentoImagem.rectTransform;
        preenchimento.anchorMin = new Vector2(0f, 0f); preenchimento.anchorMax = new Vector2(0f, 1f);
        preenchimento.pivot = new Vector2(0f, .5f); preenchimento.anchoredPosition = Vector2.zero; preenchimento.sizeDelta = Vector2.zero;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CriarBotaoDev();
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void CriarBotaoDev()
    {
        RectTransform painel = UIAprendizadoUtil.CriarRetangulo(transform, "Painel DEV", new Vector2(170f, 210f));
        painelDev = painel.gameObject;
        painel.anchorMin = painel.anchorMax = painel.pivot = new Vector2(0f, 1f);
        painel.anchoredPosition = new Vector2(16f, -16f);

        CriarBotaoDev(painel, "OPERADORES +10%", 0f,
            () => AdicionarDev(ConteudoMatematico.Operadores));
        CriarBotaoDev(painel, "ESCALA +10%", -41f,
            () => AdicionarDev(ConteudoMatematico.EscalaEEspaco));
        CriarBotaoDev(painel, "EQUAÇÕES +10%", -82f,
            () => AdicionarDev(ConteudoMatematico.Equacoes));
        CriarBotaoDev(painel, "EXPRESSÕES +10%", -123f,
            () => AdicionarDev(ConteudoMatematico.Expressoes));
        CriarBotaoDev(painel, "TESTAR MENU", -169f,
            () => ResultadosAprendizadoUI.MostrarDev(SceneManager.GetActiveScene().name));
        painelDev.SetActive(false);
    }

    private static void AdicionarDev(ConteudoMatematico conteudo)
    {
        if (ProgressoAprendizado.Instance != null)
            ProgressoAprendizado.Instance.AdicionarProgressoDev(conteudo, .1f);
    }

    private static void CriarBotaoDev(Transform pai, string rotulo, float y, UnityEngine.Events.UnityAction acao)
    {
        Image fundo = UIAprendizadoUtil.CriarImagem(pai, rotulo, new Color(.08f, .09f, .13f, .86f), true);
        RectTransform r = fundo.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(0f, y);
        r.sizeDelta = new Vector2(164f, 35f);
        Button botao = fundo.gameObject.AddComponent<Button>();
        botao.targetGraphic = fundo;
        botao.onClick.AddListener(acao);
        TMP_Text texto = UIAprendizadoUtil.CriarTexto(fundo.transform, "Texto", rotulo, 14f, TextAlignmentOptions.Center);
        texto.rectTransform.anchorMin = Vector2.zero;
        texto.rectTransform.anchorMax = Vector2.one;
        texto.rectTransform.offsetMin = texto.rectTransform.offsetMax = Vector2.zero;
    }
#endif

    private static float Suave(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
}

public static class RotulosConteudo
{
    public static string Obter(ConteudoMatematico c) => c switch
    {
        ConteudoMatematico.Operadores => "Operadores",
        ConteudoMatematico.EscalaEEspaco => "Escala e Espaço",
        ConteudoMatematico.Equacoes => "Equações",
        ConteudoMatematico.Expressoes => "Expressões",
        _ => c.ToString()
    };

    public static Color Cor(ConteudoMatematico c) => c switch
    {
        ConteudoMatematico.Operadores => new Color32(57, 218, 102, 255),
        ConteudoMatematico.EscalaEEspaco => new Color32(82, 159, 255, 255),
        ConteudoMatematico.Equacoes => new Color32(190, 103, 255, 255),
        ConteudoMatematico.Expressoes => new Color32(255, 157, 63, 255),
        _ => Color.white
    };
}

public static class UIAprendizadoUtil
{
    private static Sprite arredondado;
    public static RectTransform CriarRetangulo(Transform pai, string nome, Vector2 tamanho)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform)); go.transform.SetParent(pai, false);
        RectTransform r = go.GetComponent<RectTransform>(); r.sizeDelta = tamanho; return r;
    }
    public static Image CriarImagem(Transform pai, string nome, Color cor, bool arredondar = false)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(pai, false);
        Image image = go.GetComponent<Image>(); image.color = cor;
        if (arredondar) { image.sprite = ObterSpriteArredondado(); image.type = Image.Type.Sliced; }
        return image;
    }
    public static TMP_Text CriarTexto(Transform pai, string nome, string texto, float tamanho, TextAlignmentOptions alinhamento)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(pai, false);
        TMP_Text t = go.GetComponent<TMP_Text>(); t.text = texto; t.fontSize = tamanho; t.color = Color.white; t.alignment = alinhamento;
        TMP_FontAsset fonte = Resources.Load<TMP_FontAsset>("Fonts & Materials/Schoolbell-Regular SDF");
        if (fonte != null) t.font = fonte;
        Shadow sombra = go.AddComponent<Shadow>(); sombra.effectColor = new Color(0, 0, 0, .7f); sombra.effectDistance = new Vector2(1.5f, -1.5f);
        return t;
    }
    public static void DefinirRetangulo(RectTransform r, Vector2 posicao, Vector2 tamanho, Vector2 ancora)
    { r.anchorMin = r.anchorMax = r.pivot = ancora; r.anchoredPosition = posicao; r.sizeDelta = tamanho; }
    private static Sprite ObterSpriteArredondado()
    {
        if (arredondado != null) return arredondado;
        const int s = 32; Texture2D tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.name = "Capsula UI Runtime";
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float dx = Mathf.Max(0, 7 - Mathf.Min(x, s - 1 - x));
            float dy = Mathf.Max(0, 7 - Mathf.Min(y, s - 1 - y));
            tex.SetPixel(x, y, dx * dx + dy * dy <= 49 ? Color.white : Color.clear);
        }
        tex.Apply();
        arredondado = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(.5f, .5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
        return arredondado;
    }
}
