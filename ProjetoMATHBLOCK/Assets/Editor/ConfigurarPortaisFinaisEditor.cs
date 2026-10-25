using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Ajusta somente o visual do portal a partir das folhas reais da porta final.</summary>
public static class ConfigurarPortaisFinaisEditor
{
    [MenuItem("MATHBLOCK/Portais/Ajustar e salvar porta final desta fase")]
    private static void Aplicar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Saia do Play primeiro.");
        Scene cena = SceneManager.GetActiveScene();
        if (cena.name != "Fase 1" && cena.name != "Fase 2" && cena.name != "Fase 3")
            throw new InvalidOperationException("Apenas Fases 1, 2 e 3 estao no escopo.");
        GatilhoConclusaoFase gatilho = LocalizarGatilho(cena);
        DoorOpener porta = gatilho.PortaFinal;
        if (!porta.TryGetDoorBounds(out Bounds abertura)) throw new InvalidOperationException("Folhas da porta sem renderizadores.");
        PortalParticleEffect portal = LocalizarPortal(cena);
        if (portal == null)
        {
            var objeto = new GameObject("Portal visual final");
            Undo.RegisterCreatedObjectUndo(objeto, "Adicionar visual do portal final");
            objeto.transform.SetParent(porta.transform, false);
            portal = Undo.AddComponent<PortalParticleEffect>(objeto);
            // O novo objeto e puramente visual; nao adiciona uma area de conclusao.
            objeto.GetComponent<BoxCollider>().enabled = false;
        }
        Undo.RecordObject(portal, "Ajustar interior do portal");
        Bounds local = ConverterBounds(abertura, portal.transform);
        PlayerMovement jogador = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        Vector3 aproximacao = jogador != null ? abertura.center - jogador.transform.position : portal.transform.forward;
        float sentido = Vector3.Dot(portal.transform.forward, aproximacao) >= 0f ? 1f : -1f;
        Vector3 centro = local.center;
        centro.z += sentido * (local.extents.z + 0.10f);
        // Pequena sobreposicao escondida pela moldura evita frestas nas quatro bordas.
        portal.ConfigurarInterior(centro, new Vector2(local.size.x, local.size.y) * 1.04f, PortalParticleEffect.PortalPlane.XY, sentido, porta);
        var propriedades = new SerializedObject(portal);
        propriedades.FindProperty("particleSize").floatValue = 0.3f / Mathf.Max(0.01f, Mathf.Abs(portal.transform.lossyScale.x));
        propriedades.FindProperty("particleSizeVariation").floatValue = 0.025f;
        propriedades.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(portal);
        EditorUtility.SetDirty(portal);
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        string pasta = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/RevisaoPortais"));
        Directory.CreateDirectory(pasta);
        File.WriteAllText(Path.Combine(pasta, cena.name + ".txt"),
            "Porta: " + porta.name + "\nAbertura real: " + abertura + "\nCentro local: " + centro +
            "\nTamanho: " + new Vector2(local.size.x, local.size.y) * 1.04f + "\nCollider de transicao preservado.");
    }

    private static GatilhoConclusaoFase LocalizarGatilho(Scene cena)
    {
        GatilhoConclusaoFase resultado = null;
        foreach (var candidato in UnityEngine.Object.FindObjectsByType<GatilhoConclusaoFase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidato.gameObject.scene != cena) continue;
            if (resultado != null) throw new InvalidOperationException("Mais de uma porta final; escolha explicita necessaria.");
            resultado = candidato;
        }
        if (resultado == null || resultado.PortaFinal == null) throw new InvalidOperationException("Porta final nao configurada.");
        return resultado;
    }

    private static PortalParticleEffect LocalizarPortal(Scene cena)
    {
        PortalParticleEffect resultado = null;
        foreach (var candidato in UnityEngine.Object.FindObjectsByType<PortalParticleEffect>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidato.gameObject.scene != cena) continue;
            if (resultado != null) throw new InvalidOperationException("Mais de um portal; ajuste explicito necessario.");
            resultado = candidato;
        }
        return resultado;
    }

    private static Bounds ConverterBounds(Bounds origem, Transform destino)
    {
        Bounds local = new Bounds(destino.InverseTransformPoint(origem.center), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            local.Encapsulate(destino.InverseTransformPoint(origem.center + Vector3.Scale(origem.extents, new Vector3(x, y, z))));
        return local;
    }

    [MenuItem("MATHBLOCK/Portais/Testar tremor perto")]
    private static void Perto() => PosicionarParaTeste(2f);
    [MenuItem("MATHBLOCK/Portais/Testar tremor longe")]
    private static void Longe() => PosicionarParaTeste(8f);
    private static void PosicionarParaTeste(float distancia)
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Use em Play, antes da visualizacao isolada.");
        var portal = LocalizarPortal(SceneManager.GetActiveScene());
        var jogador = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        Camera camera = Camera.main;
        if (jogador == null || portal == null || camera == null || !camera.transform.IsChildOf(jogador.transform))
            throw new InvalidOperationException("Entre novamente em Play para testar a camera original do jogador.");
        var olhar = jogador.GetComponentInChildren<Look>(true);
        if (olhar == null) olhar = jogador.GetComponent<Look>();
        jogador.enabled = false;
        var corpo = jogador.GetComponent<MeshRenderer>();
        if (corpo != null) corpo.enabled = false;
        LocalizarGatilho(SceneManager.GetActiveScene()).enabled = false;
        var controlador = jogador.GetComponent<CharacterController>();
        if (controlador != null) controlador.enabled = false;
        Vector3 normal = portal.transform.forward;
        float lado = Vector3.Dot(normal, camera.transform.position - portal.CentroInteriorMundo) >= 0f ? 1f : -1f;
        jogador.transform.rotation = Quaternion.LookRotation(-normal * lado, Vector3.up);
        jogador.transform.position += portal.CentroInteriorMundo + normal * lado * distancia - camera.transform.position;
        typeof(Look).GetField("mouseInput", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(olhar, Vector2.zero);
        typeof(Look).GetField("xRotation", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(olhar, 0f);
        // A porta deve continuar fechada durante este teste para nao disparar conclusao.
    }
    [MenuItem("MATHBLOCK/Portais/Pausar teste")]
    private static void Pausar() { if (EditorApplication.isPlaying) Time.timeScale = 0f; }
    [MenuItem("MATHBLOCK/Portais/Retomar teste")]
    private static void Retomar() { if (EditorApplication.isPlaying) Time.timeScale = 1f; }
    [MenuItem("MATHBLOCK/Portais/Abrir porta para teste de tremor")]
    private static void AbrirParaTeste()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Use em Play.");
        LocalizarGatilho(SceneManager.GetActiveScene()).PortaFinal.OpenOnce();
    }
    [MenuItem("MATHBLOCK/Portais/Relatar particulas e tremor")]
    private static void Relatar()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Use em Play.");
        var olhar = UnityEngine.Object.FindFirstObjectByType<Look>(FindObjectsInactive.Include);
        var portal = LocalizarPortal(SceneManager.GetActiveScene());
        var texto = new StringBuilder();
        float intensidade = (float)typeof(Look).GetField("portalIntensity", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(olhar);
        texto.AppendLine("Intensidade real na camera: " + intensidade);
        texto.AppendLine("Deslocamento real: " + typeof(Look).GetField("lastPortalShakeOffset", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(olhar));
        texto.AppendLine("Pausa: " + (Time.timeScale <= 0f));
        texto.AppendLine("Porta totalmente aberta: " + LocalizarGatilho(SceneManager.GetActiveScene()).PortaFinal.IsFullyOpen);
        texto.AppendLine("Intensidade a 2m: " + PortalParticleEffect.IntensidadeProxima(portal.CentroInteriorMundo - portal.transform.forward * 2f));
        texto.AppendLine("Intensidade a 8m: " + PortalParticleEffect.IntensidadeProxima(portal.CentroInteriorMundo - portal.transform.forward * 8f));
        int quantidade = 0;
        foreach (var sistema in portal.GetComponentsInChildren<ParticleSystem>())
        {
            quantidade += sistema.particleCount;
            var renderer = sistema.GetComponent<ParticleSystemRenderer>();
            texto.AppendLine(sistema.name + ": shader=" + renderer.sharedMaterial.shader.name + ", bounds=" + renderer.bounds);
            var pontos = new ParticleSystem.Particle[1];
            if (sistema.GetParticles(pontos, 1) > 0)
                texto.AppendLine("Ponto: " + pontos[0].position + ", cor=" + pontos[0].startColor + ", tamanho=" + pontos[0].startSize);
        }
        texto.AppendLine("Particulas reais vivas: " + quantidade);
        string pasta = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/RevisaoPortais"));
        Directory.CreateDirectory(pasta);
        string estado = Time.timeScale <= 0f ? "pausa" : !LocalizarGatilho(SceneManager.GetActiveScene()).PortaFinal.IsFullyOpen ? "fechada" : intensidade > 0.01f ? "perto" : "longe";
        File.WriteAllText(Path.Combine(pasta, "tremor-" + estado + ".txt"), texto.ToString());
    }

    [MenuItem("MATHBLOCK/Portais/Visualizar porta final em Play")]
    private static void Visualizar()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Use em Play.");
        Scene cena = SceneManager.GetActiveScene();
        var portal = LocalizarPortal(cena);
        DoorOpener porta = LocalizarGatilho(cena).PortaFinal;
        if (portal == null || Camera.main == null || !porta.TryGetDoorBounds(out Bounds abertura))
            throw new InvalidOperationException("Portal, camera ou folhas ausentes.");
        Camera camera = Camera.main;
        Vector3 centro = portal.CentroInteriorMundo;
        Vector3 normal = portal.transform.forward;
        float lado = Vector3.Dot(normal, camera.transform.position - centro) >= 0f ? 1f : -1f;
        foreach (Look olhar in UnityEngine.Object.FindObjectsByType<Look>(FindObjectsSortMode.None)) olhar.enabled = false;
        camera.transform.SetParent(null, true);
        float distancia = Mathf.Max(3.5f, abertura.size.y / (2f * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f)) * 1.5f);
        camera.transform.position = centro + normal * lado * distancia;
        camera.transform.rotation = Quaternion.LookRotation(centro - camera.transform.position, portal.transform.up);
        porta.OpenOnce();
        // A posicao do jogador continua no inicio. Nao dispara a conclusao nem grava progresso.
    }
}
