using UnityEngine;
using UnityEngine.EventSystems;

namespace KartRacer
{
    /// <summary>Builds the whole game at runtime so the scene can stay empty.</summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            }
            GameFlow.Create();
#if UNITY_EDITOR
            int at = PlayerPrefs.GetInt("kr_autotest", 0);
            PlayerPrefs.SetInt("kr_autotest", 0);
            if (at == 1) new GameObject("TrackTester").AddComponent<TrackTester>();
            if (at == 2) new GameObject("ResultsPreview").AddComponent<ResultsPreview>();
#endif
        }
    }
}
