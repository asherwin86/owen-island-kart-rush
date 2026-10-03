#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace KartRacer
{
    /// <summary>Editor-only: drives a bot around every track, saves screenshots and a report to the Shots folder.</summary>
    public class ResultsPreview : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var gf = FindFirstObjectByType<GameFlow>();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            yield return new WaitForSeconds(1f);
            typeof(GameFlow).GetMethod("StartGrandPrix", flags).Invoke(gf, new object[] { 1 });
            yield return new WaitForSeconds(6f);
            var p = gf.race.player;
            p.finished = true; p.finishTime = 65.4f;
            typeof(GameFlow).GetMethod("ShowResults", flags).Invoke(gf, null);
        }
    }

    public class TrackTester : MonoBehaviour
    {
        private IEnumerator Start()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Shots"));
            Directory.CreateDirectory(dir);
            var report = new StringBuilder();
            var gf = FindFirstObjectByType<GameFlow>();
            foreach (var go in GameObject.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) go.gameObject.SetActive(false);
            var rig = gf.cam; rig.enabled = false;
            var cam = rig.cam;
            var rt = new RenderTexture(640, 360, 24);
            Time.timeScale = 4f;

            foreach (var def in TrackDefs.All)
            {
                var track = Track.Create(def);
                var root = TrackBuilder.Build(track);
                yield return null;

                // overview
                Vector3 c = Vector3.zero; foreach (var p in track.pts) c += p; c /= track.pts.Length;
                float ext = 0f; foreach (var p in track.pts) ext = Mathf.Max(ext, Mathf.Abs(p.x - c.x), Mathf.Abs(p.z - c.z));
                cam.transform.position = c + Vector3.up * ext * 2.1f;
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Shot(cam, rt, Path.Combine(dir, def.Id.ToString("00") + "_top.png"));

                int start = track.IndexBehindStart(7f);
                Vector3 pos = track.pts[start];
                float heading = Mathf.Atan2(track.tan[start].x, track.tan[start].z) * Mathf.Rad2Deg;
                var k = Kart.Create("Bot", Color.red, false, 0, track, pos, heading);
                var ai = k.gameObject.AddComponent<AIDriver>();
                ai.kart = k; ai.baseSkill = ai.skill = 0.95f;
                k.raceActive = true;

                float t = 0f; bool shot = false; int lastIdx = k.trackIdx; float lapTime = -1f;
                while (t < 160f)
                {
                    yield return null;
                    t += Time.deltaTime;
                    int idx = track.Nearest(k.Position, k.trackIdx, 25);
                    int d = idx - k.trackIdx;
                    if (d < -track.N / 2) k.crossings++; else if (d > track.N / 2) k.crossings--;
                    k.trackIdx = idx;
                    if (!shot && t > 5f)
                    {
                        shot = true;
                        cam.transform.position = k.Position - k.Forward * 8f + Vector3.up * 4f;
                        cam.transform.rotation = Quaternion.LookRotation(k.Position + Vector3.up - cam.transform.position);
                        Shot(cam, rt, Path.Combine(dir, def.Id.ToString("00") + "_drive.png"));
                    }
                    if (k.crossings >= 2) { lapTime = t; break; }
                }
                report.AppendLine(def.Id.ToString("00") + " " + def.name + " (" + def.theme + "): " + (lapTime > 0 ? "lap " + lapTime.ToString("F1") + "s" : "DID NOT FINISH LAP") + ", respawns " + k.respawns + ", radius " + def.radius + ", width " + def.width);
                Destroy(k.gameObject); Destroy(root);
                yield return null;
            }
            File.WriteAllText(Path.Combine(dir, "report.txt"), report.ToString());
            Debug.Log("TRACK TEST DONE\n" + report);
            Time.timeScale = 1f;
            UnityEditor.EditorApplication.isPlaying = false;
        }

        private static void Shot(Camera cam, RenderTexture rt, string path)
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            RenderTexture.active = null;
            cam.targetTexture = null;
        }
    }
}
#endif
