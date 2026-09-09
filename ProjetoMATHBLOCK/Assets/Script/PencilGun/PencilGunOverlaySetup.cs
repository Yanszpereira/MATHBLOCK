using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Separa a PencilGun da câmera do mundo. A arma é desenhada por uma câmera
/// overlay depois do pós-processamento e usa o ToonShader do MATHBLOCK.
/// </summary>
public sealed class PencilGunOverlaySetup : MonoBehaviour
{
    private const int PencilGunLayer = 3;
    private readonly List<Material> runtimeMaterials = new List<Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera camera in cameras)
        {
            if (camera == null)
                continue;

            Transform gun = FindDescendantByName(camera.transform, "PencilGun");
            if (gun == null)
                continue;

            if (gun.GetComponent<PencilGunOverlaySetup>() == null)
                gun.gameObject.AddComponent<PencilGunOverlaySetup>();
            if (gun.GetComponent<PencilTipOperatorColor>() == null)
                gun.gameObject.AddComponent<PencilTipOperatorColor>();
        }
    }

    private void Awake()
    {
        SetLayerRecursively(transform, PencilGunLayer);

        Camera overlayCamera = GetComponentInParent<Camera>();
        if (overlayCamera == null)
        {
            Debug.LogWarning("PencilGun: câmera overlay não encontrada.", this);
            return;
        }

        Camera worldCamera = FindWorldCamera(overlayCamera);
        if (worldCamera == null)
        {
            Debug.LogWarning("PencilGun: câmera principal do cenário não encontrada.", this);
            return;
        }

        // A câmera do mundo nunca desenha a arma; assim fog, blur e demais
        // efeitos de imagem são concluídos antes da composição da PencilGun.
        worldCamera.cullingMask &= ~(1 << PencilGunLayer);

        overlayCamera.tag = "Untagged";
        overlayCamera.clearFlags = CameraClearFlags.Depth;
        overlayCamera.cullingMask = 1 << PencilGunLayer;
        overlayCamera.depth = worldCamera.depth + 100f;
        overlayCamera.allowHDR = worldCamera.allowHDR;
        overlayCamera.allowMSAA = worldCamera.allowMSAA;
        overlayCamera.useOcclusionCulling = false;
        overlayCamera.nearClipPlane = 0.01f;
        overlayCamera.farClipPlane = 5f;
        overlayCamera.fieldOfView = worldCamera.fieldOfView;

        ApplyToonMaterials();
    }

    private void ApplyToonMaterials()
    {
        Shader toonShader = Shader.Find("Custom/URPToonShader");
        if (toonShader == null || !toonShader.isSupported)
        {
            Debug.LogWarning("PencilGun: ToonShader não encontrado ou incompatível.", this);
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null || targetRenderer is ParticleSystemRenderer)
                continue;

            Material[] originals = targetRenderer.sharedMaterials;
            Material[] toonMaterials = new Material[originals.Length];
            for (int index = 0; index < originals.Length; index++)
            {
                Material original = originals[index];
                if (original == null)
                    continue;

                Texture texture = original.HasProperty("_MainTex")
                    ? original.GetTexture("_MainTex")
                    : original.HasProperty("_BaseMap") ? original.GetTexture("_BaseMap") : null;
                Color color = original.HasProperty("_BaseColor")
                    ? original.GetColor("_BaseColor")
                    : original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;

                Material toon = new Material(toonShader)
                {
                    name = original.name + " (PencilGun Toon)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (texture != null)
                    toon.SetTexture("_MainTex", texture);
                toon.SetColor("_BaseColor", color);
                toon.SetColor("_Color", color);
                toon.EnableKeyword("_SPECULAR_ON");
                toon.EnableKeyword("_RIM_ON");
                toon.EnableKeyword("_OUTLINE_ON");
                toonMaterials[index] = toon;
                runtimeMaterials.Add(toon);
            }
            targetRenderer.sharedMaterials = toonMaterials;
        }
    }

    private static Camera FindWorldCamera(Camera overlayCamera)
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        Camera result = null;
        foreach (Camera candidate in cameras)
        {
            if (candidate == null || candidate == overlayCamera || candidate.cullingMask == 0)
                continue;
            if (result == null || candidate.depth < result.depth)
                result = candidate;
        }
        return result;
    }

    private static Transform FindDescendantByName(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (root.name == objectName)
            return root;
        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindDescendantByName(root.GetChild(index), objectName);
            if (found != null)
                return found;
        }
        return null;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int index = 0; index < root.childCount; index++)
            SetLayerRecursively(root.GetChild(index), layer);
    }

    private void OnDestroy()
    {
        foreach (Material material in runtimeMaterials)
            if (material != null)
                Destroy(material);
        runtimeMaterials.Clear();
    }
}
