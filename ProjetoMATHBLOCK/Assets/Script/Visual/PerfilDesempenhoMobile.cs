using UnityEngine;

/// <summary>Perfil de Android de entrada. Nao altera fisica, puzzles, controles ou a qualidade do PC.</summary>
public static class PerfilDesempenhoMobile
{
    public const string NomeQualidade = "Android Leve";
    // No Android, -1 significa o padrao de 30 FPS, nao FPS ilimitado.
    // Usa a frequencia atual da tela. Arredonda para cima para nao dividir
    // acidentalmente pela metade uma tela que informa 59.94 Hz.
    public static int FpsAlvo
    {
        get
        {
            double frequencia = Screen.currentResolution.refreshRateRatio.value;
            return frequencia > 0d ? Mathf.CeilToInt((float)frequencia) : 60;
        }
    }
    private static bool simulacao;
    private static int qualidadeAnterior, fpsAnterior, sincronizacaoAnterior;
    public static bool Ativo => Application.platform == RuntimePlatform.Android || simulacao;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar() { simulacao = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Registrar()
    {
        if (Ativo) Aplicar();
    }

    private static void Aplicar()
    {
        int indice = System.Array.IndexOf(QualitySettings.names, NomeQualidade);
        if (indice >= 0) QualitySettings.SetQualityLevel(indice, true);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = FpsAlvo;
        if (Application.platform != RuntimePlatform.Android) return;
        // Nao reduz resolucao, fontes, texturas nem geometria dos blocos.
        if (!Debug.isDebugBuild)
        {
            Debug.unityLogger.filterLogType = LogType.Warning;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        }
    }

#if UNITY_EDITOR
    public static void SimularNoEditor(bool ativar)
    {
        if (ativar == simulacao) return;
        if (ativar)
        {
            qualidadeAnterior = QualitySettings.GetQualityLevel(); fpsAnterior = Application.targetFrameRate;
            sincronizacaoAnterior = QualitySettings.vSyncCount;
            simulacao = true; Aplicar();
        }
        else
        {
            simulacao = false;
            QualitySettings.SetQualityLevel(qualidadeAnterior, true);
            QualitySettings.vSyncCount = sincronizacaoAnterior; Application.targetFrameRate = fpsAnterior;
        }
    }
#endif
}
