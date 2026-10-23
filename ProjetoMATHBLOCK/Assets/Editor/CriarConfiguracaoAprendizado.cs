#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class CriarConfiguracaoAprendizado
{
    private const string Pasta = "Assets/Resources/Aprendizado";
    private const string Caminho = Pasta + "/ConfiguracaoAprendizado.asset";

    [MenuItem("MATHBLOCK/Aprendizado/Criar ou restaurar configuracao padrao")]
    public static void Criar()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Pasta))
            AssetDatabase.CreateFolder("Assets/Resources", "Aprendizado");

        ConfiguracaoAprendizado config = AssetDatabase.LoadAssetAtPath<ConfiguracaoAprendizado>(Caminho);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<ConfiguracaoAprendizado>();
            AssetDatabase.CreateAsset(config, Caminho);
        }
        config.AplicarPadrao();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Selection.activeObject = config;
        Debug.Log("Configuracao de aprendizado criada em " + Caminho);
    }
}
#endif
