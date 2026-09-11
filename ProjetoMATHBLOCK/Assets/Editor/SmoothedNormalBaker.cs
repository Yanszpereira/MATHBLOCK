using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SmoothedNormalBaker
{
    private const float PositionEpsilon = 0.0001f;
    private const int SmoothedNormalUvChannel = 3; // UV4 / TEXCOORD3
    private const string OutputFolder = "Assets/Generated/ToonMeshes";
    private const string SmoothedNormalsKeyword = "_USE_SMOOTHED_NORMALS";
    private static readonly int UseSmoothedNormalsId = Shader.PropertyToID("_UseSmoothedNormals");

    [MenuItem("Tools/MATHBLOCK/Toon Outline/Bake Smoothed Normals (Selected)")]
    private static void BakeSelected()
    {
        GameObject[] selection = Selection.gameObjects;
        if (selection == null || selection.Length == 0)
        {
            Debug.LogWarning("Smoothed Normal Baker: selecione ao menos um objeto.");
            return;
        }

        EnsureOutputFolder();
        HashSet<Component> processedComponents = new HashSet<Component>();
        int processed = 0;

        foreach (GameObject selectedObject in selection)
        {
            foreach (MeshFilter filter in selectedObject.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!processedComponents.Add(filter) || !TryBake(filter.sharedMesh, out Mesh bakedMesh))
                    continue;

                Undo.RecordObject(filter, "Bake Toon Smoothed Normals");
                filter.sharedMesh = bakedMesh;
                EnableSmoothedNormals(filter.GetComponent<Renderer>());
                EditorUtility.SetDirty(filter);
                processed++;
            }

            foreach (SkinnedMeshRenderer skinned in selectedObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!processedComponents.Add(skinned) || !TryBake(skinned.sharedMesh, out Mesh bakedMesh))
                    continue;

                Undo.RecordObject(skinned, "Bake Toon Smoothed Normals");
                skinned.sharedMesh = bakedMesh;
                EnableSmoothedNormals(skinned);
                EditorUtility.SetDirty(skinned);
                processed++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Smoothed Normal Baker: {processed} malha(s) copiadas para '{OutputFolder}' usando UV4.");
    }

    private static bool TryBake(Mesh source, out Mesh bakedMesh)
    {
        bakedMesh = null;
        if (source == null)
            return false;

        if (!source.isReadable)
        {
            Debug.LogWarning($"Smoothed Normal Baker: '{source.name}' não permite leitura. Ative Read/Write no importador.", source);
            return false;
        }

        bakedMesh = Object.Instantiate(source);
        bakedMesh.name = source.name + "_SmoothedUV4";

        Vector3[] vertices = bakedMesh.vertices;
        Vector3[] normals = bakedMesh.normals;
        if (normals == null || normals.Length != vertices.Length)
        {
            bakedMesh.RecalculateNormals();
            normals = bakedMesh.normals;
        }

        Dictionary<Vector3Int, List<int>> groups = new Dictionary<Vector3Int, List<int>>();
        for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
        {
            Vector3 vertex = vertices[vertexIndex];
            Vector3Int key = new Vector3Int(
                Mathf.RoundToInt(vertex.x / PositionEpsilon),
                Mathf.RoundToInt(vertex.y / PositionEpsilon),
                Mathf.RoundToInt(vertex.z / PositionEpsilon));

            if (!groups.TryGetValue(key, out List<int> indices))
            {
                indices = new List<int>();
                groups.Add(key, indices);
            }

            indices.Add(vertexIndex);
        }

        Vector3[] smoothedNormals = new Vector3[vertices.Length];
        foreach (List<int> indices in groups.Values)
        {
            Vector3 sum = Vector3.zero;
            for (int index = 0; index < indices.Count; index++)
                sum += normals[indices[index]];

            Vector3 averaged = sum.sqrMagnitude > 0.00000001f ? sum.normalized : Vector3.up;
            for (int index = 0; index < indices.Count; index++)
                smoothedNormals[indices[index]] = averaged;
        }

        bakedMesh.SetUVs(SmoothedNormalUvChannel, new List<Vector3>(smoothedNormals));
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            $"{OutputFolder}/{SanitizeFileName(bakedMesh.name)}.asset");
        AssetDatabase.CreateAsset(bakedMesh, assetPath);
        return true;
    }

    private static void EnableSmoothedNormals(Renderer renderer)
    {
        if (renderer == null)
            return;

        Material[] materials = renderer.sharedMaterials;
        for (int index = 0; index < materials.Length; index++)
        {
            Material material = materials[index];
            if (material == null || !material.HasProperty(UseSmoothedNormalsId))
                continue;

            Undo.RecordObject(material, "Enable Toon Smoothed Normals");
            material.SetFloat(UseSmoothedNormalsId, 1f);
            material.EnableKeyword(SmoothedNormalsKeyword);
            EditorUtility.SetDirty(material);
        }
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Generated"))
            AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Generated", "ToonMeshes");
    }

    private static string SanitizeFileName(string value)
    {
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidCharacter, '_');
        return value;
    }
}
