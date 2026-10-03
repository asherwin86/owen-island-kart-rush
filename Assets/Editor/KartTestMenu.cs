using UnityEditor;
using UnityEngine;

namespace KartRacer.EditorTools
{
    public static class KartTestMenu
    {
        [MenuItem("Kart Racer/Test All Tracks (bot lap + screenshots)")]
        public static void Run()
        {
            PlayerPrefs.SetInt("kr_autotest", 1);
            PlayerPrefs.Save();
            EditorApplication.isPlaying = true;
        }
        [MenuItem("Kart Racer/Preview Grand Prix results")]
        public static void Preview()
        {
            PlayerPrefs.SetInt("kr_autotest", 2);
            PlayerPrefs.Save();
            EditorApplication.isPlaying = true;
        }
    }
}
