using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Auditoria e simulacao do perfil mobile. O Editor nao representa o desempenho de um telefone real.</summary>
public static class OtimizarMobileEditor
{
    private static string Pasta => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/AuditoriaMobile"));
    [InitializeOnLoadMethod]
    private static void RegistrarRestauracao()
    {
        EditorApplication.playModeStateChanged -= AoMudarModo;
        EditorApplication.playModeStateChanged += AoMudarModo;
    }
    private static void AoMudarModo(PlayModeStateChange estado)
    {
        if (estado == PlayModeStateChange.ExitingPlayMode) AlternarSimulacao(false);
    }
    [Serializable] private class Relatorio
    {
        public string cena, qualidade;
        public int largura, altura, fpsAlvo, renderizadores, slotsMateriais, materiaisUnicos, drawCalls, setPassCalls;
        public long bytesMateriais;
    }
    [MenuItem("MATHBLOCK/Mobile/Auditoria antes")]
    private static void Antes() => Auditar("antes");
    [MenuItem("MATHBLOCK/Mobile/Auditoria depois")]
    private static void Depois() => Auditar("depois");
    private static void Auditar(string nome)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Entre em Play para medir os materiais de runtime.");
        var relatorio = new Relatorio
        {
            cena = SceneManager.GetActiveScene().name, qualidade = QualitySettings.names[QualitySettings.GetQualityLevel()],
            largura = Screen.width, altura = Screen.height, fpsAlvo = Application.targetFrameRate,
            drawCalls = Estatistica("drawCalls"), setPassCalls = Estatistica("setPassCalls")
        };
        var materiais = new HashSet<Material>();
        foreach (Renderer renderizador in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!renderizador.enabled || !renderizador.gameObject.activeInHierarchy) continue;
            relatorio.renderizadores++;
            foreach (Material material in renderizador.sharedMaterials)
            { relatorio.slotsMateriais++; if (material != null) materiais.Add(material); }
        }
        relatorio.materiaisUnicos = materiais.Count;
        foreach (Material material in materiais) relatorio.bytesMateriais += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(material);
        Directory.CreateDirectory(Pasta);
        File.WriteAllText(Path.Combine(Pasta, nome + ".json"), JsonUtility.ToJson(relatorio, true));
        ScreenCapture.CaptureScreenshot(Path.Combine(Pasta, nome + ".png"));
    }
    private static int Estatistica(string nome)
    {
        PropertyInfo propriedade = typeof(UnityStats).GetProperty(nome, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        return propriedade != null ? Convert.ToInt32(propriedade.GetValue(null)) : -1;
    }

    [MenuItem("MATHBLOCK/Mobile/Simular Android sem limite fixo")]
    private static void Simular() => AlternarSimulacao(true);
    [MenuItem("MATHBLOCK/Mobile/Restaurar PC")]
    private static void Restaurar() => AlternarSimulacao(false);
    [MenuItem("MATHBLOCK/Mobile/Testar Fase 2")]
    private static void FaseDois() => CarregarFase("Fase 2");
    [MenuItem("MATHBLOCK/Mobile/Testar Fase 3")]
    private static void FaseTres() => CarregarFase("Fase 3");
    [MenuItem("MATHBLOCK/Mobile/Testar Fase 4")]
    private static void FaseQuatro() => CarregarFase("Fase 4");
    private static void CarregarFase(string nome)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Use somente em Play.");
        SceneManager.LoadScene(nome);
    }
    private static void AlternarSimulacao(bool ativar)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Use somente em Play.");
        Type perfil = Type.GetType("PerfilDesempenhoMobile, Assembly-CSharp");
        if (perfil == null) throw new InvalidOperationException("Perfil mobile ainda nao compilado.");
        perfil.GetMethod("SimularNoEditor", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { ativar });
    }
}
