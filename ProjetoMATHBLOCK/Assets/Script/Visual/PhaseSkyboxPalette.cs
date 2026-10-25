using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Conserva o ceu da Fase 1 e anima somente suas cores, sem alterar os assets compartilhados.</summary>
public sealed class PhaseSkyboxPalette : MonoBehaviour
{
    [SerializeField, Min(0f)] private float esperaInicial = 4f;
    [SerializeField, Min(.1f)] private float duracaoFade = 4f;
    private readonly List<Material> materiais = new();
    private readonly List<Color[]> coresOriginais = new();
    private static readonly string[] propriedades = { "_BottomColor", "_MiddleColor", "_TopColor" };
    private int fase;
    private float tempo;
    private ToonCloudSkyGenerator nuvens;
    public float FracaoTransicao { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register() => GlobalSceneBootstrap.Register(ApplyToScene, -500);

    private static void ApplyToScene(Scene cena)
    {
        if (!TryGetFase(cena.name, out int numero) || numero == 1) return;
        foreach (GameObject raiz in cena.GetRootGameObjects())
            if (raiz.GetComponentInChildren<PhaseSkyboxPalette>(true) != null) return;
        var objeto = new GameObject("Ambiente - Transicao do ceu");
        SceneManager.MoveGameObjectToScene(objeto, cena);
        var transicao = objeto.AddComponent<PhaseSkyboxPalette>();
        transicao.fase = numero;
        transicao.Preparar(cena);
    }

    private static bool TryGetFase(string nome, out int numero)
    {
        for (int i = 1; i <= 4; i++)
            if (nome == "Fase " + i || nome == "Fase" + i) { numero = i; return true; }
        numero = 0; return false;
    }

    private void Preparar(Scene cena)
    {
        // O ceu visivel e o cilindro com gradiente, nao o procedural escondido atras dele.
        foreach (GameObject raiz in cena.GetRootGameObjects())
        foreach (Renderer renderizador in raiz.GetComponentsInChildren<Renderer>(true))
        {
            Material[] lista = renderizador.sharedMaterials;
            bool mudou = false;
            for (int i = 0; i < lista.Length; i++)
            {
                Material origem = lista[i];
                if (origem == null || !origem.HasProperty("_BottomColor") ||
                    !origem.HasProperty("_MiddleColor") || !origem.HasProperty("_TopColor") ||
                    !(origem.shader.name.Contains("Sky") || origem.shader.name.Contains("AbyssCylinder"))) continue;
                var copia = new Material(origem) { name = origem.name + " (Fade da fase)", hideFlags = HideFlags.DontSave };
                materiais.Add(copia);
                coresOriginais.Add(new[] { origem.GetColor(propriedades[0]), origem.GetColor(propriedades[1]), origem.GetColor(propriedades[2]) });
                lista[i] = copia; mudou = true;
            }
            if (mudou) renderizador.sharedMaterials = lista;
        }
        Aplicar(0f);
    }

    private void Update()
    {
        // A pausa nao consome os quatro segundos iniciais nem adianta o fade.
        tempo = Time.timeSinceLevelLoad;
        FracaoTransicao = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((tempo - esperaInicial) / duracaoFade));
        Aplicar(FracaoTransicao);
        // O gerador continua animando as nuvens; nao reenvia cores ja estabilizadas.
        if (FracaoTransicao >= 1f && nuvens != null) enabled = false;
    }

    private void Aplicar(float fracao)
    {
        for (int i = 0; i < materiais.Count; i++)
        for (int p = 0; p < propriedades.Length; p++)
        {
            // No cilindro inferior, o topo toca o horizonte do cilindro superior.
            int faixa = materiais[i].shader.name.Contains("AbyssCylinder") ? 2 - p : p;
            Color anterior = CorCeu(fase - 1, faixa, coresOriginais[i][p]);
            materiais[i].SetColor(propriedades[p], Color.Lerp(anterior, CorCeu(fase, faixa, coresOriginais[i][p]), fracao));
        }
        if (nuvens == null) nuvens = FindFirstObjectByType<ToonCloudSkyGenerator>();
        if (nuvens != null) nuvens.AplicarPaleta(
            Color.Lerp(CorNuvem(fase - 1), CorNuvem(fase), fracao),
            Color.Lerp(SombraNuvem(fase - 1), SombraNuvem(fase), fracao));
    }

    private static Color CorCeu(int numero, int faixa, Color original)
    {
        // Horizonte, faixa intermediaria, topo. O formato do gradiente nao muda.
        return numero switch
        {
            2 => faixa switch { 0 => new Color(.80f, .94f, .68f), 1 => new Color(.28f, .76f, .57f), _ => new Color(.035f, .57f, .48f) },
            3 => faixa switch { 0 => new Color(1f, .64f, .40f), 1 => new Color(.48f, .48f, .76f), _ => new Color(.27f, .29f, .65f) },
            4 => faixa switch { 0 => new Color(.19f, .12f, .17f), 1 => new Color(.87f, .32f, .10f), _ => new Color(1f, .48f, .08f) },
            _ => original
        };
    }

    private static Color CorNuvem(int numero) => numero switch
    {
        2 => new Color(1f, .91f, .62f), 3 => new Color(.98f, .77f, .67f),
        4 => new Color(.54f, .32f, .29f), _ => new Color(.86f, 1f, .95f)
    };
    private static Color SombraNuvem(int numero) => numero switch
    {
        2 => new Color(.48f, .61f, .35f), 3 => new Color(.35f, .31f, .52f),
        4 => new Color(.20f, .12f, .20f), _ => new Color(.30f, .70f, .74f)
    };

    private void OnDestroy()
    {
        foreach (Material material in materiais) if (material != null) Destroy(material);
    }
}
