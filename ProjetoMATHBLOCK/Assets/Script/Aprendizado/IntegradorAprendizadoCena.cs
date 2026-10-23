using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public sealed class IntegradorAprendizadoCena : MonoBehaviour
{
    private static ConfiguracaoAprendizado configuracao;
    private readonly Dictionary<DoorValueVerifier, ConfiguracaoAprendizado.Subfase> portas = new();
    private readonly Dictionary<ElevatorTotemController, ConfiguracaoAprendizado.Subfase> elevadores = new();
    private readonly List<(BalanceScaleController balanca, UnityAction acao)> balancas = new();

    public static ConfiguracaoAprendizado Configuracao => configuracao ??=
        Resources.Load<ConfiguracaoAprendizado>("Aprendizado/ConfiguracaoAprendizado")
        ?? ConfiguracaoAprendizado.CriarPadraoEmMemoria();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Registrar() => GlobalSceneBootstrap.Register(Instalar);

    private static void Instalar(Scene scene)
    {
        if (scene.name == "Fase 4" || !scene.name.StartsWith("Fase")) return;
        new GameObject("Integrador de Aprendizado").AddComponent<IntegradorAprendizadoCena>();
        FeedbackAprendizadoUI.GarantirInstancia();
    }

    private void Start()
    {
        string cena = SceneManager.GetActiveScene().name;
        ProgressoAprendizado.Instance.IniciarCena(cena);
        var itens = Configuracao.ObterDaCena(cena).ToList();
        var listaPortas = FindObjectsByType<DoorValueVerifier>(FindObjectsSortMode.None).OrderBy(x => Caminho(x.transform)).ToArray();
        var listaElevadores = FindObjectsByType<ElevatorTotemController>(FindObjectsSortMode.None).OrderBy(x => Caminho(x.transform)).ToArray();
        var listaBalancas = FindObjectsByType<BalanceScaleController>(FindObjectsSortMode.None)
            .Where(x => !x.IsSmallBalance).OrderBy(x => Caminho(x.transform)).ToArray();

        foreach (var item in itens)
        {
            if (item.gatilho == ConfiguracaoAprendizado.TipoGatilho.Porta && item.indiceNaCena < listaPortas.Length)
                portas[listaPortas[item.indiceNaCena]] = item;
            else if (item.gatilho == ConfiguracaoAprendizado.TipoGatilho.Elevador && item.indiceNaCena < listaElevadores.Length)
                elevadores[listaElevadores[item.indiceNaCena]] = item;
            else if (item.gatilho == ConfiguracaoAprendizado.TipoGatilho.Balanca && item.indiceNaCena < listaBalancas.Length)
            {
                BalanceScaleController b = listaBalancas[item.indiceNaCena];
                UnityAction acao = () => ProgressoAprendizado.Instance.ConcluirSubfase(item);
                b.OnBalanced.AddListener(acao);
                balancas.Add((b, acao));
            }
        }
        DoorValueVerifier.RespostaAvaliada += AoAvaliarPorta;
        ElevatorTotemController.RespostaAvaliada += AoAvaliarElevador;
    }

    private void AoAvaliarPorta(DoorValueVerifier origem, bool correta) => Avaliar(portas, origem, correta);
    private void AoAvaliarElevador(ElevatorTotemController origem, bool correta) => Avaliar(elevadores, origem, correta);

    private static void Avaliar<T>(Dictionary<T, ConfiguracaoAprendizado.Subfase> mapa, T origem, bool correta)
    {
        if (!mapa.TryGetValue(origem, out var subfase)) return;
        if (correta) ProgressoAprendizado.Instance.ConcluirSubfase(subfase);
        else ProgressoAprendizado.Instance.RegistrarTentativa(subfase, false);
    }

    private void OnDestroy()
    {
        DoorValueVerifier.RespostaAvaliada -= AoAvaliarPorta;
        ElevatorTotemController.RespostaAvaliada -= AoAvaliarElevador;
        foreach (var item in balancas)
            if (item.balanca != null) item.balanca.OnBalanced.RemoveListener(item.acao);
    }

    private static string Caminho(Transform t)
    {
        string caminho = t.name;
        while (t.parent != null) { t = t.parent; caminho = t.name + "/" + caminho; }
        return caminho;
    }
}
