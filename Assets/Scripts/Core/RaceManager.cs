using System.Collections.Generic;
using UnityEngine;

namespace KartRacer
{
    public enum RaceState { Idle, Countdown, Racing, Finished }

    /// <summary>Owns one race: builds the track, spawns the karts on the grid, runs the countdown,
    /// laps and positions, and tells the UI when the player is done.</summary>
    public class RaceManager : MonoBehaviour
    {
        public static readonly string[] AiNames = { "Biscuit", "Zap", "Pixel", "Mochi", "Turbo Tom", "Nova", "Pepper", "Sprout", "Bolt", "Jelly", "Comet", "Waffles" };

        public Track track;
        public TrackDef def;
        public List<Kart> karts = new List<Kart>();
        public Kart player;
        public RaceState state = RaceState.Idle;
        public int totalLaps = 3;
        public float countdown, raceTime, finishedFor;
        public ItemSystem items;
        public GameObject trackRoot;
        public PlayerDriver playerDriver;
        public string toast; public float toastUntil;
        public System.Action OnPlayerFinished;
        public System.Action<float> OnPlayerCrossed;   // local player crossed the line (time)
        public class OnlineRacer { public int id; public string name; public Color color; public int slot; }
        public bool playerDone;
        private readonly List<AIDriver> _ais = new List<AIDriver>();
        private int _lastBeep = 99;
        /// <summary>0 = easy, 1 = normal, 2 = hard (saved as kr_diff).</summary>
        public static int Difficulty => PlayerPrefs.GetInt("kr_diff", 1);
        public static float DifficultyOffset => Difficulty == 0 ? -0.09f : Difficulty == 2 ? 0.05f : 0f;

        public void StartRace(TrackDef trackDef, string playerName, Color playerColor, int aiCount)
        {
            Cleanup();
            def = trackDef;
            track = Track.Create(def);
            trackRoot = TrackBuilder.Build(track);

            int total = aiCount + 1;
            int playerSlot = Mathf.Min(total - 1, 5);
            int aiIndex = 0;
            for (int slot = 0; slot < total; slot++)
            {
                int row = slot / 2, column = slot % 2;
                int idx = track.IndexBehindStart(7f + row * 8f);
                Vector3 pos = track.pts[idx] + track.right[idx] * ((column == 0 ? -1f : 1f) * track.halfWidth * 0.28f);
                float heading = Mathf.Atan2(track.tan[idx].x, track.tan[idx].z) * Mathf.Rad2Deg;
                bool isPlayer = slot == playerSlot;
                string nm = isPlayer ? playerName : AiNames[aiIndex % AiNames.Length];
                Color col = isPlayer ? playerColor : Color.HSVToRGB((aiIndex * 0.137f + 0.05f) % 1f, 0.7f, 0.95f);
                var k = Kart.Create(nm, col, isPlayer, slot, track, pos, heading);
                k.startSlot = slot;
                karts.Add(k);
                if (isPlayer)
                {
                    player = k;
                    playerDriver = k.gameObject.AddComponent<PlayerDriver>();
                    playerDriver.kart = k;
                }
                else
                {
                    var ai = k.gameObject.AddComponent<AIDriver>();
                    ai.kart = k;
                    ai.baseSkill = Mathf.Clamp(0.80f + 0.03f * def.difficulty + 0.012f * def.region + Random.Range(-0.03f, 0.03f) + DifficultyOffset, 0.66f, 1.0f);
                    ai.skill = ai.baseSkill;
                    ai.laneOffset = Random.Range(-track.halfWidth * 0.3f, track.halfWidth * 0.3f);
                    _ais.Add(ai);
                    aiIndex++;
                }
            }

            items = gameObject.AddComponent<ItemSystem>();
            items.Setup(track, karts);
            items.OnEvent = (kart, msg) => { toast = msg; toastUntil = Time.time + 1.4f; };
            foreach (var ai in _ais) ai.UseItem = items.Use;
            playerDriver.OnItemPressed = items.Use;

            state = RaceState.Countdown;
            _lastBeep = 99;
            Sfx.Music((int)def.theme);
            countdown = 3.99f;
            raceTime = 0f;
            playerDone = false;
            finishedFor = 0f;
        }

        /// <summary>Online race: karts come from the server's slot list; everyone but me is a remote proxy.</summary>
        public void StartRaceOnline(TrackDef trackDef, List<OnlineRacer> racers, int myId, float countdownSeconds)
        {
            Cleanup();
            def = trackDef;
            track = Track.Create(def);
            trackRoot = TrackBuilder.Build(track);
            foreach (var r in racers)
            {
                int slot = r.slot;
                int row = slot / 2, column = slot % 2;
                int idx = track.IndexBehindStart(7f + row * 8f);
                Vector3 pos = track.pts[idx] + track.right[idx] * ((column == 0 ? -1f : 1f) * track.halfWidth * 0.28f);
                float heading = Mathf.Atan2(track.tan[idx].x, track.tan[idx].z) * Mathf.Rad2Deg;
                bool me = r.id == myId;
                var k = Kart.Create(r.name, r.color, me, slot, track, pos, heading);
                k.startSlot = slot; k.netId = r.id;
                karts.Add(k);
                if (me)
                {
                    player = k;
                    playerDriver = k.gameObject.AddComponent<PlayerDriver>();
                    playerDriver.kart = k;
                }
                else k.SetRemote();
            }
            items = gameObject.AddComponent<ItemSystem>();
            items.Setup(track, karts);
            items.OnEvent = (kart, msg) => { toast = msg; toastUntil = Time.time + 1.4f; };
            if (playerDriver != null) playerDriver.OnItemPressed = items.Use;
            state = RaceState.Countdown;
            _lastBeep = 99;
            Sfx.Music((int)def.theme);
            countdown = Mathf.Max(0.5f, countdownSeconds);
            raceTime = 0f; playerDone = false; finishedFor = 0f;
        }

        public void RemoveKart(Kart k)
        {
            if (k == null || k == player) return;
            karts.Remove(k);
            Destroy(k.gameObject);
        }

        public void Cleanup()
        {
            foreach (var k in karts) if (k != null) Destroy(k.gameObject);
            karts.Clear(); _ais.Clear();
            if (items != null) { items.Clear(); Destroy(items); items = null; }
            if (trackRoot != null) Destroy(trackRoot);
            player = null; playerDriver = null; state = RaceState.Idle; playerDone = false;
        }

        private void Update()
        {
            if (state == RaceState.Idle) return;
            float dt = Time.deltaTime;

            if (state == RaceState.Countdown)
            {
                countdown -= dt;
                int whole = Mathf.CeilToInt(countdown);
                if (whole != _lastBeep && whole >= 1 && whole <= 3) { _lastBeep = whole; Sfx.Play("beep", 0.7f); }
                if (countdown <= 0f)
                {
                    Sfx.Play("go", 0.8f);
                    state = RaceState.Racing;
                    foreach (var k in karts) k.raceActive = true;
                    toast = "GO!"; toastUntil = Time.time + 1f;
                }
                return;
            }

            raceTime += dt;
            foreach (var k in karts) if (!k.isRemote) TrackProgress(k);
            RankKarts();

            // rubber-banding: bots ease off when far ahead of the player and push when behind
            foreach (var ai in _ais)
            {
                if (player == null) break;
                float gap = (player.Progress - ai.kart.Progress) / (track.N * 0.12f);
                ai.skill = ai.baseSkill * (1f + 0.07f * Mathf.Clamp(gap, -1f, 1f));
            }

            foreach (var k in karts)
            {
                if (!k.isRemote && !k.finished && k.crossings > totalLaps)
                {
                    k.finished = true; k.finishTime = raceTime;
                    if (k == player) { HandlePlayerFinish(); OnPlayerCrossed?.Invoke(k.finishTime); }
                }
            }

            if (playerDone)
            {
                finishedFor += dt;
                if (finishedFor > 3.5f && state != RaceState.Finished)
                {
                    state = RaceState.Finished;
                    OnPlayerFinished?.Invoke();
                }
            }
        }

        private void HandlePlayerFinish()
        {
            playerDone = true;
            Sfx.Play("finish", 0.9f);
            if (player != null) Fx.Confetti(player.Position);
            toast = "FINISH!"; toastUntil = Time.time + 3f;
            // hand the kart to a bot so it keeps driving over the line
            if (playerDriver != null) { playerDriver.enabled = false; }
            var ai = player.gameObject.AddComponent<AIDriver>();
            ai.kart = player; ai.baseSkill = ai.skill = 0.85f;
            _ais.Add(ai);
        }

        private void TrackProgress(Kart k)
        {
            int idx = track.Nearest(k.Position, k.trackIdx, 25);
            int d = idx - k.trackIdx;
            if (d < -track.N / 2) k.crossings++;
            else if (d > track.N / 2) k.crossings--;
            k.trackIdx = idx;
        }

        private void RankKarts()
        {
            var order = new List<Kart>(karts);
            order.Sort((a, b) =>
            {
                if (a.finished && b.finished) return a.finishTime.CompareTo(b.finishTime);
                if (a.finished) return -1;
                if (b.finished) return 1;
                return b.Progress.CompareTo(a.Progress);
            });
            for (int i = 0; i < order.Count; i++) order[i].place = i + 1;
        }

        public List<Kart> Standings()
        {
            RankKarts();
            var order = new List<Kart>(karts);
            order.Sort((a, b) => a.place.CompareTo(b.place));
            return order;
        }

        public bool PlayerWrongWay()
        {
            if (player == null || !player.raceActive || player.speed < 6f) return false;
            return Vector3.Dot(player.Forward, track.tan[player.trackIdx]) < -0.35f;
        }

        public int PlayerLap() => player == null ? 1 : Mathf.Clamp(player.crossings, 1, totalLaps);
    }
}
