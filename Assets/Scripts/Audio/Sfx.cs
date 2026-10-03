using System;
using System.Collections.Generic;
using UnityEngine;

namespace KartRacer
{
    /// <summary>All sound is synthesised in code (no audio files): sound effects, an engine loop and a music loop per realm.</summary>
    public static class Sfx
    {
        public const int SR = 22050;
        private static GameObject _host;
        private static AudioSource _sfx, _music;
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static int _musicId = -99;
        private static bool _mute = PlayerPrefs.GetInt("kr_mute", 0) == 1;

        public static bool Muted
        {
            get => _mute;
            set
            {
                _mute = value;
                PlayerPrefs.SetInt("kr_mute", value ? 1 : 0);
                if (_music != null) _music.mute = value;
                if (_sfx != null) _sfx.mute = value;
                AudioListener.volume = value ? 0f : 1f;
            }
        }

        private static void Ensure()
        {
            if (_host != null) return;
            _host = new GameObject("Sfx");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _sfx = _host.AddComponent<AudioSource>();
            _sfx.spatialBlend = 0f; _sfx.playOnAwake = false;
            _music = _host.AddComponent<AudioSource>();
            _music.spatialBlend = 0f; _music.loop = true; _music.playOnAwake = false; _music.volume = 0.32f;
            AudioListener.volume = _mute ? 0f : 1f;
        }

        // ------------------------------------------------------------------ playing

        public static void Play(string id, float volume = 0.8f)
        {
            if (_mute) return;
            Ensure();
            _sfx.PlayOneShot(Get(id), volume);
        }

        public static void Music(int id)
        {
            Ensure();
            if (id == _musicId && _music.isPlaying) return;
            _musicId = id;
            if (id < -1) { _music.Stop(); return; }
            _music.clip = GetMusic(id);
            _music.Play();
        }

        public static void StopMusic() { Ensure(); _music.Stop(); _musicId = -99; }

        public static AudioClip Get(string id)
        {
            if (Clips.TryGetValue(id, out var c) && c != null) return c;
            c = Build(id);
            Clips[id] = c;
            return c;
        }

        // ------------------------------------------------------------------ synthesis helpers

        private static AudioClip Make(string name, float[] data, bool loop = false)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        private static float Square(float phase, float duty = 0.5f) => (phase - Mathf.Floor(phase)) < duty ? 1f : -1f;
        private static float Tri(float phase) { float p = phase - Mathf.Floor(phase); return 4f * Mathf.Abs(p - 0.5f) - 1f; }
        private static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase)) - 1f;

        private static void Note(float[] buf, float startSec, float lenSec, float hz, float vol, int wave, float duty = 0.5f)
        {
            int s0 = (int)(startSec * SR), n = (int)(lenSec * SR);
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                if (s0 + i < 0) continue;
                float t = i / (float)SR;
                float env = Mathf.Min(1f, i / 120f) * Mathf.Exp(-3.2f * i / n);
                float ph = hz * t;
                float v = wave == 0 ? Square(ph, duty) : wave == 1 ? Tri(ph) : wave == 2 ? Saw(ph) : Mathf.Sin(ph * 2f * Mathf.PI);
                buf[s0 + i] += v * env * vol;
            }
        }

        private static AudioClip Build(string id)
        {
            switch (id)
            {
                case "beep": { var b = new float[(int)(SR * 0.25f)]; Note(b, 0, 0.25f, 440f, 0.5f, 3); return Make(id, b); }
                case "go": { var b = new float[(int)(SR * 0.5f)]; Note(b, 0, 0.5f, 880f, 0.5f, 3); Note(b, 0, 0.5f, 1320f, 0.25f, 3); return Make(id, b); }
                case "pickup": { var b = new float[(int)(SR * 0.4f)]; int[] n = { 72, 76, 79, 84 }; for (int i = 0; i < 4; i++) Note(b, i * 0.06f, 0.15f, Hz(n[i]), 0.3f, 0); return Make(id, b); }
                case "use": { var b = new float[(int)(SR * 0.3f)]; Note(b, 0, 0.12f, Hz(67), 0.35f, 0); Note(b, 0.08f, 0.2f, Hz(79), 0.35f, 0); return Make(id, b); }
                case "click": { var b = new float[(int)(SR * 0.08f)]; Note(b, 0, 0.08f, 700f, 0.4f, 0); return Make(id, b); }
                case "boost":
                {
                    var b = new float[(int)(SR * 0.7f)];
                    float ph = 0f;
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = i / (float)b.Length;
                        float f = Mathf.Lerp(200f, 900f, t * t);
                        ph += f / SR;
                        b[i] = Saw(ph) * 0.3f * Mathf.Min(1f, i / 300f) * (1f - t);
                    }
                    return Make(id, b);
                }
                case "hit":
                {
                    var b = new float[(int)(SR * 0.6f)];
                    var rng = new System.Random(3);
                    float lp = 0f, ph = 0f;
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = i / (float)b.Length;
                        lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.25f;
                        ph += Mathf.Lerp(220f, 60f, t) / SR;
                        b[i] = (lp * 1.2f + Mathf.Sin(ph * 2f * Mathf.PI) * 0.6f) * 0.5f * Mathf.Exp(-5f * t);
                    }
                    return Make(id, b);
                }
                case "finish":
                {
                    var b = new float[(int)(SR * 1.6f)];
                    int[] n = { 72, 76, 79, 84, 79, 84 }; float[] t0 = { 0f, 0.14f, 0.28f, 0.42f, 0.7f, 0.84f };
                    for (int i = 0; i < n.Length; i++) { Note(b, t0[i], 0.5f, Hz(n[i]), 0.25f, 0); Note(b, t0[i], 0.5f, Hz(n[i] - 12), 0.2f, 1); }
                    return Make(id, b);
                }
                case "engine":
                {
                    // 0.5 s loop containing whole cycles of an 80 Hz tone so it loops cleanly; pitch is changed via AudioSource.pitch
                    var b = new float[SR / 2];
                    for (int i = 0; i < b.Length; i++)
                    {
                        float ph = 80f * i / SR;
                        b[i] = (Saw(ph) * 0.35f + Square(ph * 2f, 0.3f) * 0.15f + Mathf.Sin(ph * 2f * Mathf.PI) * 0.3f) * 0.6f;
                    }
                    return Make(id, b, true);
                }
                case "drift":
                {
                    var b = new float[SR / 2];
                    var rng = new System.Random(11);
                    float lp = 0f;
                    for (int i = 0; i < b.Length; i++) { lp += ((float)rng.NextDouble() * 2f - 1f - lp) * 0.12f; b[i] = lp * 0.9f; }
                    return Make(id, b, true);
                }
            }
            return Make(id, new float[100]);
        }

        // ------------------------------------------------------------------ music

        private static AudioClip GetMusic(int theme)
        {
            string key = "music" + theme;
            if (Clips.TryGetValue(key, out var c) && c != null) return c;
            c = Compose(theme, key);
            Clips[key] = c;
            return c;
        }

        // theme: -1 menu, 0 candy, 1 corrupted, 2 colourful, 3 rainbow
        private static AudioClip Compose(int theme, string name)
        {
            int root; int[] scale; float bpm; int leadWave; float duty; int[] prog = { 0, 3, 4, 2 };
            switch (theme)
            {
                case 0: root = 72; scale = new[] { 0, 2, 4, 7, 9 }; bpm = 150f; leadWave = 0; duty = 0.5f; break;
                case 1: root = 57; scale = new[] { 0, 1, 3, 5, 7, 8, 10 }; bpm = 132f; leadWave = 0; duty = 0.25f; prog = new[] { 0, 1, 0, 4 }; break;
                case 2: root = 69; scale = new[] { 0, 2, 4, 5, 7, 9, 11 }; bpm = 142f; leadWave = 0; duty = 0.4f; break;
                case 3: root = 72; scale = new[] { 0, 2, 4, 6, 7, 9, 11 }; bpm = 160f; leadWave = 2; duty = 0.5f; break;
                default: root = 67; scale = new[] { 0, 2, 4, 7, 9 }; bpm = 108f; leadWave = 1; duty = 0.5f; break;
            }
            const int steps = 64;
            float step = 60f / bpm / 2f;                       // eighth notes
            var buf = new float[(int)(steps * step * SR)];
            var rng = new System.Random(100 + theme * 17);
            int deg = 0;
            for (int s = 0; s < steps; s++)
            {
                int bar = s / 16;
                int chord = prog[bar % prog.Length];
                // lead melody: random walk on the scale
                if (rng.NextDouble() > 0.28)
                {
                    deg += rng.Next(-2, 3);
                    deg = Mathf.Clamp(deg, -2, scale.Length + 2);
                    int idx = ((deg % scale.Length) + scale.Length) % scale.Length;
                    int oct = Mathf.FloorToInt(deg / (float)scale.Length);
                    int midi = root + scale[idx] + 12 * oct;
                    Note(buf, s * step, step * 1.6f, Hz(midi), 0.16f, leadWave, duty);
                }
                // bass on every quarter note
                if (s % 2 == 0)
                {
                    int bidx = chord % scale.Length;
                    Note(buf, s * step, step * 1.8f, Hz(root - 24 + scale[bidx]), 0.34f, 1);
                }
                // arpeggio for rainbow / colourful
                if ((theme == 3 || theme == 2) && s % 2 == 1)
                {
                    int aidx = (chord + (s / 2) % 3 * 2) % scale.Length;
                    Note(buf, s * step, step * 0.9f, Hz(root + 12 + scale[aidx]), 0.07f, 0, 0.25f);
                }
                // drums: kick on beats, hat on offbeats
                if (s % 4 == 0) Kick(buf, s * step);
                if (s % 2 == 1) Hat(buf, s * step, rng);
                if (s % 8 == 4 && theme != -1) Hat(buf, s * step + step * 0.5f, rng);
            }
            // corrupted: bit-crush for a glitchy sound
            if (theme == 1) for (int i = 0; i < buf.Length; i++) buf[i] = Mathf.Round(buf[i] * 10f) / 10f;
            // soft clip
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)Math.Tanh(buf[i] * 1.2f) * 0.8f;
            return Make(name, buf, true);
        }

        private static void Kick(float[] buf, float startSec)
        {
            int s0 = (int)(startSec * SR), n = (int)(0.14f * SR);
            float ph = 0f;
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = i / (float)n;
                ph += Mathf.Lerp(130f, 42f, t) / SR;
                buf[s0 + i] += Mathf.Sin(ph * 2f * Mathf.PI) * 0.55f * (1f - t);
            }
        }

        private static void Hat(float[] buf, float startSec, System.Random rng)
        {
            int s0 = (int)(startSec * SR), n = (int)(0.035f * SR);
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
                buf[s0 + i] += ((float)rng.NextDouble() * 2f - 1f) * 0.12f * (1f - i / (float)n);
        }
    }

    /// <summary>Engine and drift loops that follow one kart (the local player).</summary>
    public class KartAudio : MonoBehaviour
    {
        public Kart kart;
        private AudioSource _engine, _drift;

        private void Start()
        {
            _engine = gameObject.AddComponent<AudioSource>();
            _engine.clip = Sfx.Get("engine"); _engine.loop = true; _engine.spatialBlend = 0f; _engine.volume = 0.16f; _engine.Play();
            _drift = gameObject.AddComponent<AudioSource>();
            _drift.clip = Sfx.Get("drift"); _drift.loop = true; _drift.spatialBlend = 0f; _drift.volume = 0f; _drift.Play();
        }

        private void Update()
        {
            if (kart == null) return;
            float spd = Mathf.Clamp01(Mathf.Abs(kart.speed) / (kart.maxSpeed * 1.4f));
            bool run = kart.raceActive;
            _engine.pitch = Mathf.Lerp(0.7f, 2.2f, spd) * (kart.Boosting ? 1.15f : 1f);
            _engine.volume = Mathf.Lerp(_engine.volume, run ? 0.12f + 0.08f * spd : 0.06f, 8f * Time.deltaTime);
            _drift.volume = Mathf.Lerp(_drift.volume, kart.drifting ? 0.18f : 0f, 12f * Time.deltaTime);
        }
    }
}
