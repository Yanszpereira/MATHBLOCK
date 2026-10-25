using System;
using System.IO;
using System.Reflection;
using FMOD.Studio;
using FMODUnity;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Teste manual do timeout real, sem modificar o tempo salvo na cena.</summary>
public static class TestarAudioResetEditor
{
    private static EventInstance[] instanciasDaOrigem;
    private static string destinoTransicao;
    private static double inicioTransicao;
    [MenuItem("MATHBLOCK/Audio/Testar Fase 2 para Fase 3")]
    private static void TestarFaseTres() => TestarSaidaFaseDois("Fase 3");
    [MenuItem("MATHBLOCK/Audio/Testar Fase 2 para Menu")]
    private static void TestarMenu() => TestarSaidaFaseDois("MainMenu");

    private static void TestarSaidaFaseDois(string destino)
    {
        if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "Fase 2" || destinoTransicao != null)
            throw new InvalidOperationException("Use em Play na Fase 2, sem outro teste ativo.");
        var instancias = new System.Collections.Generic.List<EventInstance>();
        RuntimeManager.StudioSystem.getBankList(out Bank[] bancos);
        foreach (Bank banco in bancos)
        {
            banco.getEventList(out EventDescription[] eventos);
            foreach (EventDescription evento in eventos)
            {
                evento.getInstanceList(out EventInstance[] lista);
                foreach (EventInstance instancia in lista)
                    if (instancia.getPlaybackState(out PLAYBACK_STATE estado) == FMOD.RESULT.OK && estado != PLAYBACK_STATE.STOPPED)
                        instancias.Add(instancia);
            }
        }
        instanciasDaOrigem = instancias.ToArray();
        if (!SceneTransitionManager.TryTransitionToScene(destino)) throw new InvalidOperationException("Transicao recusada.");
        destinoTransicao = destino;
        inicioTransicao = EditorApplication.timeSinceStartup;
        EditorApplication.update += VerificarTransicao;
    }

    private static void VerificarTransicao()
    {
        if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup - inicioTransicao < 6d) return;
        EditorApplication.update -= VerificarTransicao;
        int restantes = 0;
        foreach (EventInstance instancia in instanciasDaOrigem)
            if (instancia.isValid() && instancia.getPlaybackState(out PLAYBACK_STATE estado) == FMOD.RESULT.OK && estado != PLAYBACK_STATE.STOPPED)
                restantes++;
        bool passou = EditorApplication.isPlaying && SceneManager.GetActiveScene().name == destinoTransicao && instanciasDaOrigem.Length > 0 && restantes == 0;
        string pasta = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TesteAudioReset"));
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, "fase2-" + destinoTransicao + ".txt"),
            $"Origem: Fase 2\nDestino: {SceneManager.GetActiveScene().name}\nInstancias antigas antes: {instanciasDaOrigem.Length}\nAinda tocando: {restantes}\nPassou: {passou}");
        destinoTransicao = null;
    }

    private static EventDescription musicaAntiga;
    private static double inicio;
    private static Relatorio relatorio;
    [Serializable] private class Relatorio
    {
        public string origem, destino, erro;
        public int musicaAntigaAntes, musicaAntigaDepois, musicaNovaDepois;
        public bool passou;
    }

    [MenuItem("MATHBLOCK/Audio/Testar reset por tempo")]
    private static void Iniciar()
    {
        if (!EditorApplication.isPlaying || Time.timeScale <= 0f || relatorio != null)
            throw new InvalidOperationException("Use em Play, sem pausa e sem outro teste ativo.");
        StudioEventEmitter musica = null;
        foreach (StudioEventEmitter emissor in UnityEngine.Object.FindObjectsByType<StudioEventEmitter>(FindObjectsSortMode.None))
            if (emissor.gameObject.scene == SceneManager.GetActiveScene() &&
                emissor.EventReference.Path == "event:/MusicaFase3") musica = emissor;
        if (musica == null || !musica.EventDescription.isValid())
            throw new InvalidOperationException("Abra a Fase 3 e aguarde sua musica iniciar.");
        CountdownTimer relogio = (CountdownTimer)typeof(CountdownTimer)
            .GetField("clockAuthority", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        if (relogio == null) throw new InvalidOperationException("Cronometro da fase ausente.");
        musicaAntiga = musica.EventDescription;
        relatorio = new Relatorio { origem = SceneManager.GetActiveScene().name, musicaAntigaAntes = Contar(musicaAntiga) };
        relogio.SetPaused(false);
        typeof(CountdownTimer).GetField("tempoAtual", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(relogio, 0.05f);
        inicio = EditorApplication.timeSinceStartup;
        EditorApplication.update += Verificar;
    }

    private static int Contar(EventDescription descricao)
    {
        if (!descricao.isValid() || descricao.getInstanceList(out EventInstance[] instancias) != FMOD.RESULT.OK) return 0;
        int total = 0;
        foreach (EventInstance instancia in instancias)
            if (instancia.getPlaybackState(out PLAYBACK_STATE estado) == FMOD.RESULT.OK && estado != PLAYBACK_STATE.STOPPED) total++;
        return total;
    }

    private static void Verificar()
    {
        double decorrido = EditorApplication.timeSinceStartup - inicio;
        if (EditorApplication.isPlaying && SceneManager.GetActiveScene().name == "Fase 1" && decorrido >= 3d)
        {
            relatorio.destino = SceneManager.GetActiveScene().name;
            relatorio.musicaAntigaDepois = Contar(musicaAntiga);
            relatorio.musicaNovaDepois = Contar(RuntimeManager.GetEventDescription("event:/MusicaFase1Certa"));
            relatorio.passou = relatorio.musicaAntigaAntes > 0 && relatorio.musicaAntigaDepois == 0 && relatorio.musicaNovaDepois == 1;
        }
        else if (EditorApplication.isPlaying && decorrido < 25d) return;
        else relatorio.erro = "Teste interrompido ou reset nao concluiu em 25 segundos.";
        EditorApplication.update -= Verificar;
        string pasta = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/TesteAudioReset"));
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, "resultado.json"), JsonUtility.ToJson(relatorio, true));
        relatorio = null;
    }
}
