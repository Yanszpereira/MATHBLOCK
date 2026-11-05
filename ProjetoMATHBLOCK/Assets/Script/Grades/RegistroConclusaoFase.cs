using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Registra uma conclusão somente no gatilho canônico da cena carregada.</summary>
public static class RegistroConclusaoFase
{
    private static int sceneHandleRegistrado = -1;
    private static int faseRegistrada = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetarControleDaCena()
    {
        sceneHandleRegistrado = -1;
        faseRegistrada = -1;
    }

public static bool TryRegistrarConclusaoDaCenaAtual(Object logContext = null)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!TryObterNumeroDaFase(scene.name, out int fase))
            return false;

        if (sceneHandleRegistrado == scene.handle && faseRegistrada == fase)
            return true;

        CountdownTimer cronometro = CountdownTimer.ClockAuthority;
        if (cronometro == null || cronometro.gameObject.scene != scene)
            cronometro = Object.FindFirstObjectByType<CountdownTimer>();

        if (cronometro == null)
        {
            Debug.LogWarning(
                $"A {scene.name} foi concluída sem CountdownTimer; nenhum resultado foi registrado.",
                logContext);
            return false;
        }

        bool registrado = ResultadosDasFases.RegistrarResultado(
            fase,
            cronometro.GetPercentualTempoRestante());
        if (!registrado)
            return false;

        sceneHandleRegistrado = scene.handle;
        faseRegistrada = fase;
        return true;
    }

    private static bool TryObterNumeroDaFase(string sceneName, out int fase)
    {
        fase = 0;
        if (string.IsNullOrWhiteSpace(sceneName) ||
            !sceneName.StartsWith("Fase ", System.StringComparison.Ordinal))
            return false;

        return int.TryParse(sceneName.Substring("Fase ".Length), out fase) &&
               fase >= 1 &&
               fase <= ResultadosDasFases.QuantidadeDeFases;
    }
}
