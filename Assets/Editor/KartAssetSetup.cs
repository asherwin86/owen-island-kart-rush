using UnityEditor;
using UnityEngine;

namespace KartRacer.EditorTools
{
    /// <summary>Creates the two base URP materials in a Resources folder so their shaders are
    /// always included in WebGL builds (Shader.Find can be stripped).</summary>
    [InitializeOnLoad]
    public static class KartAssetSetup
    {
        static KartAssetSetup() { EditorApplication.delayCall += Ensure; }

        private static void Ensure()
        {
            Make("Assets/Resources/KartLit.mat", "Universal Render Pipeline/Lit");
            Make("Assets/Resources/KartUnlit.mat", "Universal Render Pipeline/Unlit");
        }

        private static void Make(string path, string shaderName)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find(shaderName);
            if (shader == null) return;
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateAsset(new Material(shader), path);
            AssetDatabase.SaveAssets();
        }
    }
}
