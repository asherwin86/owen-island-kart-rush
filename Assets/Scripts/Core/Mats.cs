using UnityEngine;

namespace KartRacer
{
    /// <summary>Material helpers. Base materials live in Resources so WebGL builds keep their shaders.</summary>
    public static class Mats
    {
        private static Material _lit, _unlit;
        private static readonly System.Collections.Generic.Dictionary<long, Material> Cache = new System.Collections.Generic.Dictionary<long, Material>();

        private static long Key(Color c, float s, int kind)
        {
            long r = Mathf.RoundToInt(c.r * 31f), g = Mathf.RoundToInt(c.g * 31f), b = Mathf.RoundToInt(c.b * 31f), sm = Mathf.RoundToInt(s * 7f);
            return (((r * 32 + g) * 32 + b) * 8 + sm) * 4 + kind;
        }

        public static Material Lit(Color c, float smoothness = 0.25f)
        {
            long key = Key(c, smoothness, 0);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var made = MakeLit(c, smoothness);
            Cache[key] = made;
            return made;
        }

        private static Material MakeLit(Color c, float smoothness)
        {
            if (_lit == null) _lit = Resources.Load<Material>("KartLit");
            var m = _lit != null ? new Material(_lit) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        public static Material Unlit(Color c)
        {
            long key = Key(c, 0f, 1);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var made = MakeUnlit(c);
            Cache[key] = made;
            return made;
        }

        public static Material UnlitUnique(Color c) => MakeUnlit(c);

        private static Material MakeUnlit(Color c)
        {
            if (_unlit == null) _unlit = Resources.Load<Material>("KartUnlit");
            var m = _unlit != null ? new Material(_unlit) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.color = c;
            return m;
        }

        public static Material LitTex(Texture2D tex, Vector2 tiling, Color tint, float smoothness = 0.15f)
        {
            var m = MakeLit(tint, smoothness);
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
            return m;
        }

        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!collider) { var c = go.GetComponent<Collider>(); if (c != null) Object.Destroy(c); }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
