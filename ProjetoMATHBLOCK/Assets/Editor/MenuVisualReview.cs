using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Inspecao visual em Play: usa os mesmos botoes do jogador, sem alterar progresso ou cenas.</summary>
public static class MenuVisualReview
{
    private const string Menu = "MATHBLOCK/Revisao visual/";
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private const string ChaveTamanho = "MATHBLOCK.RevisaoVisual.TamanhoOriginal";
    private const string PrefixoTamanho = "MATHBLOCK review ";
    private static string Pasta => EditorPrefs.GetString("MATHBLOCK.RevisaoVisual.Pasta",
        @"C:\Users\arbustim\Documents\Codex\2026-08-14\d");

    [MenuItem(Menu + "Aplicar barras solidas e centralizar Sair")]
    public static void AplicarAcabamento()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Saia de Play antes de salvar o acabamento.");
        Transform[] objetos = SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform menu = objetos.First(t => t.name == "StartPaper");
        CentralizarSair(menu);
        var painel = objetos.Select(t => t.GetComponent<PainelProgressoAprendizadoUI>()).First(p => p != null);
        var dados = new SerializedObject(painel);
        SerializedProperty linhas = dados.FindProperty("linhas");
        for (int i = 0; i < linhas.arraySize; i++)
        {
            SerializedProperty linha = linhas.GetArrayElementAtIndex(i);
            var preenchimento = (RectTransform)linha.FindPropertyRelative("preenchimento").objectReferenceValue;
            Image baseCor = preenchimento.Find("Cor da materia").GetComponent<Image>();
            Image pontos = (Image)linha.FindPropertyRelative("pontilhado").objectReferenceValue;
            baseCor.color = linha.FindPropertyRelative("cor").colorValue;
            pontos.color = Color.black;
            pontos.pixelsPerUnitMultiplier = 2f;
            linha.FindPropertyRelative("corBase").objectReferenceValue = baseCor;
            RegistrarAlteracao(baseCor); RegistrarAlteracao(pontos);
        }
        dados.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorSceneManager.SaveScene(menu.gameObject.scene);
    }

    private static void CentralizarSair(Transform menu)
    {
        TMP_Text creditos = menu.Find("Credits").GetComponentInChildren<TMP_Text>(true);
        RectTransform botao = (RectTransform)menu.Find("Exit");
        TMP_Text sair = botao.GetComponentInChildren<TMP_Text>(true);
        sair.text = "× SAIR";
        sair.margin = Vector4.zero;
        sair.alignment = TextAlignmentOptions.Midline;
        creditos.ForceMeshUpdate(true, true);
        sair.ForceMeshUpdate(true, true);
        // Centraliza pela tinta visivel dos textos, nao pelas caixas de mesma largura.
        Vector3 centroCreditos = menu.InverseTransformPoint(creditos.transform.TransformPoint(creditos.textBounds.center));
        Vector3 centroSair = menu.InverseTransformPoint(sair.transform.TransformPoint(sair.textBounds.center));
        botao.anchoredPosition += new Vector2(centroCreditos.x - centroSair.x, 0f);
        RegistrarAlteracao(botao); RegistrarAlteracao(sair);
    }

    [MenuItem(Menu + "Testar feedback visual")]
    public static void TestarFeedback() => Executar(() =>
    {
        FeedbackAprendizadoUI.GarantirInstancia();
        var feedback = UnityEngine.Object.FindFirstObjectByType<FeedbackAprendizadoUI>();
        typeof(FeedbackAprendizadoUI).GetMethod("Enfileirar", Flags)
            .Invoke(feedback, new object[] { ConteudoMatematico.Operadores, .25f, .6f });
    });

    [MenuItem(Menu + "Integrar folha Progresso no menu principal")]
    public static void IntegrarFolhaProgresso()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Saia de Play antes de editar o menu.");
        if (!EditorSceneManager.SaveOpenScenes()) return;
        Scene cena = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        Transform[] objetos = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        if (objetos.Any(t => t.name == "ProgressMenu"))
            throw new InvalidOperationException("ProgressMenu ja existe. Ajuste a folha existente no Inspector.");
        Transform opcoes = objetos.First(t => t.name == "OptionsMenu");
        MenuController controlador = objetos.Select(t => t.GetComponent<MenuController>()).First(c => c != null);
        GameObject folha = UnityEngine.Object.Instantiate(opcoes.gameObject, opcoes.parent);
        folha.name = "ProgressMenu";
        folha.SetActive(false);
        folha.transform.SetSiblingIndex(opcoes.GetSiblingIndex());
        // Conserva exatamente a folha e o Voltar originais; retira apenas copias dos controles de Opcoes.
        foreach (Transform filho in folha.transform.Cast<Transform>().ToArray())
            if (filho.name != "Image" && filho.name != "Back") UnityEngine.Object.DestroyImmediate(filho.gameObject);
        Button voltar = folha.transform.Find("Back").GetComponent<Button>();
        // DistanceBlurOptionUI normaliza o Back de Opcoes em (-8,-35) + (0,-167), escala 1.
        RectTransform rectVoltar = voltar.GetComponent<RectTransform>();
        rectVoltar.anchoredPosition = new Vector2(-8f, -202f);
        rectVoltar.sizeDelta = new Vector2(300f, 72f);
        rectVoltar.localScale = Vector3.one;
        voltar.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(voltar.onClick, controlador.FecharProgresso);
        TMP_FontAsset fonte = voltar.GetComponentInChildren<TMP_Text>(true).font;
        var painel = folha.AddComponent<PainelProgressoAprendizadoUI>();
        var dados = new SerializedObject(painel);
        dados.FindProperty("usaFolhaDaCena").boolValue = true;
        dados.FindProperty("controladorMenu").objectReferenceValue = controlador;

        RectTransform conteudo = UIAprendizadoUtil.CriarRetangulo(folha.transform, "Conteudo Progresso", new Vector2(540f, 600f));
        conteudo.anchorMin = conteudo.anchorMax = conteudo.pivot = new Vector2(.5f, .5f);
        conteudo.anchoredPosition = Vector2.zero;
        Color tinta = new Color(.15f, .12f, .10f, 1f);
        TextoNaFolha(conteudo, fonte, "Titulo", "MEU PROGRESSO", 38f, tinta, 0f, 315f, 540f, TextAlignmentOptions.Center);
        TextoNaFolha(conteudo, fonte, "Subtitulo", "Tudo o que você aprendeu até agora", 21f, tinta, 0f, 275f, 540f, TextAlignmentOptions.Center);
        Sprite pontos = PersistirSprite(UIAprendizadoUtil.ObterSpritePontilhado(), "PontosBarraMenu");
        Sprite capsula = PersistirSprite((Sprite)typeof(UIAprendizadoUtil).GetMethod("ObterSpriteArredondado", Flags).Invoke(null, null), "CapsulaBarraMenu");
        ConteudoMatematico[] materias = (ConteudoMatematico[])Enum.GetValues(typeof(ConteudoMatematico));
        SerializedProperty linhas = dados.FindProperty("linhas");
        linhas.arraySize = materias.Length;
        for (int i = 0; i < materias.Length; i++)
        {
            ConteudoMatematico materia = materias[i];
            float y = 200f - i * 88f;
            Color cor = RotulosConteudo.Cor(materia);
            Color corTexto = Color.Lerp(tinta, cor, .48f);
            TextoNaFolha(conteudo, fonte, "Nome " + materia, RotulosConteudo.Obter(materia).ToUpperInvariant(), 28f, corTexto, -45f, y, 450f, TextAlignmentOptions.MidlineLeft);
            TMP_Text percentual = TextoNaFolha(conteudo, fonte, "Percentual " + materia, "0%", 26f, corTexto, 230f, y, 80f, TextAlignmentOptions.MidlineRight);
            Image trilho = UIAprendizadoUtil.CriarImagem(conteudo, "Trilho " + materia, new Color(.22f, .18f, .15f, .13f));
            trilho.sprite = capsula; trilho.type = Image.Type.Sliced; trilho.raycastTarget = false;
            RectTransform rect = trilho.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0f, y - 27f); rect.sizeDelta = new Vector2(540f, 20f);
            Image mascara = UIAprendizadoUtil.CriarImagem(trilho.transform, "Mascara", Color.white);
            mascara.sprite = capsula; mascara.type = Image.Type.Sliced; mascara.raycastTarget = false;
            RectTransform preenchimento = mascara.rectTransform;
            preenchimento.anchorMin = Vector2.zero; preenchimento.anchorMax = new Vector2(0f, 1f);
            preenchimento.pivot = new Vector2(0f, .5f); preenchimento.anchoredPosition = preenchimento.sizeDelta = Vector2.zero;
            mascara.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Image baseCor = UIAprendizadoUtil.CriarImagem(mascara.transform, "Cor da materia", cor);
            EsticarNaFolha(baseCor);
            Image pontilhado = UIAprendizadoUtil.CriarImagem(mascara.transform, "Pontilhado", Color.black);
            pontilhado.sprite = pontos; pontilhado.type = Image.Type.Tiled; EsticarNaFolha(pontilhado);
            SerializedProperty linha = linhas.GetArrayElementAtIndex(i);
            linha.FindPropertyRelative("conteudo").enumValueIndex = (int)materia;
            linha.FindPropertyRelative("preenchimento").objectReferenceValue = preenchimento;
            linha.FindPropertyRelative("pontilhado").objectReferenceValue = pontilhado;
            linha.FindPropertyRelative("corBase").objectReferenceValue = baseCor;
            linha.FindPropertyRelative("percentual").objectReferenceValue = percentual;
            linha.FindPropertyRelative("cor").colorValue = cor;
            linha.FindPropertyRelative("valor").floatValue = 0f;
        }
        dados.FindProperty("overall").objectReferenceValue = TextoNaFolha(conteudo, fonte, "Overall", "PROGRESSO GERAL  0%", 24f, tinta, 0f, -133f, 540f, TextAlignmentOptions.Center);
        dados.ApplyModifiedPropertiesWithoutUndo();
        var referencias = new SerializedObject(controlador);
        referencias.FindProperty("menuProgresso").objectReferenceValue = folha;
        referencias.ApplyModifiedPropertiesWithoutUndo();
        RegistrarAlteracao(controlador);
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        Selection.activeGameObject = folha;
        Debug.Log("[Revisao visual] ProgressMenu salvo com a folha, posicao, escala e Voltar de OptionsMenu. Usa OpenPaper do menu principal.");
    }

    private static TMP_Text TextoNaFolha(Transform pai, TMP_FontAsset fonte, string nome, string legenda, float tamanho, Color cor, float x, float y, float largura, TextAlignmentOptions alinhamento)
    {
        TMP_Text texto = UIAprendizadoUtil.CriarTexto(pai, nome, legenda, tamanho, alinhamento);
        texto.font = fonte; texto.color = cor; texto.raycastTarget = false;
        texto.enableAutoSizing = false; texto.textWrappingMode = TextWrappingModes.NoWrap;
        foreach (Shadow sombra in texto.GetComponents<Shadow>()) UnityEngine.Object.DestroyImmediate(sombra);
        RectTransform rect = texto.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(largura, 44f);
        return texto;
    }

    private static void EsticarNaFolha(Image imagem)
    {
        imagem.raycastTarget = false;
        RectTransform rect = imagem.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static Sprite PersistirSprite(Sprite origem, string nome)
    {
        string caminho = "Assets/Resources/Aprendizado/" + nome + ".asset";
        Sprite salvo = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        if (salvo != null) return salvo;
        Texture2D textura = UnityEngine.Object.Instantiate(origem.texture);
        textura.name = nome;
        AssetDatabase.CreateAsset(textura, caminho);
        Sprite sprite = Sprite.Create(textura, origem.rect, new Vector2(.5f, .5f), origem.pixelsPerUnit, 0, SpriteMeshType.FullRect, origem.border);
        sprite.name = nome;
        AssetDatabase.AddObjectToAsset(sprite, textura);
        AssetDatabase.SaveAssets();
        return sprite;
    }

    [MenuItem(Menu + "Salvar opcao Progresso nas cenas")]
    public static void SalvarOpcaoNasCenas()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Saia de Play antes de editar as cenas.");
        if (!EditorSceneManager.SaveOpenScenes()) return;
        SceneSetup[] abertas = EditorSceneManager.GetSceneManagerSetup();
        const string prefab = "Assets/Prefab/HUD/Gameplay HUD.prefab";
        GameObject hud = PrefabUtility.LoadPrefabContents(prefab);
        try
        {
            ConfigurarOpcao(hud.GetComponentsInChildren<Transform>(true),
                hud.GetComponentInChildren<MenuController>(true), false);
            PrefabUtility.SaveAsPrefabAsset(hud, prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(hud); }

        try
        {
            foreach (string nome in new[] { "MainMenu", "Fase 1", "Fase 2", "Fase 3", "Fase 4" })
            {
                Scene cena = EditorSceneManager.OpenScene("Assets/Scenes/" + nome + ".unity", OpenSceneMode.Single);
                Transform[] objetos = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                MenuController controlador = objetos.Select(t => t.GetComponent<MenuController>()).Where(c => c != null)
                    .OrderByDescending(c => c.GetComponentsInParent<Transform>(true).Any(t => t.name == "Gameplay HUD"))
                    .First();
                ConfigurarOpcao(objetos, controlador, nome == "MainMenu");
                EditorSceneManager.MarkSceneDirty(cena);
                if (!EditorSceneManager.SaveScene(cena)) throw new IOException("Nao foi possivel salvar " + nome);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(abertas); }
        Debug.Log("[Revisao visual] Progresso salvo no prefab da HUD e nas cinco cenas. Posicoes e OnClick editaveis no Inspector.");
    }

    private static void ConfigurarOpcao(IEnumerable<Transform> objetos, MenuController controlador, bool inicial)
    {
        Transform menu = objetos.First(t => t.name == (inicial ? "StartPaper" : "MenuPaper"));
        Button opcoes = menu.Find("Options").GetComponent<Button>();
        Transform progresso = menu.Find("Progress");
        if (progresso == null)
        {
            progresso = UnityEngine.Object.Instantiate(opcoes.gameObject, menu).transform;
            progresso.name = "Progress";
            progresso.SetSiblingIndex(opcoes.transform.GetSiblingIndex() + 1);
        }
        Button botao = progresso.GetComponent<Button>();
        botao.onClick = new Button.ButtonClickedEvent();
        UnityEventTools.AddPersistentListener(botao.onClick, controlador.AbrirProgresso);
        RegistrarAlteracao(botao);

        var referencias = new SerializedObject(controlador);
        referencias.FindProperty("menuInicial").objectReferenceValue = menu.gameObject;
        referencias.FindProperty("botaoProgresso").objectReferenceValue = botao;
        referencias.ApplyModifiedPropertiesWithoutUndo();
        RegistrarAlteracao(controlador);

        string[] nomes = { inicial ? "Start" : "Back", "Options", "Progress", "Credits", "Exit" };
        string[] legendas = { inicial ? "►<pos=48>INICIAR" : "►<pos=48>VOLTAR", "≡<pos=48>OPÇÕES", "≡<pos=48>PROGRESSO", "@<pos=48>CRÉDITOS", "×<pos=48>SAIR" };
        RectTransform primeiro = (RectTransform)menu.Find(nomes[0]);
        RectTransform ultimo = (RectTransform)menu.Find("Exit");
        float centroY = (primeiro.anchoredPosition.y + ultimo.anchoredPosition.y) * .5f;
        float centroX = ((RectTransform)menu.Find("Image")).anchoredPosition.x;
        float escala = opcoes.transform.localScale.x;
        float espacamento = inicial ? 96f : 72f;
        for (int i = 0; i < nomes.Length; i++)
        {
            RectTransform rect = (RectTransform)menu.Find(nomes[i]);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(centroX, centroY + (2 - i) * espacamento);
            rect.localRotation = inicial ? Quaternion.Euler(0f, 0f, 1.768f) : Quaternion.identity;
            rect.localScale = new Vector3(escala, escala, 1f);
            rect.sizeDelta = new Vector2(380f, 72f);
            RegistrarAlteracao(rect);
            TMP_Text texto = rect.GetComponentInChildren<TMP_Text>(true);
            texto.text = legendas[i];
            texto.enableAutoSizing = false;
            texto.fontSize = 62f;
            texto.alignment = TextAlignmentOptions.MidlineLeft;
            texto.margin = new Vector4(12f, 0f, 0f, 0f);
            texto.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform textoRect = texto.rectTransform;
            textoRect.anchorMin = Vector2.zero; textoRect.anchorMax = Vector2.one;
            textoRect.pivot = new Vector2(.5f, .5f);
            textoRect.anchoredPosition = textoRect.sizeDelta = Vector2.zero;
            textoRect.localRotation = Quaternion.identity; textoRect.localScale = Vector3.one;
            RegistrarAlteracao(textoRect); RegistrarAlteracao(texto);
        }
        if (inicial) CentralizarSair(menu);
    }

    private static void RegistrarAlteracao(UnityEngine.Object objeto)
    {
        EditorUtility.SetDirty(objeto);
        if (PrefabUtility.IsPartOfPrefabInstance(objeto))
            PrefabUtility.RecordPrefabInstancePropertyModifications(objeto);
    }

    [MenuItem(Menu + "Abrir menu")]
    public static void AbrirMenu() => Executar(() => Controlador().AbrirInicial());

    [MenuItem(Menu + "Progresso")]
    public static void Progresso() => Executar(() => Botao("Progress", false).onClick.Invoke());

    [MenuItem(Menu + "Voltar")]
    public static void Voltar() => Executar(() => Botao("Voltar", true).onClick.Invoke());

    [MenuItem(Menu + "Esc")]
    public static void Esc() => Executar(() => Controlador().OnBackButtonPressed());

    [MenuItem(Menu + "Opcoes")]
    public static void Opcoes() => Executar(() => Botao("Options", false).onClick.Invoke());

    [MenuItem(Menu + "Creditos")]
    public static void Creditos() => Executar(() => Botao("Credits", false).onClick.Invoke());

    [MenuItem(Menu + "Barras de demonstracao")]
    public static void DemonstrarBarras() => Executar(() =>
    {
        var painel = UnityEngine.Object.FindFirstObjectByType<PainelProgressoAprendizadoUI>();
        if (painel == null) throw new InvalidOperationException("Abra Progresso primeiro.");
        painel.StopAllCoroutines();
        var linhas = (System.Collections.IEnumerable)typeof(PainelProgressoAprendizadoUI).GetField("linhas", Flags).GetValue(painel);
        int indice = 0;
        foreach (object linha in linhas)
            linha.GetType().GetField("valor", Flags).SetValue(linha, Mathf.Min(1f, ++indice * .25f));
        // Apenas valores de apresentacao: nao altera o servico nem o save do jogador.
        bool folhaDaCena = (bool)typeof(PainelProgressoAprendizadoUI).GetField("usaFolhaDaCena", Flags).GetValue(painel);
        var rotina = (System.Collections.IEnumerator)typeof(PainelProgressoAprendizadoUI).GetMethod(folhaDaCena ? "AnimarBarras" : "AnimarEntrada", Flags).Invoke(painel, null);
        painel.StartCoroutine(rotina);
    });

    [MenuItem(Menu + "Hover progresso entrar")]
    public static void HoverEntrar() => Executar(() => ExecuteEvents.Execute(Botao("Progress", false).gameObject,
        new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler));

    [MenuItem(Menu + "Hover progresso sair")]
    public static void HoverSair() => Executar(() => ExecuteEvents.Execute(Botao("Progress", false).gameObject,
        new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler));

    [MenuItem(Menu + "Capturar e relatar")]
    public static void CapturarERelatar() => Executar(() =>
    {
        Canvas.ForceUpdateCanvases();
        Directory.CreateDirectory(Pasta);
        var relatorio = new Relatorio
        {
            cena = SceneManager.GetActiveScene().name,
            largura = Screen.width, altura = Screen.height,
            timeScale = Time.timeScale, cursorVisivel = Cursor.visible,
            cursorTravado = Cursor.lockState.ToString(),
            captura = Path.Combine(Pasta, "menu-review.png")
        };
        foreach (var raiz in UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
        {
            if (raiz.name != "MenuPaper" && raiz.name != "StartPaper" && raiz.name != "OptionsMenu" && raiz.GetComponent<PainelProgressoAprendizadoUI>() == null) continue;
            RectTransform papel = raiz.name == "MenuPaper" || raiz.name == "StartPaper" || raiz.name == "OptionsMenu" || raiz.name == "ProgressMenu"
                ? raiz.Find("Image") as RectTransform
                : raiz.GetComponentsInChildren<Image>().FirstOrDefault(i => i.name == "Folha de progresso")?.rectTransform;
            if (papel == null) continue;
            Rect areaPapel = RetanguloNaTela(papel);
            relatorio.papeis.Add(new Papel { caminho = Caminho(papel), area = areaPapel });
            foreach (RectTransform rect in raiz.GetComponentsInChildren<RectTransform>())
            {
                TMP_Text texto = rect.GetComponent<TMP_Text>();
                Button botao = rect.GetComponent<Button>();
                if (texto == null && botao == null && !rect.name.StartsWith("Trilho")) continue;
                Rect area = RetanguloNaTela(rect);
                relatorio.elementos.Add(new Elemento
                {
                    caminho = Caminho(rect), area = area,
                    posicao = rect.anchoredPosition, tamanho = rect.rect.size,
                    escala = rect.localScale, rotacao = rect.localEulerAngles,
                    ancoraMin = rect.anchorMin, ancoraMax = rect.anchorMax, pivo = rect.pivot,
                    foraDoRetanguloDoPapel = area.xMin < areaPapel.xMin - 1f || area.xMax > areaPapel.xMax + 1f
                        || area.yMin < areaPapel.yMin - 1f || area.yMax > areaPapel.yMax + 1f,
                    texto = texto != null ? texto.text : null,
                    centroVisualNoMenu = texto != null ? (Vector2)raiz.InverseTransformPoint(texto.transform.TransformPoint(texto.textBounds.center)) : Vector2.zero,
                    fonte = texto != null && texto.font != null ? texto.font.name : null,
                    tamanhoFonte = texto != null ? texto.fontSize : 0f,
                    autoTamanho = texto != null && texto.enableAutoSizing,
                    interativo = botao != null && botao.IsInteractable()
                });
            }
        }
        Capturar(relatorio.captura);
        File.WriteAllText(Path.Combine(Pasta, "menu-review.json"), JsonUtility.ToJson(relatorio, true));
        Debug.Log("[Revisao visual] Captura e layout salvos em " + Pasta + ". Progresso do jogador nao foi alterado.");
    });

    [MenuItem(Menu + "Resolucao/1280x720")]
    public static void HD() => DefinirResolucao(1280, 720);
    [MenuItem(Menu + "Resolucao/1920x1080")]
    public static void FullHD() => DefinirResolucao(1920, 1080);
    [MenuItem(Menu + "Resolucao/1024x768")]
    public static void QuatroPorTres() => DefinirResolucao(1024, 768);

    [MenuItem(Menu + "Resolucao/Restaurar original")]
    public static void RestaurarResolucao() => Executar(() =>
    {
        int indice = SessionState.GetInt(ChaveTamanho, -1);
        if (indice < 0) return;
        var view = GameView();
        view.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(view, indice);
        object grupo = GrupoTamanhos();
        int total = (int)grupo.GetType().GetMethod("GetTotalCount", Flags).Invoke(grupo, null);
        int nativos = (int)grupo.GetType().GetMethod("GetBuiltinCount", Flags).Invoke(grupo, null);
        for (int i = total - 1; i >= nativos; i--)
        {
            object tamanho = grupo.GetType().GetMethod("GetGameViewSize", Flags).Invoke(grupo, new object[] { i });
            string nome = tamanho.GetType().GetProperty("baseText", Flags)?.GetValue(tamanho) as string;
            if (nome != null && nome.StartsWith(PrefixoTamanho, StringComparison.Ordinal))
                grupo.GetType().GetMethod("RemoveCustomSize", Flags).Invoke(grupo, new object[] { i });
        }
        SessionState.EraseInt(ChaveTamanho);
        view.Repaint();
    });

    private static void DefinirResolucao(int largura, int altura) => Executar(() =>
    {
        var view = GameView();
        PropertyInfo selecao = view.GetType().GetProperty("selectedSizeIndex", Flags);
        if (selecao == null) throw new InvalidOperationException("Esta versao nao expoe a resolucao da Game View.");
        if (SessionState.GetInt(ChaveTamanho, -1) < 0)
            SessionState.SetInt(ChaveTamanho, (int)selecao.GetValue(view));
        object grupo = GrupoTamanhos();
        Type tipoGrupo = grupo.GetType();
        int total = (int)tipoGrupo.GetMethod("GetTotalCount", Flags).Invoke(grupo, null);
        int indice = -1;
        for (int i = 0; i < total; i++)
        {
            object tamanho = tipoGrupo.GetMethod("GetGameViewSize", Flags).Invoke(grupo, new object[] { i });
            Type tipo = tamanho.GetType();
            if ((int)tipo.GetProperty("width", Flags).GetValue(tamanho) == largura
                && (int)tipo.GetProperty("height", Flags).GetValue(tamanho) == altura)
            { indice = i; break; }
        }
        if (indice < 0)
        {
            Assembly assembly = typeof(EditorWindow).Assembly;
            Type tipo = assembly.GetType("UnityEditor.GameViewSize");
            Type tipoModo = assembly.GetType("UnityEditor.GameViewSizeType");
            object tamanho = Activator.CreateInstance(tipo, Flags, null, new object[]
                { Enum.Parse(tipoModo, "FixedResolution"), largura, altura, PrefixoTamanho + largura + "x" + altura }, null);
            tipoGrupo.GetMethod("AddCustomSize", Flags).Invoke(grupo, new[] { tamanho });
            indice = total;
        }
        selecao.SetValue(view, indice);
        view.Focus();
        view.Repaint();
        EditorApplication.QueuePlayerLoopUpdate();
    });

    private static object GrupoTamanhos()
    {
        Type tipo = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
        object tamanhos = tipo.BaseType.GetProperty("instance", Flags).GetValue(null);
        object grupo = tipo.GetProperty("currentGroupType", Flags).GetValue(tamanhos);
        return tipo.GetMethod("GetGroup", Flags).Invoke(tamanhos, new[] { grupo });
    }

    private static EditorWindow GameView()
    {
        Type tipo = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        return Resources.FindObjectsOfTypeAll(tipo).OfType<EditorWindow>().FirstOrDefault()
            ?? throw new InvalidOperationException("Abra a aba Game antes de revisar.");
    }

    private static void Capturar(string caminho)
    {
        Type tipo = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
        object view = GameView();
        var origem = tipo.GetProperty("targetTexture", Flags)?.GetValue(view) as RenderTexture;
        foreach (string campo in new[] { "m_TargetTexture", "m_RenderTexture" })
            origem ??= tipo.GetField(campo, Flags)?.GetValue(view) as RenderTexture;
        if (origem == null) throw new InvalidOperationException("A Game View ainda nao tem imagem renderizada.");
        RenderTexture anterior = RenderTexture.active;
        Texture2D imagem = null;
        try
        {
            RenderTexture.active = origem;
            imagem = new Texture2D(origem.width, origem.height, TextureFormat.RGB24, false);
            imagem.ReadPixels(new Rect(0, 0, origem.width, origem.height), 0, 0);
            imagem.Apply();
            // O backbuffer da Game View no D3D chega invertido na leitura direta.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11
                || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
            {
                Color32[] pixels = imagem.GetPixels32();
                int largura = imagem.width;
                for (int y = 0; y < imagem.height / 2; y++)
                    for (int x = 0; x < largura; x++)
                    {
                        int a = y * largura + x, b = (imagem.height - y - 1) * largura + x;
                        (pixels[a], pixels[b]) = (pixels[b], pixels[a]);
                    }
                imagem.SetPixels32(pixels);
                imagem.Apply();
            }
            File.WriteAllBytes(caminho, imagem.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = anterior;
            if (imagem != null) UnityEngine.Object.DestroyImmediate(imagem);
        }
    }

    private static MenuController Controlador() => UnityEngine.Object.FindObjectsByType<MenuController>(FindObjectsSortMode.None)
        .FirstOrDefault(c => c.isActiveAndEnabled) ?? throw new InvalidOperationException("MenuController ativo nao encontrado.");

    private static Button Botao(string nome, bool painel)
    {
        var botao = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b =>
            (b.name == nome || (painel && nome == "Voltar" && b.name == "Back")) && b.IsInteractable() && (!painel || b.GetComponentInParent<PainelProgressoAprendizadoUI>() != null));
        return botao ?? throw new InvalidOperationException("Botao ativo nao encontrado: " + nome);
    }

    private static Rect RetanguloNaTela(RectTransform rect)
    {
        Vector3[] cantos = new Vector3[4];
        rect.GetWorldCorners(cantos);
        Canvas canvas = rect.GetComponentInParent<Canvas>()?.rootCanvas;
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 minimo = new Vector2(float.PositiveInfinity, float.PositiveInfinity), maximo = -minimo;
        foreach (Vector3 canto in cantos)
        {
            Vector2 ponto = RectTransformUtility.WorldToScreenPoint(camera, canto);
            minimo = Vector2.Min(minimo, ponto); maximo = Vector2.Max(maximo, ponto);
        }
        return Rect.MinMaxRect(minimo.x, minimo.y, maximo.x, maximo.y);
    }

    private static string Caminho(Transform objeto) => objeto.parent == null ? objeto.name : Caminho(objeto.parent) + "/" + objeto.name;

    private static void Executar(Action acao)
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[Revisao visual] Entre em Play primeiro."); return; }
        try { acao(); }
        catch (Exception e) { Debug.LogError("[Revisao visual] " + (e.InnerException ?? e).Message); }
    }

    [Serializable] private sealed class Relatorio
    {
        public string cena, cursorTravado, captura;
        public int largura, altura;
        public float timeScale;
        public bool cursorVisivel;
        public List<Papel> papeis = new();
        public List<Elemento> elementos = new();
    }
    [Serializable] private sealed class Papel { public string caminho; public Rect area; }
    [Serializable] private sealed class Elemento
    {
        public string caminho, texto, fonte;
        public Rect area;
        public Vector2 posicao, tamanho, ancoraMin, ancoraMax, pivo, centroVisualNoMenu;
        public Vector3 escala, rotacao;
        public float tamanhoFonte;
        public bool foraDoRetanguloDoPapel, autoTamanho, interativo;
    }
}
