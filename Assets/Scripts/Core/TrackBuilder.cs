using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace KartRacer
{
    /// <summary>Spins / bobs a decoration so the corrupted and colourful worlds feel alive.</summary>
    public class Wobble : MonoBehaviour
    {
        public Vector3 spin;
        public float bobHeight, bobSpeed = 1f;
        private Vector3 _base; private float _t;
        private void Start() { _base = transform.position; _t = Random.value * 10f; }
        private void Update()
        {
            _t += Time.deltaTime * bobSpeed;
            if (spin != Vector3.zero) transform.Rotate(spin * Time.deltaTime, Space.Self);
            if (bobHeight > 0f) transform.position = _base + Vector3.up * Mathf.Sin(_t) * bobHeight;
        }
    }

    public static class TrackBuilder
    {
        public static GameObject Build(Track t)
        {
            var pal = TrackDefs.Palettes[(int)t.def.theme];
            var root = new GameObject("Track_" + t.def.name);
            ApplyEnvironment(pal);

            // ground (with collider) --------------------------------------------------
            var groundMat = Mats.LitTex(GroundTex(pal), new Vector2(260f, 260f), Color.white, 0.05f);
            var ground = Mats.Prim(PrimitiveType.Cube, root.transform, new Vector3(0f, -0.5f, 0f), new Vector3(5200f, 1f, 5200f), groundMat, true);
            ground.name = "Ground";
            var pm = new PhysicsMaterial { dynamicFriction = 0.2f, staticFriction = 0.2f, bounciness = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
            ground.GetComponent<Collider>().material = pm;

            // road + curbs --------------------------------------------------------------
            AddMesh(root.transform, "Road", Ribbon(t, -t.halfWidth, t.halfWidth, 0.04f, 1f / 12f, true),
                Mats.LitTex(RoadTex(t.def.theme, pal), Vector2.one, Color.white, 0.2f));
            var curbMat = Mats.LitTex(CurbTex(pal), Vector2.one, Color.white, 0.2f);
            AddMesh(root.transform, "CurbL", Ribbon(t, -t.halfWidth - 1.4f, -t.halfWidth, 0.06f, 1f / 2.8f, false), curbMat);
            AddMesh(root.transform, "CurbR", Ribbon(t, t.halfWidth, t.halfWidth + 1.4f, 0.06f, 1f / 2.8f, false), curbMat);

            // outer fence with colliders ------------------------------------------------
            var fenceMat = Mats.Lit(pal.fence, 0.4f);
            float off = t.halfWidth + Track.WallOffset;
            var fenceRoot = new GameObject("Fence");
            fenceRoot.transform.SetParent(root.transform, false);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < t.N; i += 3)
                {
                    Vector3 a = t.pts[i] + t.right[i] * (off * side);
                    Vector3 b = t.pts[t.Wrap(i + 3)] + t.right[t.Wrap(i + 3)] * (off * side);
                    Vector3 mid = (a + b) * 0.5f + Vector3.up * 1.2f;
                    float len = (b - a).magnitude + 1.2f;
                    var w = Mats.Prim(PrimitiveType.Cube, fenceRoot.transform, mid, new Vector3(1f, 2.4f, len), fenceMat, true);
                    w.transform.rotation = Quaternion.LookRotation((b - a).normalized, Vector3.up);
                }
            }

            // start / finish ------------------------------------------------------------
            BuildStartLine(t, root.transform, pal);

            // boost pads ----------------------------------------------------------------
            var padMat = Mats.LitTex(PadTex(pal), Vector2.one, Color.white, 0.1f);
            foreach (var p in t.pads)
            {
                var pad = Mats.Prim(PrimitiveType.Cube, root.transform, new Vector3(p.x, p.y, p.z), new Vector3(t.halfWidth * 0.5f, 0.05f, 7f), padMat);
                pad.transform.rotation = Quaternion.Euler(0f, p.w, 0f);
            }

            BuildDecor(t, root.transform, pal);
            return root;
        }

        // ------------------------------------------------------------------ environment

        private static void ApplyEnvironment(ThemePalette pal)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = pal.fog;
            RenderSettings.fogDensity = pal.fogDensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = pal.ambient;

            var existing = GameObject.Find("Sun");
            var sunGo = existing != null ? existing : new GameObject("Sun");
            var light = sunGo.GetComponent<Light>();
            if (light == null) light = sunGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = pal.sun;
            light.intensity = 1.15f;
            light.shadows = LightShadows.None;
            sunGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = pal.sky; }
        }

        // ------------------------------------------------------------------ meshes

        private static Mesh Ribbon(Track t, float offA, float offB, float y, float vScale, bool roadUv)
        {
            int n = t.N + 1;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new int[t.N * 6];
            for (int i = 0; i < n; i++)
            {
                int k = i % t.N;
                verts[i * 2] = t.pts[k] + t.right[k] * offA + Vector3.up * y;
                verts[i * 2 + 1] = t.pts[k] + t.right[k] * offB + Vector3.up * y;
                float v = t.cum[i] * vScale;
                uvs[i * 2] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
            }
            for (int i = 0; i < t.N; i++)
            {
                int a = i * 2, b = i * 2 + 1, c = i * 2 + 2, d = i * 2 + 3;
                tris[i * 6] = a; tris[i * 6 + 1] = c; tris[i * 6 + 2] = b;
                tris[i * 6 + 3] = b; tris[i * 6 + 4] = c; tris[i * 6 + 5] = d;
            }
            var m = new Mesh { name = "Ribbon" };
            m.vertices = verts; m.uv = uvs; m.triangles = tris;
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        private static void AddMesh(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ textures

        private static Texture2D NewTex(int w, int h, bool point = false)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            tex.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D GroundTex(ThemePalette pal)
        {
            var tex = NewTex(64, 64);
            var rng = new System.Random(7);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float n = (float)rng.NextDouble();
                    bool patch = ((x / 16) + (y / 16)) % 2 == 0;
                    Color c = Color.Lerp(pal.ground, pal.groundB, patch ? 0.15f + n * 0.25f : 0.55f + n * 0.3f);
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D RoadTex(Theme theme, ThemePalette pal)
        {
            var tex = NewTex(64, 64);
            var rng = new System.Random(11);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float u = x / 63f, v = y / 63f;
                    Color c = pal.road;
                    float n = (float)rng.NextDouble() * 0.06f;
                    c = new Color(c.r + n, c.g + n, c.b + n, 1f);
                    if (theme == Theme.Rainbow)
                    {
                        float band = Mathf.Floor(u * 7f) / 7f;
                        c = Color.HSVToRGB(band * 0.85f, 0.85f, 0.95f);
                        if (u * 7f % 1f < 0.06f) c = Color.white;
                    }
                    else
                    {
                        bool edge = u < 0.04f || u > 0.96f;
                        bool dash = Mathf.Abs(u - 0.5f) < 0.018f && v < 0.5f;
                        if (edge || dash) c = theme == Theme.Corrupted ? new Color(1f, 0.1f, 0.6f) : Color.white;
                        if (theme == Theme.Colorful && rng.NextDouble() < 0.04) c = Color.HSVToRGB((float)rng.NextDouble(), 0.8f, 1f);
                        if (theme == Theme.Candy && rng.NextDouble() < 0.03) c = Color.HSVToRGB((float)rng.NextDouble(), 0.5f, 1f);
                        if (theme == Theme.Corrupted && rng.NextDouble() < 0.02) c = new Color(0.1f, 1f, 0.9f);
                    }
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D CurbTex(ThemePalette pal)
        {
            var tex = NewTex(2, 4, true);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 2; x++) tex.SetPixel(x, y, y < 2 ? pal.curbA : pal.curbB);
            tex.Apply();
            return tex;
        }

        private static Texture2D PadTex(ThemePalette pal)
        {
            var tex = NewTex(16, 16);
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dist = Mathf.Abs(x - 7.5f);
                    bool on = Mathf.Abs(((y + dist * 1.2f) % 8f) - 4f) < 1.6f;
                    tex.SetPixel(x, y, on ? new Color(1f, 0.95f, 0.2f) : new Color(0.1f, 0.7f, 1f));
                }
            tex.Apply();
            return tex;
        }

        private static Texture2D CheckerTex()
        {
            var tex = NewTex(8, 2, true);
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 8; x++) tex.SetPixel(x, y, (x + y) % 2 == 0 ? Color.white : new Color(0.05f, 0.05f, 0.05f));
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ start line

        private static void BuildStartLine(Track t, Transform parent, ThemePalette pal)
        {
            Vector3 p = t.pts[0];
            Quaternion rot = Quaternion.LookRotation(t.tan[0], Vector3.up);
            var line = Mats.Prim(PrimitiveType.Cube, parent, p + Vector3.up * 0.07f, new Vector3(t.halfWidth * 2f, 0.04f, 3f), Mats.LitTex(CheckerTex(), Vector2.one, Color.white, 0.1f));
            line.transform.rotation = rot;

            var archMat = Mats.Lit(pal.accent, 0.4f);
            for (int s = -1; s <= 1; s += 2)
            {
                var pillar = Mats.Prim(PrimitiveType.Cube, parent, p + t.right[0] * (s * (t.halfWidth + 2f)) + Vector3.up * 4f, new Vector3(1.2f, 8f, 1.2f), archMat);
                pillar.transform.rotation = rot;
            }
            var beam = Mats.Prim(PrimitiveType.Cube, parent, p + Vector3.up * 8.3f, new Vector3((t.halfWidth + 2f) * 2f + 1.2f, 1.6f, 1.2f), archMat);
            beam.transform.rotation = rot;

            var labelGo = new GameObject("StartLabel");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.position = p + Vector3.up * 8.3f - t.tan[0] * 0.7f;
            labelGo.transform.rotation = Quaternion.LookRotation(t.tan[0], Vector3.up);
            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text = "START / FINISH";
            tmp.fontSize = 8f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(40f, 3f);
        }

        // ------------------------------------------------------------------ scenery

        private static Color Pastel(System.Random r, float s = 0.45f, float v = 1f) => Color.HSVToRGB((float)r.NextDouble(), s, v);

        private static void BuildDecor(Track t, Transform parent, ThemePalette pal)
        {
            var staticRoot = new GameObject("Decor");
            staticRoot.transform.SetParent(parent, false);
            var animRoot = new GameObject("Animated");
            animRoot.transform.SetParent(parent, false);
            var rng = new System.Random(t.def.seed * 3 + 5);
            float near = t.halfWidth + 3.5f;
            int count = 150;

            for (int n = 0; n < count; n++)
            {
                int i = rng.Next(0, t.N);
                float side = rng.Next(0, 2) == 0 ? -1f : 1f;
                float lat = near + (float)rng.NextDouble() * (Track.WallOffset - 2f);
                if (rng.NextDouble() < 0.4) lat = near + Track.WallOffset + 3f + (float)rng.NextDouble() * 50f; // beyond the fence
                Vector3 pos = t.pts[i] + t.right[i] * (side * lat);
                switch (t.def.theme)
                {
                    case Theme.Candy: CandyProp(rng, staticRoot.transform, pos); break;
                    case Theme.Corrupted: CorruptProp(rng, staticRoot.transform, animRoot.transform, pos); break;
                    case Theme.Colorful: ColorProp(rng, staticRoot.transform, animRoot.transform, pos); break;
                    default: RainbowProp(rng, staticRoot.transform, animRoot.transform, pos); break;
                }
            }

            if (t.def.theme == Theme.Rainbow)
            {
                for (int k = 0; k < 7; k++) RainbowArch(t, animRoot.transform, (int)(k / 7f * t.N) + 12);
                var starMat = Mats.Unlit(Color.white);
                for (int s = 0; s < 120; s++)
                {
                    Vector3 sp = new Vector3(Random.Range(-500f, 500f), Random.Range(25f, 140f), Random.Range(-500f, 500f));
                    Mats.Prim(PrimitiveType.Sphere, staticRoot.transform, sp, Vector3.one * Random.Range(0.6f, 2f), starMat);
                }
            }
            if (t.def.theme == Theme.Corrupted)
            {
                for (int g = 0; g < 14; g++) GlitchTower(t, staticRoot.transform, rng, t.Wrap((int)(g / 14f * t.N) + 5));
            }

            StaticBatchingUtility.Combine(staticRoot);
        }

        private static void CandyProp(System.Random r, Transform p, Vector3 pos)
        {
            switch (r.Next(0, 4))
            {
                case 0: // lollipop
                    Mats.Prim(PrimitiveType.Cylinder, p, pos + Vector3.up * 2.5f, new Vector3(0.35f, 2.5f, 0.35f), Mats.Lit(Color.white));
                    var disc = Mats.Prim(PrimitiveType.Cylinder, p, pos + Vector3.up * 5.4f, new Vector3(3.6f, 0.25f, 3.6f), Mats.Lit(Pastel(r, 0.6f)));
                    disc.transform.rotation = Quaternion.Euler(90f, r.Next(0, 360), 0f);
                    break;
                case 1: // gumdrop
                    Mats.Prim(PrimitiveType.Sphere, p, pos + Vector3.up * 0.6f, new Vector3(3f, 2.2f, 3f), Mats.Lit(Pastel(r, 0.65f), 0.7f));
                    break;
                case 2: // candy cane
                    Mats.Prim(PrimitiveType.Cylinder, p, pos + Vector3.up * 2f, new Vector3(0.6f, 2f, 0.6f), Mats.Lit(Color.white));
                    Mats.Prim(PrimitiveType.Cylinder, p, pos + Vector3.up * 1f, new Vector3(0.62f, 0.5f, 0.62f), Mats.Lit(new Color(1f, 0.2f, 0.3f)));
                    Mats.Prim(PrimitiveType.Cylinder, p, pos + Vector3.up * 3f, new Vector3(0.62f, 0.5f, 0.62f), Mats.Lit(new Color(1f, 0.2f, 0.3f)));
                    var hook = Mats.Prim(PrimitiveType.Cylinder, p, pos + new Vector3(0.7f, 4.2f, 0f), new Vector3(0.6f, 0.9f, 0.6f), Mats.Lit(new Color(1f, 0.2f, 0.3f)));
                    hook.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                default: // chocolate tree
                    Mats.Prim(PrimitiveType.Cylinder, p, pos + Vector3.up * 1.6f, new Vector3(0.8f, 1.6f, 0.8f), Mats.Lit(new Color(0.35f, 0.2f, 0.12f)));
                    Mats.Prim(PrimitiveType.Sphere, p, pos + Vector3.up * 4.2f, new Vector3(3.6f, 3.2f, 3.6f), Mats.Lit(new Color(0.6f, 1f, 0.8f), 0.6f));
                    break;
            }
        }

        private static void CorruptProp(System.Random r, Transform p, Transform anim, Vector3 pos)
        {
            switch (r.Next(0, 3))
            {
                case 0:
                    float h = 6f + (float)r.NextDouble() * 14f;
                    var mono = Mats.Prim(PrimitiveType.Cube, p, pos + Vector3.up * h * 0.5f, new Vector3(2f + (float)r.NextDouble() * 2f, h, 2f), Mats.Lit(new Color(0.07f, 0.04f, 0.1f), 0.7f));
                    mono.transform.rotation = Quaternion.Euler(0f, r.Next(0, 360), r.Next(-8, 8));
                    Mats.Prim(PrimitiveType.Cube, p, pos + Vector3.up * h, new Vector3(2.2f, 0.25f, 2.2f), Mats.Unlit(r.Next(0, 2) == 0 ? new Color(1f, 0.1f, 0.6f) : new Color(0.1f, 1f, 0.9f)));
                    break;
                case 1:
                    var cube = Mats.Prim(PrimitiveType.Cube, anim, pos + Vector3.up * (3f + (float)r.NextDouble() * 6f), Vector3.one * (1f + (float)r.NextDouble() * 2.2f), Mats.Unlit(Color.HSVToRGB(0.75f + (float)r.NextDouble() * 0.25f, 0.9f, 1f)));
                    var w = cube.AddComponent<Wobble>(); w.spin = new Vector3(40f, 70f, 20f); w.bobHeight = 1f;
                    break;
                default:
                    var spike = Mats.Prim(PrimitiveType.Cube, p, pos + Vector3.up * 2f, new Vector3(0.6f, 4f, 0.6f), Mats.Lit(new Color(0.8f, 0.05f, 0.2f)));
                    spike.transform.rotation = Quaternion.Euler(r.Next(-20, 20), r.Next(0, 360), r.Next(-20, 20));
                    break;
            }
        }

        private static void GlitchTower(Track t, Transform p, System.Random r, int i)
        {
            Vector3 pos = t.pts[i] + t.right[i] * ((r.Next(0, 2) == 0 ? -1 : 1) * (t.halfWidth + 24f));
            for (int k = 0; k < 8; k++)
            {
                var c = Mats.Prim(PrimitiveType.Cube, p, pos + new Vector3((float)r.NextDouble() * 4f - 2f, k * 2.2f, (float)r.NextDouble() * 4f - 2f),
                    new Vector3(3f, 1.6f, 3f), Mats.Unlit(k % 2 == 0 ? new Color(1f, 0.1f, 0.6f) : new Color(0.1f, 1f, 0.9f)));
                c.transform.rotation = Quaternion.Euler(0f, r.Next(0, 90), 0f);
            }
        }

        private static void ColorProp(System.Random r, Transform p, Transform anim, Vector3 pos)
        {
            switch (r.Next(0, 4))
            {
                case 0: // balloon
                    Mats.Prim(PrimitiveType.Cylinder, anim, pos + Vector3.up * 2f, new Vector3(0.08f, 2f, 0.08f), Mats.Lit(Color.white));
                    var b = Mats.Prim(PrimitiveType.Sphere, anim, pos + Vector3.up * 5f, new Vector3(2.4f, 3f, 2.4f), Mats.Lit(Pastel(r, 0.85f), 0.8f));
                    b.AddComponent<Wobble>().bobHeight = 0.6f;
                    break;
                case 1: // giant crayon
                    var cr = Mats.Prim(PrimitiveType.Cylinder, p, pos + Vector3.up * 3f, new Vector3(1.4f, 3f, 1.4f), Mats.Lit(Pastel(r, 0.9f)));
                    cr.transform.rotation = Quaternion.Euler(r.Next(-12, 12), 0f, r.Next(-12, 12));
                    Mats.Prim(PrimitiveType.Sphere, p, pos + Vector3.up * 6.2f, new Vector3(1.4f, 1.1f, 1.4f), Mats.Lit(Pastel(r, 0.9f)));
                    break;
                case 2: // paint puddle
                    var pud = Mats.Prim(PrimitiveType.Cylinder, p, pos + Vector3.up * 0.03f, new Vector3(4f + (float)r.NextDouble() * 4f, 0.03f, 4f + (float)r.NextDouble() * 4f), Mats.Lit(Pastel(r, 0.8f), 0.9f));
                    break;
                default: // tent
                    var tent = Mats.Prim(PrimitiveType.Cube, p, pos + Vector3.up * 1.6f, new Vector3(3.4f, 3.2f, 3.4f), Mats.Lit(Pastel(r, 0.7f)));
                    tent.transform.rotation = Quaternion.Euler(0f, r.Next(0, 90), 0f);
                    var roof = Mats.Prim(PrimitiveType.Cube, p, pos + Vector3.up * 3.7f, new Vector3(2.6f, 1.6f, 2.6f), Mats.Lit(Pastel(r, 0.9f)));
                    roof.transform.rotation = Quaternion.Euler(0f, 45f + r.Next(0, 90), 0f);
                    break;
            }
        }

        private static void RainbowProp(System.Random r, Transform p, Transform anim, Vector3 pos)
        {
            if (r.Next(0, 2) == 0)
            {
                float h = 4f + (float)r.NextDouble() * 12f;
                Mats.Prim(PrimitiveType.Cylinder, p, pos + Vector3.up * h * 0.5f, new Vector3(0.7f, h * 0.5f, 0.7f), Mats.Unlit(Color.HSVToRGB((float)r.NextDouble(), 0.7f, 1f)));
            }
            else
            {
                var orb = Mats.Prim(PrimitiveType.Sphere, anim, pos + Vector3.up * (4f + (float)r.NextDouble() * 10f), Vector3.one * (1.2f + (float)r.NextDouble() * 1.8f), Mats.Unlit(Color.HSVToRGB((float)r.NextDouble(), 0.5f, 1f)));
                var w = orb.AddComponent<Wobble>(); w.bobHeight = 1.2f; w.bobSpeed = 0.7f + (float)r.NextDouble();
            }
        }

        private static void RainbowArch(Track t, Transform p, int idx)
        {
            idx = t.Wrap(idx);
            Vector3 c = t.pts[idx];
            float span = t.halfWidth + 6f;
            for (int band = 0; band < 7; band++)
            {
                Color col = Color.HSVToRGB(band / 7f * 0.85f, 0.85f, 1f);
                for (int s = 0; s <= 16; s++)
                {
                    float a = s / 16f * Mathf.PI;
                    Vector3 pos = c + t.right[idx] * (Mathf.Cos(a) * (span - band * 0.9f)) + Vector3.up * (Mathf.Sin(a) * (span - band * 0.9f) * 0.8f);
                    var seg = Mats.Prim(PrimitiveType.Cube, p, pos, new Vector3(0.9f, 0.9f, 1.4f), Mats.Unlit(col));
                    seg.transform.rotation = Quaternion.LookRotation(t.tan[idx], Vector3.up) * Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg + 90f);
                }
            }
        }
    }
}
