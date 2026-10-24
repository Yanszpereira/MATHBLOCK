using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Verificacao em Play; nao grava progresso nem altera cenas salvas.</summary>
public static class RevisaoCeus
{
    private const string Pasta = "C:/Users/arbustim/Documents/Codex/2026-08-14/d/ceus";
    private static int fase;
    private static int etapa;
    private static double limite;
    private static readonly List<string> amostras = new();

    [MenuItem("MATHBLOCK/Revisao visual/Testar ceus")]
    private static void Iniciar()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Entre em Play primeiro.");
        Directory.CreateDirectory(Pasta);
        amostras.Clear(); fase = 1;
        EditorApplication.update -= Atualizar;
        EditorApplication.update += Atualizar;
        Carregar();
    }

    private static void Carregar()
    {
        etapa = 0; Time.timeScale = 1f;
        SceneManager.LoadScene("Fase " + fase);
        limite = EditorApplication.timeSinceStartup + 45d;
    }

    private static void Atualizar()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > limite)
        {
            EditorApplication.update -= Atualizar;
            File.WriteAllLines(Path.Combine(Pasta, "relatorio.txt"), amostras);
            return;
        }
        if (SceneManager.GetActiveScene().name != "Fase " + fase) return;
        float tempo = Time.timeSinceLevelLoad;
        if (etapa == 0 && tempo > .2f)
        {
            // A mesma inclinacao deixa o ceu e as nuvens visiveis nas quatro capturas.
            Camera camera = Camera.main;
            foreach (Look olhar in UnityEngine.Object.FindObjectsByType<Look>(FindObjectsSortMode.None)) olhar.enabled = false;
            if (camera != null) camera.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            Capturar("inicio"); etapa++;
        }
        else if (etapa == 1 && tempo >= 3.8f) { Amostrar("antes_do_fade"); etapa++; }
        else if (etapa == 2 && tempo >= 6f) { Amostrar("meio_do_fade"); etapa++; }
        else if (etapa == 3 && tempo >= 8.3f) { Capturar("final"); etapa++; }
        else if (etapa == 4 && tempo >= 9f)
        {
            File.WriteAllLines(Path.Combine(Pasta, "relatorio.txt"), amostras);
            if (++fase <= 4) Carregar();
            else
            {
                EditorApplication.update -= Atualizar;
                Debug.Log("Revisao dos ceus concluida: " + Pasta);
            }
        }
    }

    private static void Capturar(string nome)
    {
        Amostrar(nome);
        ScreenCapture.CaptureScreenshot(Path.Combine(Pasta, "fase" + fase + "-" + nome + ".png"));
    }

    private static void Amostrar(string nome)
    {
        var transicao = UnityEngine.Object.FindFirstObjectByType<PhaseSkyboxPalette>();
        amostras.Add($"Fase {fase} | {nome} | tempo={Time.timeSinceLevelLoad:F2} | fade={(transicao != null ? transicao.FracaoTransicao : 0f):F3}");
        foreach (Renderer alvo in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        foreach (Material material in alvo.sharedMaterials)
            if (material != null && material.HasProperty("_BottomColor") && material.HasProperty("_TopColor"))
                amostras.Add($"  {alvo.name}: horizonte={material.GetColor("_BottomColor")} topo={material.GetColor("_TopColor")}");
    }
}
