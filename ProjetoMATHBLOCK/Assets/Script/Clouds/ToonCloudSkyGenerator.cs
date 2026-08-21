using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ToonCloudSkyGenerator : MonoBehaviour
{
    private const string CloudModelPath = "Environment/Clouds/CLOUD";
    private const int DesktopCloudCount = 14;
    private const int MobileCloudCount = 9;

    private readonly List<Transform> clouds = new List<Transform>();
    private Material cloudMaterial;
    private Transform viewer;
    private float recycleRadius;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string sceneName = scene.name;
        if (!sceneName.StartsWith("Fase") && sceneName != "MainScene")
            return;

        if (FindFirstObjectByType<ToonCloudSkyGenerator>() != null)
            return;

        GameObject generator = new GameObject("Environment - Toon 3D Clouds");
        generator.AddComponent<ToonCloudSkyGenerator>();
    }

    private void Awake()
    {
        RemoveLegacyParticleClouds();
        viewer = Camera.main != null ? Camera.main.transform : transform;
        CreateToonMaterial();
        GenerateCloudField();
    }

    private void LateUpdate()
    {
        if (viewer == null && Camera.main != null)
            viewer = Camera.main.transform;

        if (viewer == null)
            return;

        Vector3 center = viewer.position;
        for (int i = 0; i < clouds.Count; i++)
        {
            Transform cloud = clouds[i];
            if (cloud == null)
                continue;

            cloud.position += cloud.forward * (0.35f + i % 4 * 0.08f) * Time.deltaTime;
            Vector2 horizontalOffset = new Vector2(cloud.position.x - center.x, cloud.position.z - center.z);
            if (horizontalOffset.sqrMagnitude > recycleRadius * recycleRadius)
                PlaceCloud(cloud, i, true);
        }
    }

    private void GenerateCloudField()
    {
        GameObject model = Resources.Load<GameObject>(CloudModelPath);
        if (model == null)
        {
            Debug.LogError($"Modelo de nuvem não encontrado em Resources/{CloudModelPath}.", this);
            return;
        }

        int count = Application.isMobilePlatform ? MobileCloudCount : DesktopCloudCount;
        recycleRadius = Application.isMobilePlatform ? 190f : 235f;

        for (int i = 0; i < count; i++)
        {
            GameObject cloud = Instantiate(model, transform);
            cloud.name = $"Toon Cloud {i + 1:00}";
            cloud.tag = "Cloud";
            cloud.AddComponent<DistanceFogBlurExclude>();
            StripUnwantedComponents(cloud);
            ApplyToonMaterial(cloud);
            PlaceCloud(cloud.transform, i, false);
            clouds.Add(cloud.transform);
        }
    }

    private void PlaceCloud(Transform cloud, int index, bool oppositeSide)
    {
        Vector3 center = viewer != null ? viewer.position : Vector3.zero;
        float count = Application.isMobilePlatform ? MobileCloudCount : DesktopCloudCount;
        float angle = (index / count) * Mathf.PI * 2f + Random.Range(-0.28f, 0.28f);
        if (oppositeSide)
            angle += Mathf.PI;

        float distance = Random.Range(recycleRadius * 0.30f, recycleRadius * 0.72f);
        cloud.position = center + new Vector3(
            Mathf.Cos(angle) * distance,
            Random.Range(42f, 88f),
            Mathf.Sin(angle) * distance);

        float scale = Random.Range(29f, 58f);
        cloud.localScale = new Vector3(
            scale * Random.Range(1.15f, 1.85f),
            scale * Random.Range(0.62f, 1.05f),
            scale * Random.Range(0.9f, 1.4f));
        cloud.rotation = Quaternion.Euler(
            Random.Range(-5f, 5f),
            Random.Range(0f, 360f),
            Random.Range(-3f, 3f));
    }

    private void CreateToonMaterial()
    {
        Shader toonShader = Shader.Find("Custom/URPToonShader");
        if (toonShader == null)
            toonShader = Shader.Find("Universal Render Pipeline/Lit");
        if (toonShader == null)
            toonShader = Shader.Find("Standard");

        cloudMaterial = new Material(toonShader) { name = "Cloud Toon Material (Runtime)" };
        SetColor("_BaseColor", new Color(0.86f, 1f, 0.95f, 1f));
        SetColor("_Color", new Color(0.86f, 1f, 0.95f, 1f));
        SetColor("_ShadeColor", new Color(0.30f, 0.70f, 0.74f, 1f));
        SetColor("_RimColor", new Color(0.95f, 1f, 0.98f, 1f));
        SetColor("_OutlineColor", new Color(0.11f, 0.34f, 0.39f, 1f));
        SetFloat("_ShadeSteps", 3f);
        SetFloat("_ShadeSmoothness", 0.11f);
        SetFloat("_MinBrightness", 0.58f);
        SetFloat("_AmbientStrength", 0.68f);
        SetFloat("_EnableSpecular", 0f);
        SetFloat("_EnableRim", 1f);
        SetFloat("_RimAmount", 0.65f);
        SetFloat("_OutlineWidth", Application.isMobilePlatform ? 0f : 0.0022f);
        if (!Application.isMobilePlatform)
            cloudMaterial.EnableKeyword("_OUTLINE_ON");
        cloudMaterial.EnableKeyword("_RIM_ON");
        cloudMaterial.enableInstancing = true;
    }

    private void ApplyToonMaterial(GameObject cloud)
    {
        foreach (Renderer targetRenderer in cloud.GetComponentsInChildren<Renderer>(true))
        {
            if (targetRenderer is ParticleSystemRenderer)
                continue;

            Material[] materials = new Material[Mathf.Max(1, targetRenderer.sharedMaterials.Length)];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = cloudMaterial;
            targetRenderer.sharedMaterials = materials;
            targetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            targetRenderer.receiveShadows = false;
        }
    }

    private static void StripUnwantedComponents(GameObject cloud)
    {
        foreach (ParticleSystem particles in cloud.GetComponentsInChildren<ParticleSystem>(true))
            Destroy(particles.gameObject);

        foreach (MonoBehaviour behaviour in cloud.GetComponentsInChildren<MonoBehaviour>(true))
            Destroy(behaviour);

        foreach (Collider collider in cloud.GetComponentsInChildren<Collider>(true))
            Destroy(collider);
    }

    private static void RemoveLegacyParticleClouds()
    {
        foreach (CloudSpawnerManager spawner in FindObjectsByType<CloudSpawnerManager>(FindObjectsSortMode.None))
            spawner.enabled = false;

        foreach (ParticleSystem particles in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            if (BelongsToLegacyCloud(particles.transform))
                Destroy(particles.gameObject);
        }
    }

    private static bool BelongsToLegacyCloud(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains("cloud") || lowerName.Contains("nuvem") || current.GetComponent<CloudMover>() != null)
                return true;
            current = current.parent;
        }
        return false;
    }

    private void SetColor(string propertyName, Color value)
    {
        if (cloudMaterial.HasProperty(propertyName))
            cloudMaterial.SetColor(propertyName, value);
    }

    private void SetFloat(string propertyName, float value)
    {
        if (cloudMaterial.HasProperty(propertyName))
            cloudMaterial.SetFloat(propertyName, value);
    }

    private void OnDestroy()
    {
        if (cloudMaterial != null)
            Destroy(cloudMaterial);
    }
}
