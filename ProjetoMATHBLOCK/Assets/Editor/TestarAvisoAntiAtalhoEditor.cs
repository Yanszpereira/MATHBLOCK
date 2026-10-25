using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Verifica os filtros e a exibicao unica sem alterar a cena salva ou a posicao final do jogador.</summary>
public static class TestarAvisoAntiAtalhoEditor
{
    private static double inicio;
    private static string resultado;

    [MenuItem("MATHBLOCK/Revisao visual/Testar aviso antiatalho")]
    private static void Testar()
    {
        if (!EditorApplication.isPlaying || Time.timeScale <= 0f || AvisoAntiAtalho.JaExibido)
            throw new InvalidOperationException("Use em Play, sem pausa, antes de disparar o aviso.");
        var sensores = UnityEngine.Object.FindObjectsByType<AvisoAntiAtalho>(FindObjectsSortMode.None);
        var jogador = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        if (sensores.Length != 2 || jogador == null) throw new InvalidOperationException("Sensores ou jogador ausentes.");
        var controle = jogador.GetComponent<CharacterController>();
        var sensor = sensores[0];
        bool foraNaoDispara = !sensor.TentarMostrar(controle);
        var blocoTeste = new GameObject("Bloco temporario do teste", typeof(BoxCollider));
        blocoTeste.transform.position = sensor.GetComponent<BoxCollider>().bounds.center;
        bool blocoNaoDispara = !sensor.TentarMostrar(blocoTeste.GetComponent<BoxCollider>());
        UnityEngine.Object.Destroy(blocoTeste);
        Vector3 posicao = jogador.transform.position;
        bool ativo = controle.enabled;
        bool primeiro, repetido;
        try
        {
            controle.enabled = false;
            jogador.transform.position = sensor.GetComponent<BoxCollider>().bounds.center;
            controle.enabled = true;
            Physics.SyncTransforms();
            primeiro = sensor.TentarMostrar(controle);
            repetido = sensor.TentarMostrar(controle);
        }
        finally
        {
            controle.enabled = false;
            jogador.transform.position = posicao;
            controle.enabled = ativo;
            Physics.SyncTransforms();
        }
        resultado = $"Sensores: {sensores.Length}\nJogador fora nao dispara: {foraNaoDispara}\nBloco nao dispara: {blocoNaoDispara}\nPrimeira tentativa exibida: {primeiro}\nRepeticao bloqueada: {!repetido}";
        inicio = EditorApplication.timeSinceStartup;
        EditorApplication.update -= VerificarFim;
        EditorApplication.update += VerificarFim;
    }

    private static void VerificarFim()
    {
        if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup - inicio < 6.5d) return;
        EditorApplication.update -= VerificarFim;
        bool sumiu = GameObject.Find("Aviso de tentativa de atalho") == null;
        bool exibido = AvisoAntiAtalho.JaExibido;
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/teste-aviso-antiatalho.txt")),
            resultado + $"\nSumiu sozinho: {sumiu}\nMemoria da exibicao mantida: {exibido}");
    }
}
