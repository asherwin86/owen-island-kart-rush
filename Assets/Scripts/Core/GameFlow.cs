using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static KartRacer.UIKit;

namespace KartRacer
{
    /// <summary>Title screen, island map with 4 realms x 5 tracks, pause and results. Everything is built in code.</summary>
    public class GameFlow : MonoBehaviour
    {
        public static readonly Color[] KartColors =
        {
            new Color(0.95f, 0.25f, 0.3f), new Color(0.25f, 0.55f, 1f), new Color(0.3f, 0.85f, 0.4f), new Color(1f, 0.8f, 0.15f),
            new Color(0.75f, 0.4f, 0.95f), new Color(1f, 0.55f, 0.15f), new Color(1f, 0.5f, 0.8f), new Color(0.2f, 0.9f, 0.9f),
        };

        public CameraRig cam;
        public RaceManager race;
        public RaceHUD hud;
        public string playerName = "Racer";
        public Color playerColor;
        public TrackDef current;

        private GameObject _title, _island, _pause, _results;
        private TMP_InputField _nameInput;
        private TMP_Text _regionTitle, _regionTag, _resultTitle, _resultList;
        private Button[] _trackButtons = new Button[5];
        private TMP_Text[] _trackLabels = new TMP_Text[5];
        private Image[] _regionButtons = new Image[4];
        private Image[] _swatches = new Image[8];
        private int _region, _colorIndex;
        private bool _paused;
        public System.Action OnlineClicked;
        public OnlineFlow Online;

        public static void Create()
        {
            var go = new GameObject("GameFlow");
            var gf = go.AddComponent<GameFlow>();
            gf.Init();
        }

        private void Init()
        {
            Application.runInBackground = true;
            cam = CameraRig.Create();
            race = new GameObject("Race").AddComponent<RaceManager>();
            race.transform.SetParent(transform, false);
            _colorIndex = PlayerPrefs.GetInt("kr_color", 3);
            playerColor = KartColors[_colorIndex];
            playerName = PlayerPrefs.GetString("kr_name", "Racer");

            var canvas = MakeCanvas("MenuCanvas", 50, transform);
            BuildTitle(canvas.transform);
            BuildIsland(canvas.transform);
            BuildPause(canvas.transform);
            BuildResults(canvas.transform);
            hud = RaceHUD.Create(transform, race, TogglePause);
            Online = OnlineFlow.Create(this);
            OnlineClicked = Online.Open;
            ShowTitle();
        }

        // ------------------------------------------------------------------ screens

        private void BuildTitle(Transform parent)
        {
            _title = Group(parent, "Title");
            Panel(_title.transform, "Sky", new Color(0.38f, 0.68f, 1f));
            var low = Box(_title.transform, "Ground", B, new Vector2(0f, 0f), new Vector2(1400f, 230f), new Color(0.4f, 0.85f, 0.45f), false);
            low.rectTransform.anchorMin = new Vector2(0f, 0f); low.rectTransform.anchorMax = new Vector2(1f, 0f);
            low.rectTransform.sizeDelta = new Vector2(0f, 200f);
            var t = Label(_title.transform, "ISLAND KART RUSH", 96f, C, new Vector2(0f, 230f), new Vector2(1200f, 130f));
            t.fontStyle = FontStyles.Bold;
            Label(_title.transform, "Four realms. Twenty tracks. One island.", 36f, C, new Vector2(0f, 150f), new Vector2(1000f, 60f), TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.9f));

            _nameInput = InputBox(_title.transform, "Your name", C, new Vector2(0f, 60f), new Vector2(440f, 64f), 12);
            _nameInput.text = playerName == "Racer" ? "" : playerName;
            Label(_title.transform, "Pick your kart colour", 28f, C, new Vector2(0f, -10f), new Vector2(600f, 40f));
            for (int i = 0; i < 8; i++)
            {
                int idx = i;
                var sw = Btn(_title.transform, "", C, new Vector2((i - 3.5f) * 76f, -70f), new Vector2(64f, 64f), KartColors[i], () => PickColor(idx));
                _swatches[i] = sw.GetComponent<Image>();
            }
            Btn(_title.transform, "PLAY", C, new Vector2(0f, -170f), new Vector2(380f, 92f), new Color(0.15f, 0.7f, 0.3f), OpenIsland, 46f);
            Btn(_title.transform, "ONLINE RACE", C, new Vector2(0f, -275f), new Vector2(380f, 70f), new Color(0.2f, 0.45f, 0.9f), () => OnlineClicked?.Invoke(), 34f);
            Label(_title.transform, "W / Arrows: drive    A / D: steer    Space: drift    E: use item    R: reset    Esc: pause", 22f, B, new Vector2(0f, 24f), new Vector2(1200f, 40f), TextAlignmentOptions.Center, new Color(0.1f, 0.2f, 0.1f));
            PickColor(_colorIndex);
        }

        private void PickColor(int i)
        {
            _colorIndex = i; playerColor = KartColors[i];
            PlayerPrefs.SetInt("kr_color", i);
            for (int s = 0; s < _swatches.Length; s++) _swatches[s].transform.localScale = Vector3.one * (s == i ? 1.25f : 1f);
        }

        private void BuildIsland(Transform parent)
        {
            _island = Group(parent, "Island");
            Panel(_island.transform, "Sea", new Color(0.08f, 0.42f, 0.78f));
            var map = new GameObject("IslandMap", typeof(RectTransform), typeof(Image));
            map.transform.SetParent(_island.transform, false);
            var mrt = (RectTransform)map.transform;
            mrt.anchorMin = mrt.anchorMax = mrt.pivot = C; mrt.anchoredPosition = new Vector2(-300f, -10f); mrt.sizeDelta = new Vector2(620f, 620f);
            var mimg = map.GetComponent<Image>();
            mimg.sprite = MakeIslandSprite(); mimg.preserveAspect = true; mimg.raycastTarget = false;

            Label(_island.transform, "THE ISLAND", 54f, TL, new Vector2(330f, -50f), new Vector2(600f, 80f), TextAlignmentOptions.Left).fontStyle = FontStyles.Bold;
            Btn(_island.transform, "< BACK", TL, new Vector2(100f, -105f), new Vector2(160f, 54f), new Color(0.15f, 0.2f, 0.4f, 0.9f), ShowTitle, 26f);

            Vector2[] centers = { new Vector2(-150f, 150f), new Vector2(150f, 150f), new Vector2(-150f, -150f), new Vector2(150f, -150f) };
            for (int r = 0; r < 4; r++)
            {
                int reg = r;
                var pal = TrackDefs.Palettes[r];
                var b = Btn(map.transform, pal.name.ToUpper(), C, centers[r], new Vector2(250f, 60f), Color.Lerp(pal.accent, Color.black, 0.25f), () => SelectRegion(reg), 26f);
                _regionButtons[r] = b.GetComponent<Image>();
                // keep clicks working even though the map image ignores raycasts
            }

            var panel = Box(_island.transform, "TrackPanel", R, new Vector2(-340f, -10f), new Vector2(520f, 600f), new Color(0f, 0f, 0f, 0.55f));
            _regionTitle = Label(panel.transform, "", 44f, T, new Vector2(0f, -50f), new Vector2(500f, 70f));
            _regionTitle.fontStyle = FontStyles.Bold;
            _regionTag = Label(panel.transform, "", 24f, T, new Vector2(0f, -105f), new Vector2(500f, 40f), TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.8f));
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                var tb = Btn(panel.transform, "", T, new Vector2(0f, -190f - i * 82f), new Vector2(470f, 72f), new Color(1f, 1f, 1f, 0.2f), () => StartTrack(TrackDefs.Get(_region, idx)));
                _trackButtons[i] = tb;
                _trackLabels[i] = tb.GetComponentInChildren<TMP_Text>();
                _trackLabels[i].fontSize = 28f;
                _trackLabels[i].alignment = TextAlignmentOptions.Left;
                ((RectTransform)_trackLabels[i].transform).offsetMin = new Vector2(24f, 0f);
            }
            SelectRegion(0);
        }

        private void SelectRegion(int r)
        {
            _region = r;
            var pal = TrackDefs.Palettes[r];
            _regionTitle.text = pal.name; _regionTitle.color = Color.Lerp(pal.accent, Color.white, 0.4f);
            _regionTag.text = pal.tagline;
            for (int i = 0; i < 5; i++)
            {
                var d = TrackDefs.Get(r, i);
                float best = PlayerPrefs.GetFloat("kr_best_" + d.Id, 0f);
                string stars = new string('*', i + 1) + "<color=#ffffff55>" + new string('*', 4 - i) + "</color>";
                _trackLabels[i].text = (i + 1) + "   " + d.name + "\n<size=20><color=#ffd54a>" + stars + "</color>   " + (best > 0f ? "best " + TimeString(best) : "no time yet") + "</size>";
                _trackButtons[i].GetComponent<Image>().color = Color.Lerp(new Color(0f, 0f, 0f, 0.35f), pal.accent, 0.35f);
            }
            for (int k = 0; k < 4; k++) _regionButtons[k].transform.localScale = Vector3.one * (k == r ? 1.15f : 1f);
        }

        private void BuildPause(Transform parent)
        {
            _pause = Group(parent, "Pause");
            Panel(_pause.transform, "Dim", new Color(0f, 0f, 0f, 0.6f));
            Label(_pause.transform, "PAUSED", 80f, C, new Vector2(0f, 170f), new Vector2(800f, 110f)).fontStyle = FontStyles.Bold;
            Btn(_pause.transform, "RESUME", C, new Vector2(0f, 50f), new Vector2(380f, 80f), new Color(0.15f, 0.7f, 0.3f), TogglePause, 40f);
            Btn(_pause.transform, "RESTART", C, new Vector2(0f, -50f), new Vector2(380f, 70f), new Color(0.2f, 0.45f, 0.9f), () => { SetPaused(false); StartTrack(current); }, 34f);
            Btn(_pause.transform, "ISLAND MAP", C, new Vector2(0f, -140f), new Vector2(380f, 70f), new Color(0.6f, 0.25f, 0.25f), () => { SetPaused(false); OpenIsland(); }, 34f);
            _pause.SetActive(false);
        }

        private void BuildResults(Transform parent)
        {
            _results = Group(parent, "Results");
            Panel(_results.transform, "Dim", new Color(0f, 0f, 0f, 0.7f));
            _resultTitle = Label(_results.transform, "", 70f, C, new Vector2(0f, 270f), new Vector2(1000f, 100f));
            _resultTitle.fontStyle = FontStyles.Bold;
            _resultList = Label(_results.transform, "", 30f, C, new Vector2(0f, 20f), new Vector2(700f, 400f), TextAlignmentOptions.Center);
            Btn(_results.transform, "NEXT TRACK", C, new Vector2(-250f, -270f), new Vector2(270f, 70f), new Color(0.15f, 0.7f, 0.3f), NextTrack, 30f);
            Btn(_results.transform, "RETRY", C, new Vector2(40f, -270f), new Vector2(220f, 70f), new Color(0.2f, 0.45f, 0.9f), () => StartTrack(current), 30f);
            Btn(_results.transform, "MAP", C, new Vector2(270f, -270f), new Vector2(200f, 70f), new Color(0.6f, 0.25f, 0.25f), OpenIsland, 30f);
            _results.SetActive(false);
        }

        // ------------------------------------------------------------------ flow

        private void HideAll()
        {
            _title.SetActive(false); _island.SetActive(false); _pause.SetActive(false); _results.SetActive(false);
        }

        public void ShowTitle()
        {
            race.Cleanup(); cam.target = null; HideAll(); _title.SetActive(true);
            Time.timeScale = 1f;
        }

        public void CommitName()
        {
            playerName = string.IsNullOrWhiteSpace(_nameInput.text) ? "Racer" : _nameInput.text.Trim();
            PlayerPrefs.SetString("kr_name", playerName);
        }

        public void HideMenus() => HideAll();

        public void OpenIsland()
        {
            CommitName();
            race.Cleanup(); cam.target = null; HideAll(); _island.SetActive(true);
            SelectRegion(_region);
            Time.timeScale = 1f;
        }

        public void StartTrack(TrackDef def)
        {
            current = def;
            HideAll();
            Time.timeScale = 1f; _paused = false;
            race.OnPlayerFinished = ShowResults;
            race.StartRace(def, playerName, playerColor, 7);
            cam.Snap(race.player);
        }

        private void NextTrack()
        {
            int next = (current.Id + 1) % TrackDefs.All.Count;
            _region = TrackDefs.All[next].region;
            StartTrack(TrackDefs.All[next]);
        }

        private void ShowResults()
        {
            var p = race.player;
            float best = PlayerPrefs.GetFloat("kr_best_" + current.Id, 0f);
            bool record = best <= 0f || p.finishTime < best;
            if (record) { PlayerPrefs.SetFloat("kr_best_" + current.Id, p.finishTime); PlayerPrefs.Save(); }
            _resultTitle.text = p.place == 1 ? "YOU WIN!" : "YOU FINISHED " + Ordinal(p.place);
            var sb = new System.Text.StringBuilder();
            foreach (var k in race.Standings())
            {
                string time = k.finished ? TimeString(k.finishTime) : "DNF";
                string line = k.place + ".  " + k.racerName + "   " + time;
                sb.AppendLine(k.isPlayer ? "<color=#ffe15a><b>" + line + "</b></color>" : line);
            }
            if (record) sb.AppendLine("\n<color=#7dff9a>New best time!</color>");
            _resultList.text = sb.ToString();
            _results.SetActive(true);
        }

        public void TogglePause()
        {
            if (Online != null && Online.InRace) { Online.ToggleLeavePanel(); return; }
            if (race.state == RaceState.Racing || race.state == RaceState.Countdown) SetPaused(!_paused);
        }

        private void SetPaused(bool p)
        {
            _paused = p;
            Time.timeScale = p ? 0f : 1f;
            _pause.SetActive(p);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) TogglePause();
        }

        // ------------------------------------------------------------------ island art

        private static Sprite MakeIslandSprite()
        {
            const int S = 512;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color[S * S];
            var rng = new System.Random(5);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float nx = (x - S / 2f) / (S / 2f), ny = (y - S / 2f) / (S / 2f);
                    float r = Mathf.Sqrt(nx * nx + ny * ny), th = Mathf.Atan2(ny, nx);
                    float edge = 0.86f * (1f + 0.06f * Mathf.Sin(3f * th + 1f) + 0.05f * Mathf.Sin(5f * th + 2f) + 0.035f * Mathf.Sin(9f * th));
                    Color c;
                    if (r > edge)
                    {
                        float k = Mathf.Clamp01((r - edge) / 0.14f);
                        c = Color.Lerp(new Color(0.35f, 0.85f, 0.9f, 1f), new Color(0.08f, 0.42f, 0.78f, 0f), k);
                    }
                    else
                    {
                        int reg = ny > 0f ? (nx < 0f ? 0 : 1) : (nx < 0f ? 2 : 3);
                        var pal = TrackDefs.Palettes[reg];
                        float n = Mathf.PerlinNoise(x * 0.04f + reg * 10f, y * 0.04f);
                        c = Color.Lerp(pal.ground, pal.groundB, n);
                        if (reg == 1) c = Color.Lerp(c, new Color(0.5f, 0.05f, 0.45f), n * 0.6f);
                        if (reg == 2) c = Color.HSVToRGB(Mathf.Floor(x / 38f + y / 38f) * 0.13f % 1f, 0.5f, 0.95f);
                        if (reg == 3) c = Color.HSVToRGB((r * 3.2f + th * 0.15f) % 1f, 0.75f, 0.95f);
                        if (reg == 0 && rng.NextDouble() < 0.02) c = Color.white;
                        if (reg == 1 && rng.NextDouble() < 0.012) c = new Color(0.1f, 1f, 0.9f);
                        if (r > edge - 0.045f) c = Color.Lerp(c, new Color(1f, 0.93f, 0.7f), 0.75f);
                        if (Mathf.Abs(nx) < 0.012f || Mathf.Abs(ny) < 0.012f) c = Color.Lerp(c, Color.white, 0.55f);
                        if (r < 0.07f) c = new Color(1f, 0.95f, 0.8f);
                        c.a = 1f;
                    }
                    px[y * S + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
