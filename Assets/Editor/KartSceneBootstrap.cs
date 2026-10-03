using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KartRacer.EditorTools
{
    /// <summary>Creates the (nearly empty) Main scene and adds it to the build the first time the project opens.
    /// The whole game is built at runtime by KartRacer.Bootstrap.</summary>
    [InitializeOnLoad]
    public static class KartSceneBootstrap
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        static KartSceneBootstrap()
        {
            EditorApplication.delayCall += Ensure;
        }

        private static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!System.IO.File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || scenes[0].path != ScenePath)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (SceneManager.GetActiveScene().path != ScenePath && !EditorApplication.isCompiling)
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Kart Racer/Build WebGL (WebBuild)")]
        public static void BuildWeb()
        {
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "WebBuild",
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            BuildPipeline.BuildPlayer(opts);
        }
    }
}
