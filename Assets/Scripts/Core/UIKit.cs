using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KartRacer
{
    /// <summary>Small helpers for building uGUI at runtime (no scene or prefab editing needed).</summary>
    public static class UIKit
    {
        public static readonly Vector2 C = new Vector2(0.5f, 0.5f), T = new Vector2(0.5f, 1f), B = new Vector2(0.5f, 0f),
            TL = new Vector2(0f, 1f), TR = new Vector2(1f, 1f), BL = new Vector2(0f, 0f), BR = new Vector2(1f, 0f), L = new Vector2(0f, 0.5f), R = new Vector2(1f, 0.5f);

        private static Sprite _round, _circle;

        public static Canvas MakeCanvas(string name, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static Sprite RoundSprite()
        {
            if (_round != null) return _round;
            const int s = 48; const float r = 14f;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(r - x, x - (s - 1 - r)));
                    float dy = Mathf.Max(0f, Mathf.Max(r - y, y - (s - 1 - r)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
                }
            tex.Apply();
            _round = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _round;
        }

        public static Sprite CircleSprite()
        {
            if (_circle != null) return _circle;
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f - 0.5f, s / 2f - 0.5f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(s / 2f - d)));
                }
            tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _circle;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static GameObject Group(Transform parent, string name, bool stretch = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            if (stretch) Stretch((RectTransform)go.transform);
            return go;
        }

        public static Image Panel(Transform parent, string name, Color color, bool rounded = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var img = go.GetComponent<Image>();
            img.color = color;
            if (rounded) { img.sprite = RoundSprite(); img.type = Image.Type.Sliced; }
            return img;
        }

        public static Image Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, bool rounded = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            if (rounded) { img.sprite = RoundSprite(); img.type = Image.Type.Sliced; }
            return img;
        }

        public static TMP_Text Label(Transform parent, string text, float size, Vector2 anchor, Vector2 pos, Vector2 dim,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = dim;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = align;
            t.color = color ?? Color.white;
            t.raycastTarget = false; t.richText = true;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public static Button Btn(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 dim, Color color, UnityAction onClick, float fontSize = 30f)
        {
            var img = Box(parent, label + "Button", anchor, pos, dim, color);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(onClick);
            var t = Label(img.transform, label, fontSize, C, Vector2.zero, dim);
            Stretch((RectTransform)t.transform);
            return btn;
        }

        public static TMP_InputField InputBox(Transform parent, string placeholder, Vector2 anchor, Vector2 pos, Vector2 dim, int maxLen)
        {
            var img = Box(parent, "Input", anchor, pos, dim, new Color(1f, 1f, 1f, 0.9f));
            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(img.transform, false);
            var art = (RectTransform)area.transform;
            Stretch(art); art.offsetMin = new Vector2(14f, 4f); art.offsetMax = new Vector2(-14f, -4f);
            var ph = Label(area.transform, placeholder, 28f, C, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, new Color(0.3f, 0.3f, 0.35f, 0.6f));
            Stretch((RectTransform)ph.transform);
            var txt = Label(area.transform, "", 30f, C, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left, new Color(0.1f, 0.1f, 0.15f));
            Stretch((RectTransform)txt.transform);
            var input = img.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = art; input.textComponent = txt; input.placeholder = ph; input.characterLimit = maxLen;
            return input;
        }

        public static string Ordinal(int n) => n == 1 ? "1ST" : n == 2 ? "2ND" : n == 3 ? "3RD" : n + "TH";

        public static string TimeString(float t)
        {
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return m + ":" + s.ToString("00.00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
