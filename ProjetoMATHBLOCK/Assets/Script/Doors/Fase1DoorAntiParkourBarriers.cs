using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fecha as rotas externas ao lado das portas da Fase 1 sem bloquear seus vãos.
/// As barreiras são apenas colliders: não possuem renderer e são invisíveis.
/// </summary>
public sealed class Fase1DoorAntiParkourBarriers : MonoBehaviour
{
    private const string TargetSceneName = "Fase 1";
    private const string BarrierRootName = "BarreirasInvisiveisAntiParkour";

    // Dimensões em espaço local da porta. A moldura ocupa aproximadamente
    // 11 unidades de largura; cada extensão começa depois dela.
    private static readonly Vector3 LeftCenter = new Vector3(-15.5f, 8f, 0f);
    private static readonly Vector3 RightCenter = new Vector3(15.5f, 8f, 0f);
    private static readonly Vector3 SideBarrierSize = new Vector3(20f, 20f, 4f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneInitializer()
    {
        GlobalSceneBootstrap.Register(InstallInFase1);
    }

    private static void InstallInFase1(Scene activeScene)
    {
        if (!activeScene.IsValid() || activeScene.name != TargetSceneName)
            return;

        if (GameObject.Find(BarrierRootName) != null)
            return;

        DoorValueVerifier[] doors = FindObjectsByType<DoorValueVerifier>(FindObjectsSortMode.None);
        if (doors == null || doors.Length == 0)
        {
            Debug.LogWarning("Fase 1: nenhuma porta encontrada para instalar as barreiras anti-parkour.");
            return;
        }

        GameObject root = new GameObject(BarrierRootName);
        root.isStatic = true;

        int installedDoorCount = 0;
        foreach (DoorValueVerifier door in doors)
        {
            // Verificadores de puzzle independentes nao representam portas.
            if (door == null || door.GetComponent<DoorOpener>() == null)
                continue;

            CreateSideBarrier(root.transform, door.transform, "Esquerda", LeftCenter);
            CreateSideBarrier(root.transform, door.transform, "Direita", RightCenter);
            installedDoorCount++;
        }

        Debug.Log($"Fase 1: barreiras anti-parkour instaladas em {installedDoorCount} portas.");
    }

    private static void CreateSideBarrier(
        Transform root,
        Transform door,
        string sideName,
        Vector3 localCenter)
    {
        GameObject barrier = new GameObject($"Barreira_{door.name}_{sideName}");
        barrier.layer = door.gameObject.layer;
        barrier.isStatic = true;
        barrier.transform.SetParent(root, false);

        // Usa a orientação e a escala da porta, mas mantém a barreira fora da
        // hierarquia animada para ela não se mover quando as folhas abrirem.
        barrier.transform.SetPositionAndRotation(
            door.TransformPoint(localCenter),
            door.rotation);
        barrier.transform.localScale = Vector3.Scale(door.lossyScale, SideBarrierSize);

        BoxCollider collider = barrier.AddComponent<BoxCollider>();
        collider.isTrigger = false;
        collider.center = Vector3.zero;
        collider.size = Vector3.one;

        // Apenas a primeira porta recebe a brincadeira; as demais mantem
        // exatamente o bloqueio existente. O sensor nao modifica o collider solido.
        if (door.name == "porta1")
        {
            var sensor = new GameObject("Sensor de tentativa de atalho");
            sensor.layer = barrier.layer;
            sensor.transform.SetParent(barrier.transform, false);
            BoxCollider area = sensor.AddComponent<BoxCollider>();
            area.isTrigger = true;
            Vector3 escala = sensor.transform.lossyScale;
            area.size = Vector3.one + new Vector3(.3f / Mathf.Abs(escala.x), .3f / Mathf.Abs(escala.y), .3f / Mathf.Abs(escala.z));
            Rigidbody corpo = sensor.AddComponent<Rigidbody>();
            corpo.isKinematic = true;
            corpo.useGravity = false;
            sensor.AddComponent<AvisoAntiAtalho>().Configurar(door.GetComponent<DoorOpener>());
        }
    }
}
