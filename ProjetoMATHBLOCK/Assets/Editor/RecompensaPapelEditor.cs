using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Autoria persistente do prefab/colliders e verificacao visual sem alterar o save.</summary>
public static class RecompensaPapelEditor
{
    private const string Pasta = "C:/Users/arbustim/Documents/Codex/2026-08-14/d/recompensa-papel";
    private const string Recursos = "Assets/Resources/Aprendizado/";
    private static readonly List<string> relatorio = new();
    private static double inicio;
    private static int captura;
    private static int ultimoQuadro = -999;

    [MenuItem("MATHBLOCK/Aprendizado/Configurar recompensa de papel")]
    private static void Configurar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Saia de Play.");
        if (!EditorSceneManager.SaveOpenScenes()) return;
        Directory.CreateDirectory(Pasta);
        CriarPrefab(); relatorio.Clear();
        SceneSetup[] anteriores = EditorSceneManager.GetSceneManagerSetup();
        string[] caminhos = {
            "03_MathBlocks_e_Puzzles/Portas/porta3/portafim",
            "03_MathBlocks_e_Puzzles/Porta/porta1",
            "Porta/portacorrigida"
        };
        try
        {
            for (int i = 0; i < caminhos.Length; i++)
            {
                Scene cena = EditorSceneManager.OpenScene($"Assets/Scenes/Fase {i + 1}.unity");
                Transform porta = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .First(t => Caminho(t) == caminhos[i]);
                DoorOpener abertura = porta.GetComponent<DoorOpener>();
                if (abertura == null || !abertura.TryGetDoorBounds(out Bounds limites))
                    throw new InvalidOperationException("Porta sem bounds validos: " + caminhos[i]);
                GatilhoConclusaoFase gatilho = cena.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<GatilhoConclusaoFase>(true)).FirstOrDefault();
                if (gatilho == null)
                {
                    var objeto = new GameObject("Conclusao da fase - Porta final");
                    SceneManager.MoveGameObjectToScene(objeto, cena);
                    gatilho = objeto.AddComponent<GatilhoConclusaoFase>();
                    var corpo = objeto.AddComponent<Rigidbody>(); corpo.isKinematic = true; corpo.useGravity = false;
                    corpo.constraints = RigidbodyConstraints.FreezeAll;
                }
                gatilho.Configurar(abertura);
                gatilho.transform.SetPositionAndRotation(limites.center, Quaternion.identity);
                gatilho.transform.localScale = Vector3.one;
                BoxCollider caixa = gatilho.GetComponent<BoxCollider>();
                caixa.isTrigger = true; caixa.center = Vector3.zero;
                // Bounds reais das folhas, com uma pequena zona de aproximacao.
                caixa.size = limites.size + new Vector3(1.2f, .8f, 3f);
                Physics.SyncTransforms();
                relatorio.Add($"{cena.name}: {caminhos[i]} | centro={gatilho.transform.position} | tamanho={caixa.size}");
                ValidarGuardas(gatilho);
                EditorUtility.SetDirty(gatilho); EditorUtility.SetDirty(caixa);
                EditorSceneManager.MarkSceneDirty(cena); EditorSceneManager.SaveScene(cena);
            }
        }
        finally
        {
            File.WriteAllLines(Path.Combine(Pasta, "gatilhos.txt"), relatorio);
            EditorSceneManager.RestoreSceneManagerSetup(anteriores);
        }
        Debug.Log("Recompensa de papel e gatilhos salvos nas Fases 1 a 3.");
    }

    [MenuItem("MATHBLOCK/Aprendizado/Atualizar papel 2D 15 FPS")]
    private static void AtualizarPapel2D()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Saia de Play.");
        CriarPrefab();
    }

    private static void CriarPrefab()
    {
        string caminho = Recursos + "FolhaRecompensa.asset";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        if (sprite == null)
        {
            Texture2D textura = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Images/MenuImages/paper.png");
            // Recorta apenas a margem transparente da arte original, sem redesenha-la.
            sprite = Sprite.Create(textura, new Rect(80, 58, 350, 420), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "Folha do artista - Recompensa";
            AssetDatabase.CreateAsset(sprite, caminho);
        }
        PrepararSprites2D(out Sprite[] quadros, out Vector2[] centros, out Vector2 tamanhoFinal);
        var raiz = new GameObject("Folha de recompensa", typeof(RectTransform), typeof(CanvasRenderer), typeof(PapelDesamassandoUI), typeof(CanvasGroup));
        try
        {
            RectTransform rect = raiz.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(600, 720);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            raiz.GetComponent<PapelDesamassandoUI>().DefinirArte(sprite);
            raiz.GetComponent<PapelDesamassandoUI>().DefinirQuadros(quadros, centros, tamanhoFinal);
            raiz.GetComponent<PapelDesamassandoUI>().raycastTarget = false;
            CanvasGroup grupo = raiz.GetComponent<CanvasGroup>(); grupo.blocksRaycasts = grupo.interactable = false;
            RectTransform conteudo = UIAprendizadoUtil.CriarRetangulo(rect, "Conteudo", new Vector2(450, 600));
            conteudo.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            PrefabUtility.SaveAsPrefabAsset(raiz, Recursos + "RecompensaPapel.prefab");
            AssetDatabase.SaveAssets();
        }
        finally { UnityEngine.Object.DestroyImmediate(raiz); }
    }

    private static void PrepararSprites2D(out Sprite[] quadros, out Vector2[] centros, out Vector2 tamanhoFinal)
    {
        const string caminhoTextura = "Assets/Images/MenuImages/PapelDesamassando2D-Limpo.png";
        const string caminhoQuadros = Recursos + "QuadrosPapel2DLimpo.asset";
        AssetDatabase.ImportAsset(caminhoTextura, ImportAssetOptions.ForceSynchronousImport);
        var importador = (TextureImporter)AssetImporter.GetAtPath(caminhoTextura);
        importador.textureType = TextureImporterType.Default;
        importador.npotScale = TextureImporterNPOTScale.None;
        importador.isReadable = true; importador.mipmapEnabled = false;
        importador.alphaIsTransparency = true; importador.filterMode = FilterMode.Bilinear;
        importador.wrapMode = TextureWrapMode.Clamp;
        importador.textureCompression = TextureImporterCompression.Uncompressed;
        importador.SaveAndReimport();
        Texture2D textura = AssetDatabase.LoadAssetAtPath<Texture2D>(caminhoTextura);
        Color32[] pixels = textura.GetPixels32();
        quadros = new Sprite[15]; centros = new Vector2[15]; tamanhoFinal = Vector2.one;
        bool criar = AssetDatabase.LoadMainAssetAtPath(caminhoQuadros) == null;
        Sprite[] existentes = criar ? new Sprite[0] : AssetDatabase.LoadAllAssetsAtPath(caminhoQuadros)
            .OfType<Sprite>().OrderBy(s => s.name).ToArray();
        for (int i = 0; i < 15; i++)
        {
            int coluna = i % 5, linha = i / 5;
            int x0 = Mathf.RoundToInt(coluna * textura.width / 5f), x1 = Mathf.RoundToInt((coluna + 1) * textura.width / 5f);
            int y0 = Mathf.RoundToInt((2 - linha) * textura.height / 3f), y1 = Mathf.RoundToInt((3 - linha) * textura.height / 3f);
            Rect desenho = LocalizarDesenho(pixels, textura.width, x0, y0, x1 - x0, y1 - y0);
            centros[i] = desenho.center;
            if (i == 14) tamanhoFinal = desenho.size;
            if (criar)
            {
                Sprite quadro = Sprite.Create(textura, new Rect(x0, y0, x1 - x0, y1 - y0), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                quadro.name = "Papel2D_" + i.ToString("D2");
                if (i == 0) AssetDatabase.CreateAsset(quadro, caminhoQuadros);
                else AssetDatabase.AddObjectToAsset(quadro, caminhoQuadros);
                quadros[i] = quadro;
            }
            else quadros[i] = existentes[i];
        }
        AssetDatabase.SaveAssets();
        // Leitura so durante a autoria; nao mantem copia de pixels na RAM do jogo.
        importador.isReadable = false; importador.SaveAndReimport();
    }

    private static Rect LocalizarDesenho(Color32[] pixels, int larguraTextura, int x0, int y0, int largura, int altura)
    {
        int[] colunas = new int[largura], linhas = new int[altura];
        for (int y = 0; y < altura; y++)
        for (int x = 0; x < largura; x++)
            if (pixels[(y0 + y) * larguraTextura + x0 + x].a > 128) { colunas[x]++; linhas[y]++; }
        // Ignora pequenos residuos de alpha, sem editar os pixels gerados.
        int esquerda = Array.FindIndex(colunas, n => n >= 8), direita = Array.FindLastIndex(colunas, n => n >= 8);
        int baixo = Array.FindIndex(linhas, n => n >= 8), cima = Array.FindLastIndex(linhas, n => n >= 8);
        if (esquerda < 0 || baixo < 0) throw new InvalidOperationException("Quadro 2D vazio.");
        return new Rect((float)esquerda / largura, (float)baixo / altura,
            (float)(direita - esquerda + 1) / largura, (float)(cima - baixo + 1) / altura);
    }

    private static void ValidarGuardas(GatilhoConclusaoFase gatilho)
    {
        FieldInfo campo = typeof(DoorOpener).GetField("hasOpened", BindingFlags.Instance | BindingFlags.NonPublic);
        bool anterior = gatilho.PortaFinal.HasOpened;
        var simulado = new GameObject("Jogador simulado - somente teste"); simulado.tag = "Player";
        BoxCollider jogador = simulado.AddComponent<BoxCollider>(); jogador.size = new Vector3(.5f, 1.7f, .5f);
        try
        {
            simulado.transform.position = gatilho.transform.position; Physics.SyncTransforms();
            campo.SetValue(gatilho.PortaFinal, false);
            bool fechada = gatilho.PodeConcluir(jogador);
            campo.SetValue(gatilho.PortaFinal, true);
            bool abertaPerto = gatilho.PodeConcluir(jogador);
            simulado.transform.position += Vector3.one * 100f; Physics.SyncTransforms();
            bool abertaLonge = gatilho.PodeConcluir(jogador);
            relatorio.Add($"  fechada/perto={fechada}; aberta/perto={abertaPerto}; aberta/longe={abertaLonge}");
            if (fechada || !abertaPerto || abertaLonge) throw new InvalidOperationException("Validacao de aproximacao falhou.");
        }
        finally { campo.SetValue(gatilho.PortaFinal, anterior); UnityEngine.Object.DestroyImmediate(simulado); }
    }

    private static string Caminho(Transform alvo) => alvo.parent != null ? Caminho(alvo.parent) + "/" + alvo.name : alvo.name;

    private static GatilhoConclusaoFase gatilhoEmTeste;
    private static int conclusoes;
    private static bool portaLiberada, resultadoVisto;

    [MenuItem("MATHBLOCK/Aprendizado/Testar fluxo da porta final")]
    private static void TestarFluxo()
    {
        if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "Fase 1")
            throw new InvalidOperationException("Use Play na Fase 1. O teste nao grava cenas nem progresso.");
        gatilhoEmTeste = UnityEngine.Object.FindFirstObjectByType<GatilhoConclusaoFase>();
        PlayerMovement jogador = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        if (gatilhoEmTeste == null || jogador == null || gatilhoEmTeste.PortaFinal.HasOpened)
            throw new InvalidOperationException("Inicie com a porta final fechada.");
        CharacterController controle = jogador.GetComponent<CharacterController>();
        bool habilitado = controle != null && controle.enabled;
        if (controle != null) controle.enabled = false;
        Vector3 alvo = gatilhoEmTeste.transform.position;
        alvo.y = gatilhoEmTeste.GetComponent<BoxCollider>().bounds.min.y + 1.2f;
        alvo.z -= gatilhoEmTeste.GetComponent<BoxCollider>().size.z * .35f;
        jogador.transform.position = alvo;
        if (controle != null) controle.enabled = habilitado;
        Physics.SyncTransforms();
        conclusoes = 0; portaLiberada = resultadoVisto = false; relatorio.Clear();
        inicio = EditorApplication.timeSinceStartup;
        ProgressoAprendizado.OnFaseConcluida -= ContarConclusao;
        ProgressoAprendizado.OnFaseConcluida += ContarConclusao;
        EditorApplication.update -= VerificarFluxo; EditorApplication.update += VerificarFluxo;
    }

    private static void ContarConclusao(string cena) { conclusoes++; }

    private static void VerificarFluxo()
    {
        double tempo = EditorApplication.timeSinceStartup - inicio;
        if (!EditorApplication.isPlaying || tempo > 35d) { EncerrarFluxo("Interrompido/timeout"); return; }
        if (!portaLiberada && tempo > 2d)
        {
            bool antecipado = UnityEngine.Object.FindFirstObjectByType<ResultadosAprendizadoUI>() != null;
            relatorio.Add($"Porta fechada e jogador perto: resultado={antecipado}, conclusoes={conclusoes}");
            // Simula somente a liberacao: exercita OpenOnce e o collider real,
            // sem enviar acerto ao sistema de score ou aprendizagem.
            gatilhoEmTeste.PortaFinal.OpenOnce(); portaLiberada = true;
        }
        if (portaLiberada && !resultadoVisto && UnityEngine.Object.FindFirstObjectByType<ResultadosAprendizadoUI>() != null)
        {
            resultadoVisto = true;
            relatorio.Add($"Recompensa disparada pelo collider: consumido={gatilhoEmTeste.Consumido}; conclusoes={conclusoes}");
        }
        if (SceneManager.GetActiveScene().name == "Fase 2")
            EncerrarFluxo($"Chegou a Fase 2: resultadoVisto={resultadoVisto}; conclusoes={conclusoes}; tempoDoJogo={Time.timeScale}");
    }

    private static void EncerrarFluxo(string mensagem)
    {
        relatorio.Add(mensagem); Directory.CreateDirectory(Pasta);
        File.WriteAllLines(Path.Combine(Pasta, "fluxo-porta.txt"), relatorio);
        ProgressoAprendizado.OnFaseConcluida -= ContarConclusao;
        EditorApplication.update -= VerificarFluxo;
    }

    [MenuItem("MATHBLOCK/Aprendizado/Testar recompensa de papel")]
    private static void Testar()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Entre em Play.");
        Directory.CreateDirectory(Pasta); relatorio.Clear(); captura = 0; ultimoQuadro = -999;
        ResultadosAprendizadoUI.MostrarDev(SceneManager.GetActiveScene().name);
        var ui = UnityEngine.Object.FindFirstObjectByType<ResultadosAprendizadoUI>();
        var lista = (IEnumerable)typeof(ResultadosAprendizadoUI).GetField("linhas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
        int i = 0;
        // Somente apresentacao: nao credita acertos nem modifica dados salvos.
        foreach (object linha in lista) linha.GetType().GetField("destino").SetValue(linha, .35f + .28f * i++);
        inicio = EditorApplication.timeSinceStartup;
        EditorApplication.update -= CapturarTeste; EditorApplication.update += CapturarTeste;
    }

    private static void CapturarTeste()
    {
        double tempo = EditorApplication.timeSinceStartup - inicio;
        var quadroVisivel = UnityEngine.Object.FindFirstObjectByType<PapelDesamassandoUI>();
        if (quadroVisivel != null && quadroVisivel.QuadroAtual != ultimoQuadro)
        {
            ultimoQuadro = quadroVisivel.QuadroAtual;
            relatorio.Add($"quadro2D={ultimoQuadro}; t={tempo:F3}");
        }
        double[] momentos = { .5, 1.2, 2.1, 4.3, 5.3, 6.5 };
        if (!EditorApplication.isPlaying || tempo > 15) { EditorApplication.update -= CapturarTeste; return; }
        if (captura >= momentos.Length || tempo < momentos[captura]) return;
        var ui = UnityEngine.Object.FindFirstObjectByType<ResultadosAprendizadoUI>();
        int ativas = UnityEngine.Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None).Count(p => p.inputIsActive);
        var papel = UnityEngine.Object.FindFirstObjectByType<PapelDesamassandoUI>();
        relatorio.Add($"t={tempo:F2}; etapa={ui?.EtapaAtual ?? "Encerrada"}; quadro={papel?.QuadroAtual}; fps={PapelDesamassandoUI.QuadrosPorSegundo}; tempoDoJogo={Time.timeScale}; entradasAtivas={ativas}");
        ScreenCapture.CaptureScreenshot(Path.Combine(Pasta, "papel-" + captura + ".png"));
        captura++;
        if (captura == momentos.Length)
        {
            File.WriteAllLines(Path.Combine(Pasta, "animacao.txt"), relatorio);
            EditorApplication.update -= CapturarTeste;
        }
    }
}
