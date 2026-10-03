using System.Collections.Generic;
using UnityEngine;

namespace KartRacer
{
    /// <summary>Runtime track: a closed centre line sampled at N points, plus helpers to find where a kart is on it.</summary>
    public class Track
    {
        public TrackDef def;
        public int N = 260;
        public Vector3[] pts, tan, right;
        public float[] cum;
        public float length, halfWidth;
        public const float WallOffset = 12f; // distance of the outer fence from the road edge
        public readonly List<Vector3> boxPositions = new List<Vector3>();
        public readonly List<Vector4> pads = new List<Vector4>(); // x,y,z = pos, w = heading degrees

        public static Track Create(TrackDef d)
        {
            var t = new Track { def = d, halfWidth = d.width * 0.5f };
            var rng = new System.Random(d.seed);
            int harmonics = 2 + (d.difficulty + 1) / 2;
            var kk = new int[harmonics];
            var amp = new float[harmonics];
            var ph = new float[harmonics];
            for (int i = 0; i < harmonics; i++)
            {
                kk[i] = 2 + i + rng.Next(0, 2);
                amp[i] = (0.20f + 0.04f * d.difficulty) / (1f + 0.35f * i);
                ph[i] = (float)(rng.NextDouble() * Mathf.PI * 2f);
            }

            float scale = 1f;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                t.Sample(d, kk, amp, ph, scale);
                if (t.MinTurnRadius() >= t.halfWidth * 2.4f) break;
                scale *= 0.88f;
            }
            t.Finish();
            t.PlaceFeatures(rng);
            return t;
        }

        private void Sample(TrackDef d, int[] kk, float[] amp, float[] ph, float scale)
        {
            pts = new Vector3[N];
            for (int i = 0; i < N; i++)
            {
                float th = i / (float)N * Mathf.PI * 2f;
                float s = 0f;
                for (int h = 0; h < kk.Length; h++) s += amp[h] * Mathf.Cos(kk[h] * th + ph[h]);
                float r = d.radius * (1f + scale * s);
                pts[i] = new Vector3(Mathf.Cos(th) * r * d.stretch, 0f, Mathf.Sin(th) * r);
            }
        }

        private float MinTurnRadius()
        {
            float min = float.MaxValue;
            for (int i = 0; i < N; i++)
            {
                Vector3 p0 = pts[(i + N - 1) % N], p1 = pts[i], p2 = pts[(i + 1) % N];
                float a = (p1 - p0).magnitude, b = (p2 - p1).magnitude, c = (p2 - p0).magnitude;
                float cross = Vector3.Cross(p1 - p0, p2 - p1).magnitude;
                if (cross < 0.0001f) continue;
                float r = a * b * c / (2f * cross);
                if (r < min) min = r;
            }
            return min;
        }

        private void Finish()
        {
            tan = new Vector3[N];
            right = new Vector3[N];
            cum = new float[N + 1];
            for (int i = 0; i < N; i++)
            {
                Vector3 d = pts[(i + 1) % N] - pts[(i + N - 1) % N];
                d.y = 0f;
                tan[i] = d.normalized;
                right[i] = Vector3.Cross(Vector3.up, tan[i]).normalized;
                cum[i + 1] = cum[i] + (pts[(i + 1) % N] - pts[i]).magnitude;
            }
            length = cum[N];
        }

        private void PlaceFeatures(System.Random rng)
        {
            // item box rows
            int rows = 5 + def.difficulty / 2;
            for (int r = 0; r < rows; r++)
            {
                int idx = (int)((r + 0.5f) / rows * N) + 8;
                for (int c = -1; c <= 1; c++)
                    boxPositions.Add(pts[idx % N] + right[idx % N] * (c * halfWidth * 0.34f) + Vector3.up * 1.1f);
            }
            // boost pads, offset from the boxes
            int padCount = 4 + def.difficulty;
            for (int p = 0; p < padCount; p++)
            {
                int idx = (int)((p + 0.15f) / padCount * N) + 25;
                float side = (p % 3 - 1) * halfWidth * 0.45f;
                Vector3 pos = pts[idx % N] + right[idx % N] * side + Vector3.up * 0.06f;
                float heading = Mathf.Atan2(tan[idx % N].x, tan[idx % N].z) * Mathf.Rad2Deg;
                pads.Add(new Vector4(pos.x, pos.y, pos.z, heading));
            }
        }

        public int Nearest(Vector3 p, int hint, int window)
        {
            int best = hint;
            float bd = float.MaxValue;
            for (int j = -window; j <= window; j++)
            {
                int i = ((hint + j) % N + N) % N;
                float dx = p.x - pts[i].x, dz = p.z - pts[i].z;
                float d = dx * dx + dz * dz;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public int NearestGlobal(Vector3 p) => Nearest(p, 0, N / 2);

        public float Lateral(Vector3 p, int idx) => Vector3.Dot(p - pts[idx], right[idx]);

        public int Wrap(int i) => ((i % N) + N) % N;

        /// <summary>Index that is roughly `metres` behind the start line.</summary>
        public int IndexBehindStart(float metres)
        {
            float target = length - metres;
            for (int i = N - 1; i >= 0; i--) if (cum[i] <= target) return i;
            return 0;
        }
    }
}
