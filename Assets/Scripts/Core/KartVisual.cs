using TMPro;
using UnityEngine;

namespace KartRacer
{
    /// <summary>Builds a cartoon kart + driver out of primitives and animates wheels, drift sparks,
    /// boost flames, shield and the name tag.</summary>
    public class KartVisual : MonoBehaviour
    {
        private Transform[] _wheels = new Transform[4];
        private Renderer[] _sparks = new Renderer[2];
        private Transform _flame;
        private GameObject _shield;
        private Transform _label;
        private Material[] _tierMats;

        public static KartVisual Build(Kart k)
        {
            var root = k.model.gameObject;
            var v = root.AddComponent<KartVisual>();
            var body = Mats.Lit(k.color, 0.6f);
            var dark = Mats.Lit(new Color(0.08f, 0.08f, 0.1f), 0.2f);
            var trim = Mats.Lit(Color.Lerp(k.color, Color.white, 0.55f), 0.6f);
            var t = k.model;

            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.42f, 0f), new Vector3(1.25f, 0.4f, 2.1f), body);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.38f, 1.2f), new Vector3(0.9f, 0.3f, 0.6f), trim);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0f, 0.9f, -1.05f), new Vector3(1.4f, 0.1f, 0.5f), trim);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(-0.55f, 0.65f, -1.05f), new Vector3(0.1f, 0.5f, 0.1f), dark);
            Mats.Prim(PrimitiveType.Cube, t, new Vector3(0.55f, 0.65f, -1.05f), new Vector3(0.1f, 0.5f, 0.1f), dark);

            int w = 0;
            for (int fx = -1; fx <= 1; fx += 2)
                for (int fz = -1; fz <= 1; fz += 2)
                {
                    var wheel = Mats.Prim(PrimitiveType.Cylinder, t, new Vector3(fx * 0.72f, 0.33f, fz * 0.75f), new Vector3(0.66f, 0.14f, 0.66f), dark);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    v._wheels[w++] = wheel.transform;
                }

            // driver: round head with a face, plus a coloured helmet cap
            var skin = Mats.Lit(new Color(1f, 0.85f, 0.7f), 0.3f);
            var head = Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 1.05f, -0.15f), Vector3.one * 0.72f, skin);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 1.2f, -0.2f), new Vector3(0.78f, 0.5f, 0.78f), body);
            var white = Mats.Lit(Color.white, 0.6f);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(-0.15f, 1.07f, 0.17f), Vector3.one * 0.19f, white);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0.15f, 1.07f, 0.17f), Vector3.one * 0.19f, white);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(-0.15f, 1.07f, 0.25f), Vector3.one * 0.09f, dark);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0.15f, 1.07f, 0.25f), Vector3.one * 0.09f, dark);
            Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.9f, 0.2f), new Vector3(0.22f, 0.07f, 0.06f), dark);

            // drift sparks (two small spheres by the rear wheels)
            v._tierMats = new[] { Mats.UnlitUnique(new Color(0.3f, 0.7f, 1f)), Mats.UnlitUnique(new Color(1f, 0.6f, 0.1f)), Mats.UnlitUnique(new Color(0.85f, 0.3f, 1f)) };
            for (int s = 0; s < 2; s++)
            {
                var sp = Mats.Prim(PrimitiveType.Sphere, t, new Vector3(s == 0 ? -0.75f : 0.75f, 0.25f, -1.25f), Vector3.one * 0.38f, v._tierMats[0]);
                v._sparks[s] = sp.GetComponent<Renderer>();
                sp.SetActive(false);
            }

            // boost flame
            var fl = Mats.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.5f, -1.5f), new Vector3(0.5f, 0.5f, 1.4f), Mats.Unlit(new Color(1f, 0.55f, 0.1f)));
            v._flame = fl.transform;
            fl.SetActive(false);

            // shield bubble: three orbiting orbs
            var sh = new GameObject("Shield");
            sh.transform.SetParent(t, false);
            sh.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                Mats.Prim(PrimitiveType.Sphere, sh.transform, new Vector3(Mathf.Cos(a) * 1.5f, 0f, Mathf.Sin(a) * 1.5f), Vector3.one * 0.45f, Mats.Unlit(new Color(0.6f, 0.95f, 1f)));
            }
            sh.AddComponent<Wobble>().spin = new Vector3(0f, 260f, 0f);
            v._shield = sh;
            sh.SetActive(false);

            if (!k.isPlayer)
            {
                var lab = new GameObject("NameTag");
                lab.transform.SetParent(t, false);
                var tmp = lab.AddComponent<TextMeshPro>();
                tmp.text = k.racerName;
                tmp.fontSize = 3.2f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                tmp.outlineWidth = 0.25f;
                tmp.outlineColor = new Color32(0, 0, 0, 255);
                tmp.rectTransform.sizeDelta = new Vector2(8f, 1f);
                v._label = lab.transform;
            }
            return v;
        }

        public void Tick(Kart k)
        {
            float spin = k.speed * 60f * Time.deltaTime;
            for (int i = 0; i < 4; i++) if (_wheels[i] != null) _wheels[i].Rotate(0f, spin, 0f, Space.Self);

            bool spark = k.drifting && k.DriftTier > 0;
            for (int s = 0; s < 2; s++)
            {
                _sparks[s].gameObject.SetActive(k.drifting);
                if (k.drifting) _sparks[s].sharedMaterial = _tierMats[Mathf.Max(0, k.DriftTier - 1)];
                _sparks[s].transform.localScale = Vector3.one * (spark ? 0.5f + 0.15f * Mathf.Sin(Time.time * 40f + s) : 0.28f);
            }
            _flame.gameObject.SetActive(k.Boosting);
            if (k.Boosting) _flame.localScale = new Vector3(0.5f, 0.5f, 1.2f + 0.5f * Mathf.Sin(Time.time * 50f));
            _shield.SetActive(k.shield);

            if (_label != null)
            {
                var cam = Camera.main;
                _label.position = k.model.position + Vector3.up * 2.4f;
                if (cam != null) _label.rotation = cam.transform.rotation;
            }
        }
    }
}
