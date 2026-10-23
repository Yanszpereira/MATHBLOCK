using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ProgressoAprendizado : MonoBehaviour
{
    [Serializable]
    public sealed class Registro
    {
        public int tentativas;
        public int acertos;
        [Range(0f, 1f)] public float valor;
    }

    public static ProgressoAprendizado Instance { get; private set; }
    public static event Action<ConteudoMatematico, float> OnSubfaseConcluida;
    public static event Action<ConteudoMatematico, float, float> OnProgressoAtualizado;
    public static event Action<string> OnFaseConcluida;

    private readonly Dictionary<ConteudoMatematico, Registro> registros = new();
    private readonly HashSet<string> concluidasNestaPartida = new();
    private string cenaAtual;
    private bool faseAtualConcluida;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CriarServico()
    {
        if (Instance != null) return;
        new GameObject("Progresso de Aprendizado").AddComponent<ProgressoAprendizado>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        foreach (ConteudoMatematico c in Enum.GetValues(typeof(ConteudoMatematico)))
            registros[c] = new Registro();
    }

    public void IniciarCena(string cena)
    {
        if (cenaAtual == cena) return;
        cenaAtual = cena;
        concluidasNestaPartida.Clear();
        faseAtualConcluida = false;
    }

    public void RegistrarTentativa(ConfiguracaoAprendizado.Subfase subfase, bool acertou)
    {
        if (subfase == null || concluidasNestaPartida.Contains(subfase.id)) return;
        foreach (var item in subfase.conteudos)
        {
            Registro r = registros[item.conteudo];
            r.tentativas++;
            if (acertou) r.acertos++;

            // Regra unica: acerto aproxima 35% do caminho restante, ponderado pela dificuldade;
            // erro remove 8%. O resultado sempre permanece normalizado entre zero e um.
            float peso = Mathf.Max(.01f, item.peso);
            r.valor = acertou
                ? Mathf.Clamp01(r.valor + (1f - r.valor) * .35f * peso)
                : Mathf.Clamp01(r.valor - .08f * peso);
        }
    }

    public void ConcluirSubfase(ConfiguracaoAprendizado.Subfase subfase)
    {
        if (subfase == null || !concluidasNestaPartida.Add(subfase.id)) return;
        Dictionary<ConteudoMatematico, float> valoresAnteriores = new();
        foreach (var item in subfase.conteudos)
            valoresAnteriores[item.conteudo] = registros[item.conteudo].valor;
        RegistrarAcertoFinal(subfase);
        foreach (var item in subfase.conteudos)
        {
            OnProgressoAtualizado?.Invoke(item.conteudo, valoresAnteriores[item.conteudo], registros[item.conteudo].valor);
            OnSubfaseConcluida?.Invoke(item.conteudo, registros[item.conteudo].valor);
        }
    }

    private void RegistrarAcertoFinal(ConfiguracaoAprendizado.Subfase subfase)
    {
        foreach (var item in subfase.conteudos)
        {
            Registro r = registros[item.conteudo];
            r.tentativas++;
            r.acertos++;
            r.valor = Mathf.Clamp01(r.valor + (1f - r.valor) * .35f * Mathf.Max(.01f, item.peso));
        }
    }

    public Registro Obter(ConteudoMatematico conteudo) => registros[conteudo];

    /// <summary>Atalho exclusivo de desenvolvimento para testar a UI sem resolver a fase.</summary>
    public void AdicionarProgressoDev(ConteudoMatematico conteudo, float incremento = .1f)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Registro registro = registros[conteudo];
        float anterior = registro.valor;
        registro.valor = Mathf.Clamp01(registro.valor + Mathf.Max(0f, incremento));
        OnProgressoAtualizado?.Invoke(conteudo, anterior, registro.valor);
        OnSubfaseConcluida?.Invoke(conteudo, registro.valor);
#endif
    }

    public float ObterOverall(IEnumerable<ConteudoMatematico> conteudos)
    {
        float soma = 0f; int quantidade = 0;
        foreach (var c in conteudos) { soma += registros[c].valor; quantidade++; }
        return quantidade == 0 ? 0f : soma / quantidade;
    }

    public bool ConcluirFase(string cena)
    {
        if (faseAtualConcluida) return false;
        faseAtualConcluida = true;
        OnFaseConcluida?.Invoke(cena);
        return true;
    }
}
