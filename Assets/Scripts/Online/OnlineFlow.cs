using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static KartRacer.UIKit;

namespace KartRacer
{
    /// <summary>Online racing: menu (quick race / private room / join code), lobby, race sync and results.
    /// Every client drives its own kart and streams its state; the server relays and decides the final order.</summary>
    public class OnlineFlow : MonoBehaviour
    {
        // Set this to the deployed server. Players can also switch to "my own server" in the menu.
        public const string OfficialServerUrl = "wss://island-kart-server.onrender.com";

        [Serializable] public class PInfo { public int id; public string name; public int color; }
        [Serializable] public class Slot { public int id; public int slot; }
        [Serializable] public class Snap { public int id; public float x, z, h, v; public int d, b, s, sh, c, i; }
        [Serializable] public class Res { public int id; public string name; public float time; }
        [Serializable] public class Msg
        {
            public string t, msg, code, phase;
            public int id, hostId, track, by;
            public bool isPublic;
            public float countdown, x, z;
            public float next = -1f;
            public List<PInfo> players; public List<Slot> slots; public List<Snap> p; public List<Res> list;
        }

        private enum Mode { Closed, Menu, Connecting, Lobby, Race }
        private class Remote { public Kart k; public Vector3 target; public float heading, v; public int d, b, s; }

        private GameFlow _gf;
        private RaceManager _race;
        private Mode _mode = Mode.Closed;
        private GameObject _menu, _lobby, _results, _leave;
        private TMP_Text _menuStatus, _serverBtnLabel, _code, _lobbyInfo, _trackName, _trackTheme, _resultTitle, _resultList;
        private TMP_InputField _joinInput, _urlInput;
        private Button _startBtn, _prevBtn, _nextBtn;
        private readonly Image[] _swatch = new Image[8];
        private readonly TMP_Text[] _rowName = new TMP_Text[8], _rowTag = new TMP_Text[8];
        private readonly Button[] _rowKick = new Button[8];
        private readonly GameObject[] _row = new GameObject[8];

        private string _pending;
        private float _connectStart, _sendTimer, _nextIn = -1f;
        private int _myId, _hostId, _track;
        private bool _isPublic, _custom, _resultsShown;
        private string _roomCode = "";
        private List<PInfo> _players = new List<PInfo>();
        private readonly Dictionary<int, Remote> _remotes = new Dictionary<int, Remote>();
        private List<Res> _final;

        public bool InRace => _mode == Mode.Race;

        public static OnlineFlow Create(GameFlow gf)
        {
            var o = gf.gameObject.AddComponent<OnlineFlow>();
            o._gf = gf; o._race = gf.race;
            o.Build();
            return o;
        }

        // ------------------------------------------------------------------ UI

        private void Build()
        {
            _custom = PlayerPrefs.GetInt("kr_srv_custom", 0) == 1;
            var canvas = MakeCanvas("OnlineCanvas", 60, transform).transform;

            // --- menu
            _menu = Group(canvas, "OnlineMenu");
            Panel(_menu.transform, "Sky", new Color(0.12f, 0.3f, 0.65f));
            Label(_menu.transform, "ONLINE RACE", 70f, C, new Vector2(0f, 290f), new Vector2(900f, 100f)).fontStyle = FontStyles.Bold;
            _menuStatus = Label(_menu.transform, "", 26f, C, new Vector2(0f, 215f), new Vector2(1100f, 44f), TextAlignmentOptions.Center, new Color(1f, 0.95f, 0.6f));
            Btn(_menu.transform, "QUICK RACE (PUBLIC)", C, new Vector2(0f, 130f), new Vector2(520f, 84f), new Color(0.15f, 0.7f, 0.3f), () => Connect("{\"t\":\"joinpublic\",\"name\":\"" + Esc(Name()) + "\"}"), 34f);
            Btn(_menu.transform, "CREATE PRIVATE ROOM", C, new Vector2(0f, 30f), new Vector2(520f, 72f), new Color(0.2f, 0.45f, 0.9f), () => Connect("{\"t\":\"create\",\"name\":\"" + Esc(Name()) + "\"}"), 30f);
            _joinInput = InputBox(_menu.transform, "Room code", C, new Vector2(-140f, -70f), new Vector2(240f, 64f), 4);
            _joinInput.onValueChanged.AddListener(v => { var u = v.ToUpperInvariant(); if (u != v) _joinInput.text = u; });
            Btn(_menu.transform, "JOIN", C, new Vector2(120f, -70f), new Vector2(240f, 64f), new Color(0.9f, 0.55f, 0.15f), () =>
            {
                string c = _joinInput.text.Trim().ToUpperInvariant();
                if (c.Length != 4) { _menuStatus.text = "Type the 4-letter room code first."; return; }
                Connect("{\"t\":\"join\",\"code\":\"" + Esc(c) + "\",\"name\":\"" + Esc(Name()) + "\"}");
            }, 30f);
            var sb = Btn(_menu.transform, "", C, new Vector2(0f, -165f), new Vector2(520f, 56f), new Color(1f, 1f, 1f, 0.18f), ToggleServer, 24f);
            _serverBtnLabel = sb.GetComponentInChildren<TMP_Text>();
            _urlInput = InputBox(_menu.transform, "ws://192.168.1.5:8080", C, new Vector2(0f, -235f), new Vector2(520f, 56f), 80);
            _urlInput.text = PlayerPrefs.GetString("kr_srv_url", "ws://localhost:8080");
            Btn(_menu.transform, "< BACK", C, new Vector2(0f, -315f), new Vector2(240f, 56f), new Color(0.6f, 0.25f, 0.25f), CloseToTitle, 26f);
            RefreshServerUi();

            // --- lobby
            _lobby = Group(canvas, "OnlineLobby");
            Panel(_lobby.transform, "Sky", new Color(0.12f, 0.3f, 0.65f));
            _code = Label(_lobby.transform, "", 56f, C, new Vector2(0f, 300f), new Vector2(1000f, 80f));
            _code.fontStyle = FontStyles.Bold;
            var pp = Box(_lobby.transform, "Players", C, new Vector2(-310f, 10f), new Vector2(520f, 540f), new Color(0f, 0f, 0f, 0.35f));
            for (int i = 0; i < 8; i++)
            {
                int idx = i;
                float y = 218f - i * 62f;
                var row = Group(pp.transform, "Row" + i, false);
                _row[i] = row;
                _swatch[i] = Box(row.transform, "Swatch", C, new Vector2(-215f, y), new Vector2(38f, 38f), Color.white);
                _rowName[i] = Label(row.transform, "", 30f, C, new Vector2(-30f, y), new Vector2(320f, 44f), TextAlignmentOptions.Left);
                _rowTag[i] = Label(row.transform, "", 22f, C, new Vector2(120f, y), new Vector2(100f, 40f), TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
                _rowKick[i] = Btn(row.transform, "KICK", C, new Vector2(212f, y), new Vector2(84f, 42f), new Color(0.7f, 0.2f, 0.2f), () => Send("{\"t\":\"kick\",\"id\":" + RowId(idx) + "}"), 20f);
            }
            Label(_lobby.transform, "TRACK", 26f, C, new Vector2(260f, 215f), new Vector2(480f, 40f), TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.7f));
            _trackName = Label(_lobby.transform, "", 40f, C, new Vector2(260f, 160f), new Vector2(380f, 60f));
            _trackName.fontStyle = FontStyles.Bold;
            _trackTheme = Label(_lobby.transform, "", 24f, C, new Vector2(260f, 112f), new Vector2(480f, 40f), TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.8f));
            _prevBtn = Btn(_lobby.transform, "<", C, new Vector2(30f, 160f), new Vector2(64f, 60f), new Color(1f, 1f, 1f, 0.25f), () => Send("{\"t\":\"track\",\"track\":" + ((_track + 19) % 20) + "}"), 34f);
            _nextBtn = Btn(_lobby.transform, ">", C, new Vector2(490f, 160f), new Vector2(64f, 60f), new Color(1f, 1f, 1f, 0.25f), () => Send("{\"t\":\"track\",\"track\":" + ((_track + 1) % 20) + "}"), 34f);
            _lobbyInfo = Label(_lobby.transform, "", 26f, C, new Vector2(260f, 20f), new Vector2(520f, 100f), TextAlignmentOptions.Center, new Color(1f, 0.95f, 0.6f));
            _lobbyInfo.textWrappingMode = TextWrappingModes.Normal;
            _startBtn = Btn(_lobby.transform, "START RACE", C, new Vector2(260f, -90f), new Vector2(380f, 84f), new Color(0.15f, 0.7f, 0.3f), () => Send("{\"t\":\"start\"}"), 36f);
            Btn(_lobby.transform, "LEAVE", C, new Vector2(260f, -200f), new Vector2(260f, 64f), new Color(0.6f, 0.25f, 0.25f), LeaveToMenu, 30f);

            // --- results
            _results = Group(canvas, "OnlineResults");
            Panel(_results.transform, "Dim", new Color(0f, 0f, 0f, 0.72f));
            _resultTitle = Label(_results.transform, "", 64f, C, new Vector2(0f, 280f), new Vector2(1000f, 90f));
            _resultTitle.fontStyle = FontStyles.Bold;
            _resultList = Label(_results.transform, "", 30f, C, new Vector2(0f, 20f), new Vector2(800f, 420f));
            Btn(_results.transform, "BACK TO LOBBY", C, new Vector2(-170f, -280f), new Vector2(300f, 66f), new Color(0.15f, 0.7f, 0.3f), BackToLobby, 28f);
            Btn(_results.transform, "LEAVE", C, new Vector2(170f, -280f), new Vector2(240f, 66f), new Color(0.6f, 0.25f, 0.25f), LeaveToMenu, 28f);

            // --- leave confirm
            _leave = Group(canvas, "OnlineLeave");
            Panel(_leave.transform, "Dim", new Color(0f, 0f, 0f, 0.6f));
            Label(_leave.transform, "LEAVE THE RACE?", 64f, C, new Vector2(0f, 120f), new Vector2(900f, 90f)).fontStyle = FontStyles.Bold;
            Btn(_leave.transform, "KEEP RACING", C, new Vector2(0f, 0f), new Vector2(380f, 76f), new Color(0.15f, 0.7f, 0.3f), ToggleLeavePanel, 32f);
            Btn(_leave.transform, "LEAVE", C, new Vector2(0f, -100f), new Vector2(380f, 70f), new Color(0.6f, 0.25f, 0.25f), LeaveToMenu, 32f);

            HideAll();
        }

        private void HideAll() { _menu.SetActive(false); _lobby.SetActive(false); _results.SetActive(false); _leave.SetActive(false); }

        private string Name() { _gf.CommitName(); return _gf.playerName; }
        private static string Esc(string s) => (s ?? "").Replace("\\", "").Replace("\"", "");

        private int RowId(int i) => i < _players.Count ? _players[i].id : -1;

        private void ToggleServer()
        {
            _custom = !_custom;
            PlayerPrefs.SetInt("kr_srv_custom", _custom ? 1 : 0);
            RefreshServerUi();
        }

        private void RefreshServerUi()
        {
            _serverBtnLabel.text = _custom ? "Server: MY OWN  (tap to switch)" : "Server: OFFICIAL  (tap to switch)";
            _urlInput.gameObject.SetActive(_custom);
        }

        private string ServerUrl()
        {
            if (!_custom) return OfficialServerUrl;
            string u = _urlInput.text.Trim();
            if (u.Length == 0) u = "ws://localhost:8080";
            if (!u.StartsWith("ws://") && !u.StartsWith("wss://")) u = "ws://" + u;
            PlayerPrefs.SetString("kr_srv_url", u);
            return u;
        }

        // ------------------------------------------------------------------ flow

        public void Open()
        {
            _gf.CommitName();
            _gf.HideMenus();
            _menuStatus.text = "";
            ShowOnly(_menu);
            _mode = Mode.Menu;
        }

        private void ShowOnly(GameObject g) { HideAll(); g.SetActive(true); }

        private void CloseToTitle()
        {
            HideAll(); _mode = Mode.Closed;
            _gf.ShowTitle();
        }

        private void Connect(string first)
        {
            _pending = first;
            _menuStatus.text = "Connecting...";
            _connectStart = Time.unscaledTime;
            _mode = Mode.Connecting;
            _players.Clear(); _final = null; _myId = 0; _nextIn = -1f;
            GameSocket.Connect(ServerUrl());
        }

        private void Fail(string why)
        {
            GameSocket.Close();
            _pending = null;
            _race.Cleanup(); _gf.cam.target = null; _remotes.Clear();
            ShowOnly(_menu); _mode = Mode.Menu;
            _menuStatus.text = why;
        }

        public void LeaveToMenu()
        {
            Send("{\"t\":\"leave\"}");
            Fail("");
        }

        private void BackToLobby()
        {
            _race.Cleanup(); _gf.cam.target = null; _remotes.Clear();
            _mode = Mode.Lobby; ShowOnly(_lobby); RefreshLobby();
        }

        public void ToggleLeavePanel() { if (_mode == Mode.Race && !_results.activeSelf) _leave.SetActive(!_leave.activeSelf); }

        private static void Send(string s) => GameSocket.Send(s);

        private void Update()
        {
            if (_mode == Mode.Closed) return;

            if (_mode == Mode.Connecting)
            {
                float el = Time.unscaledTime - _connectStart;
                int st = GameSocket.State;
                if (st == GameSocket.Open && _pending != null) { GameSocket.Send(_pending); _pending = null; }
                if (st == GameSocket.Closed && el > 0.3f) { Fail("Could not reach the server. Check the connection or try again."); return; }
                if (el > 100f) { Fail("The server took too long to answer."); return; }
                _menuStatus.text = el > 5f ? "Waking the server up... this can take up to a minute the first time." : "Connecting...";
            }
            else if (_mode != Mode.Menu && GameSocket.State == GameSocket.Closed)
            {
                Fail("Lost the connection to the server.");
                return;
            }

            while (GameSocket.TryReceive(out string raw))
            {
                Msg m;
                try { m = JsonUtility.FromJson<Msg>(raw); } catch (Exception) { continue; }
                if (m != null && !string.IsNullOrEmpty(m.t)) Handle(m);
                if (_mode == Mode.Closed || _mode == Mode.Menu) break;
            }

            if (_mode == Mode.Lobby) RefreshLobbyInfo();
            if (_mode == Mode.Race) RaceTick();
        }

        private void Handle(Msg m)
        {
            switch (m.t)
            {
                case "joined": _myId = m.id; break;
                case "err":
                    if (_mode == Mode.Connecting) Fail(m.msg);
                    else if (_mode == Mode.Lobby) _lobbyInfo.text = m.msg;
                    break;
                case "room": OnRoom(m); break;
                case "start": OnStart(m); break;
                case "snap": OnSnap(m); break;
                case "finished":
                    if (_remotes.TryGetValue(m.id, out var rf) && rf.k != null) { rf.k.finished = true; rf.k.finishTime = _race.raceTime; }
                    break;
                case "results": OnResults(m); break;
                case "slick":
                    if (_mode == Mode.Race && _race.items != null && _race.player != null)
                    {
                        Kart owner = _remotes.TryGetValue(m.by, out var ro) ? ro.k : null;
                        _race.items.SpawnSlick(new Vector3(m.x, _race.player.Position.y - 0.55f, m.z), owner);
                    }
                    break;
                case "hit":
                    if (_mode == Mode.Race && _race.player != null && _race.player.Hit())
                    { _race.toast = "HIT BY A BOLT!"; _race.toastUntil = Time.time + 1.4f; }
                    break;
                case "kicked": Fail("You were removed from the room."); break;
            }
        }

        private void OnRoom(Msg m)
        {
            _roomCode = m.code; _hostId = m.hostId; _isPublic = m.isPublic; _track = m.track;
            _players = m.players ?? new List<PInfo>();
            if (_mode == Mode.Connecting) { _mode = Mode.Lobby; ShowOnly(_lobby); }
            if (_mode == Mode.Lobby) RefreshLobby();
            if (_mode == Mode.Race)
            {
                // drop karts of players who left
                var ids = new HashSet<int>();
                foreach (var p in _players) ids.Add(p.id);
                var gone = new List<int>();
                foreach (var kv in _remotes) if (!ids.Contains(kv.Key)) gone.Add(kv.Key);
                foreach (int id in gone) { _race.RemoveKart(_remotes[id].k); _remotes.Remove(id); }
            }
        }

        private void RefreshLobby()
        {
            _code.text = _isPublic ? "PUBLIC RACE" : "ROOM CODE:  <color=#ffe15a>" + _roomCode + "</color>";
            bool host = !_isPublic && _myId == _hostId;
            for (int i = 0; i < 8; i++)
            {
                bool has = i < _players.Count;
                _row[i].SetActive(has);
                if (!has) continue;
                var p = _players[i];
                _swatch[i].color = GameFlow.KartColors[Mathf.Abs(p.color) % GameFlow.KartColors.Length];
                _rowName[i].text = p.name + (p.id == _myId ? "  (you)" : "");
                _rowTag[i].text = (!_isPublic && p.id == _hostId) ? "HOST" : "";
                _rowKick[i].gameObject.SetActive(host && p.id != _myId);
            }
            var def = TrackDefs.Get(Mathf.Clamp(_track, 0, 19) / 5, Mathf.Clamp(_track, 0, 19) % 5);
            _trackName.text = _isPublic ? "Random each race" : def.name;
            _trackTheme.text = _isPublic ? "A new track every round" : TrackDefs.Palettes[def.region].name;
            _prevBtn.gameObject.SetActive(host); _nextBtn.gameObject.SetActive(host);
            _startBtn.gameObject.SetActive(host);
            RefreshLobbyInfo();
        }

        private void RefreshLobbyInfo()
        {
            if (_mode != Mode.Lobby) return;
            if (_isPublic)
            {
                if (_players.Count < 2) _lobbyInfo.text = "Waiting for another racer...";
                else if (_nextIn >= 0f) _lobbyInfo.text = "Next race starts in " + Mathf.CeilToInt(_nextIn) + "s";
                else _lobbyInfo.text = "Get ready...";
            }
            else if (_myId == _hostId) { if (_lobbyInfo.text.Length == 0 || _lobbyInfo.text.StartsWith("Share") || _lobbyInfo.text.StartsWith("Waiting")) _lobbyInfo.text = _players.Count < 2 ? "Share the code with a friend!" : "Pick a track and press START."; }
            else _lobbyInfo.text = "Waiting for the host to start...";
        }

        // ------------------------------------------------------------------ race

        private void OnStart(Msg m)
        {
            _final = null; _resultsShown = false;
            _results.SetActive(false); _leave.SetActive(false);
            var def = TrackDefs.Get(Mathf.Clamp(m.track, 0, 19) / 5, Mathf.Clamp(m.track, 0, 19) % 5);
            var racers = new List<RaceManager.OnlineRacer>();
            foreach (var s in m.slots)
            {
                PInfo pi = _players.Find(p => p.id == s.id);
                if (pi == null) continue;
                racers.Add(new RaceManager.OnlineRacer
                {
                    id = s.id, name = pi.name, slot = s.slot,
                    color = GameFlow.KartColors[Mathf.Abs(pi.color) % GameFlow.KartColors.Length],
                });
            }
            if (racers.Find(r => r.id == _myId) == null) return;
            HideAll();
            Time.timeScale = 1f;
            _race.OnPlayerFinished = () => ShowResults();
            _race.OnPlayerCrossed = t => Send("{\"t\":\"finish\",\"time\":" + t.ToString("F3", CultureInfo.InvariantCulture) + "}");
            _race.StartRaceOnline(def, racers, _myId, m.countdown);
            _gf.cam.Snap(_race.player);
            _remotes.Clear();
            foreach (var k in _race.karts)
                if (k.isRemote) _remotes[k.netId] = new Remote { k = k, target = k.Position, heading = k.heading };
            _race.items.OnSlickDropped = p => Send("{\"t\":\"slick\",\"x\":" + F(p.x) + ",\"z\":" + F(p.z) + "}");
            _race.items.OnBoltHitRemote = k => Send("{\"t\":\"hit\",\"target\":" + k.netId + "}");
            _mode = Mode.Race;
            _sendTimer = 0f;
        }

        private static string F(float v) => v.ToString("F2", CultureInfo.InvariantCulture);

        private void OnSnap(Msg m)
        {
            _nextIn = m.next;
            if (_mode != Mode.Race || m.p == null) return;
            foreach (var s in m.p)
            {
                if (s.id == _myId || !_remotes.TryGetValue(s.id, out var r) || r.k == null) continue;
                r.target = new Vector3(s.x, r.k.rb.position.y, s.z);
                r.heading = s.h; r.v = s.v; r.d = s.d; r.b = s.b; r.s = s.s;
                r.k.shield = s.sh != 0;
                r.k.crossings = s.c; r.k.trackIdx = Mathf.Clamp(s.i, 0, r.k.track.N - 1);
            }
        }

        private void RaceTick()
        {
            float dt = Time.deltaTime;
            var me = _race.player;
            if (me != null)
            {
                _sendTimer -= dt;
                if (_sendTimer <= 0f)
                {
                    _sendTimer = 1f / 15f;
                    Vector3 p = me.Position;
                    int d = me.drifting ? me.driftDir * (me.DriftTier + 1) : 0;
                    Send("{\"t\":\"state\",\"x\":" + F(p.x) + ",\"z\":" + F(p.z) + ",\"h\":" + F(me.heading) + ",\"v\":" + F(me.speed)
                        + ",\"d\":" + d + ",\"b\":" + (me.Boosting ? 1 : 0) + ",\"s\":" + (me.spinTimer > 0f ? 1 : 0) + ",\"sh\":" + (me.shield ? 1 : 0)
                        + ",\"c\":" + me.crossings + ",\"i\":" + me.trackIdx + "}");
                }
            }
            foreach (var r in _remotes.Values)
            {
                var k = r.k; if (k == null) continue;
                r.target += Quaternion.Euler(0f, r.heading, 0f) * Vector3.forward * (r.v * dt);
                Vector3 cur = k.rb.position;
                Vector3 np = (cur - r.target).sqrMagnitude > 900f ? r.target : Vector3.Lerp(cur, r.target, 1f - Mathf.Exp(-12f * dt));
                k.rb.position = np; k.transform.position = np;
                k.heading = Mathf.LerpAngle(k.heading, r.heading, 1f - Mathf.Exp(-12f * dt));
                k.speed = r.v;
                k.drifting = r.d != 0; k.driftDir = r.d > 0 ? 1 : -1;
                int tier = Mathf.Abs(r.d) - 1;
                k.driftCharge = tier <= 0 ? 0f : tier == 1 ? 1.0f : tier == 2 ? 1.8f : 2.7f;
                k.boostTimer = r.b != 0 ? 0.3f : 0f;
                k.spinTimer = r.s != 0 ? 0.3f : 0f;
                k.visualSpin = r.s != 0 ? k.visualSpin + 720f * dt : Mathf.MoveTowardsAngle(k.visualSpin, 0f, 900f * dt);
                k.steer = 0f;
            }
            if (_final != null && !_resultsShown) ShowResults();
        }

        private void OnResults(Msg m)
        {
            _final = m.list ?? new List<Res>();
            if (_mode == Mode.Race)
            {
                foreach (var k in _race.karts) k.raceActive = false;
                ShowResults();
            }
        }

        private void ShowResults()
        {
            if (_mode != Mode.Race) return;
            _resultsShown = true;
            _leave.SetActive(false);
            var sb = new StringBuilder();
            var me = _race.player;
            if (_final != null)
            {
                int pos = 1;
                bool won = _final.Count > 0 && _final[0].id == _myId && _final[0].time > 0f;
                _resultTitle.text = won ? "YOU WIN!" : "RACE OVER";
                foreach (var r in _final)
                {
                    string line = pos + ".  " + r.name + "   " + (r.time > 0f ? TimeString(r.time) : "DNF");
                    sb.AppendLine(r.id == _myId ? "<color=#ffe15a><b>" + line + "</b></color>" : line);
                    pos++;
                }
                if (me != null && me.finished)
                {
                    string key = "kr_best_" + _race.def.Id;
                    float best = PlayerPrefs.GetFloat(key, 0f);
                    if (best <= 0f || me.finishTime < best) { PlayerPrefs.SetFloat(key, me.finishTime); PlayerPrefs.Save(); sb.AppendLine("\n<color=#7dff9a>New best time!</color>"); }
                }
            }
            else
            {
                _resultTitle.text = me != null && me.finished ? "YOU FINISHED " + Ordinal(me.place) : "RACE";
                foreach (var k in _race.Standings())
                {
                    string line = k.place + ".  " + k.racerName + "   " + (k.finished ? TimeString(k.finishTime) : "racing...");
                    sb.AppendLine(k.isPlayer ? "<color=#ffe15a><b>" + line + "</b></color>" : line);
                }
                sb.AppendLine("\n<color=#bbbbbb>Waiting for the others to finish...</color>");
            }
            _resultList.text = sb.ToString();
            _results.SetActive(true);
        }
    }
}
