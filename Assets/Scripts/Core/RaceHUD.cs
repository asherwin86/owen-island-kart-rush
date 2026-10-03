using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static KartRacer.UIKit;

namespace KartRacer
{
    /// <summary>In-race overlay: position, lap, timer, item slot, speed, minimap, countdown, toasts, and on-screen touch buttons.</summary>
    public class RaceHUD : MonoBehaviour
    {
        private RaceManager _race;
        private GameObject _root, _touchRoot;
        private TMP_Text _place, _lap, _time, _speed, _item, _count, _toast, _wrong, _drift;
        private Image _itemBox;
        private RectTransform _mapRect;
        private Image[] _dots = new Image[16];
        private Texture2D _mapTex;
        private RawImage _mapImg;
        private Track _mapTrack;
        private Vector2 _mapMin, _mapSize;
        private bool _touchWanted;
        private System.Action _onPause;

        public static RaceHUD Create(Transform parent, RaceManager race, System.Action onPause)
        {
            var go = new GameObject("RaceHUD");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<RaceHUD>();
            h._race = race; h._onPause = onPause;
            h.Build();
            return h;
        }

        private void Build()
        {
            var canvas = MakeCanvas("HudCanvas", 20, transform);
            _root = Group(canvas.transform, "Hud").gameObject;
            var t = _root.transform;

            _place = Label(t, "1ST", 84f, BL, new Vector2(170f, 90f), new Vector2(320f, 120f), TextAlignmentOptions.Left);
            _place.fontStyle = FontStyles.Bold;
            _lap = Label(t, "LAP 1/3", 44f, TR, new Vector2(-170f, -46f), new Vector2(320f, 70f), TextAlignmentOptions.Right);
            _time = Label(t, "0:00.00", 38f, T, new Vector2(0f, -40f), new Vector2(300f, 60f));
            _speed = Label(t, "0", 40f, B, new Vector2(0f, 40f), new Vector2(300f, 60f));
            _itemBox = Box(t, "ItemBox", TL, new Vector2(105f, -105f), new Vector2(130f, 130f), new Color(0f, 0f, 0f, 0.5f));
            _item = Label(_itemBox.transform, "", 30f, C, Vector2.zero, new Vector2(130f, 130f));
            _count = Label(t, "", 170f, C, new Vector2(0f, 90f), new Vector2(900f, 240f));
            _count.fontStyle = FontStyles.Bold;
            _toast = Label(t, "", 56f, C, new Vector2(0f, 190f), new Vector2(1000f, 90f));
            _toast.fontStyle = FontStyles.Bold;
            _wrong = Label(t, "WRONG WAY", 60f, C, new Vector2(0f, 270f), new Vector2(900f, 90f), TextAlignmentOptions.Center, new Color(1f, 0.25f, 0.25f));
            _drift = Label(t, "", 34f, B, new Vector2(0f, 110f), new Vector2(600f, 50f));

            // minimap
            var mapBg = Box(t, "MiniMap", TR, new Vector2(-120f, -250f), new Vector2(200f, 200f), new Color(0f, 0f, 0f, 0.45f));
            _mapRect = mapBg.rectTransform;
            var imgGo = new GameObject("MapImage", typeof(RectTransform), typeof(RawImage));
            imgGo.transform.SetParent(_mapRect, false);
            Stretch((RectTransform)imgGo.transform);
            _mapImg = imgGo.GetComponent<RawImage>();
            _mapImg.raycastTarget = false;
            for (int i = 0; i < _dots.Length; i++)
            {
                var d = Box(_mapRect, "Dot" + i, C, Vector2.zero, new Vector2(10f, 10f), Color.white, false);
                d.sprite = CircleSprite();
                d.raycastTarget = false;
                _dots[i] = d;
            }

            BuildTouch(canvas.transform);
            _root.SetActive(false);
        }

        private class Hold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            public System.Action<bool> OnHold;
            public void OnPointerDown(PointerEventData e) => OnHold?.Invoke(true);
            public void OnPointerUp(PointerEventData e) => OnHold?.Invoke(false);
            public void OnPointerExit(PointerEventData e) => OnHold?.Invoke(false);
        }

        private void BuildTouch(Transform canvas)
        {
            _touchRoot = Group(canvas, "Touch");
            var tr = _touchRoot.transform;
            float left = 0f, right = 0f;
            MakeHold(tr, "<", BL, new Vector2(130f, 140f), 170f, h => left = h ? -1f : 0f, () => KartInput.TouchSteer = left + right);
            MakeHold(tr, ">", BL, new Vector2(330f, 140f), 170f, h => right = h ? 1f : 0f, () => KartInput.TouchSteer = left + right);
            MakeHold(tr, "DRIFT", BR, new Vector2(-330f, 150f), 150f, h => KartInput.TouchDrift = h, null);
            MakeHold(tr, "BRAKE", BR, new Vector2(-130f, 110f), 120f, h => KartInput.TouchBrake = h, null);
            var item = Box(tr, "ItemButton", BR, new Vector2(-170f, 290f), new Vector2(150f, 150f), new Color(1f, 1f, 1f, 0.3f), false);
            item.sprite = CircleSprite();
            item.gameObject.AddComponent<Hold>().OnHold = h => { if (h) KartInput.TouchItem = true; };
            Label(item.transform, "ITEM", 32f, C, Vector2.zero, new Vector2(150f, 150f));
            var pause = Box(tr, "PauseButton", TR, new Vector2(-60f, -60f), new Vector2(80f, 80f), new Color(1f, 1f, 1f, 0.3f), false);
            pause.sprite = CircleSprite();
            pause.gameObject.AddComponent<Hold>().OnHold = h => { if (h) _onPause?.Invoke(); };
            Label(pause.transform, "II", 34f, C, Vector2.zero, new Vector2(80f, 80f));
            _touchRoot.SetActive(false);
        }

        private void MakeHold(Transform parent, string label, Vector2 anchor, Vector2 pos, float size, System.Action<bool> onHold, System.Action after)
        {
            var img = Box(parent, label + "Hold", anchor, pos, new Vector2(size, size), new Color(1f, 1f, 1f, 0.28f), false);
            img.sprite = CircleSprite();
            img.gameObject.AddComponent<Hold>().OnHold = h => { onHold(h); after?.Invoke(); };
            Label(img.transform, label, size > 140f ? 44f : 28f, C, Vector2.zero, new Vector2(size, size));
        }

        private void BuildMap(Track track)
        {
            _mapTrack = track;
            const int S = 160;
            _mapTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var clear = new Color[S * S];
            _mapTex.SetPixels(clear);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in track.pts) { min = Vector2.Min(min, new Vector2(p.x, p.z)); max = Vector2.Max(max, new Vector2(p.x, p.z)); }
            float size = Mathf.Max(max.x - min.x, max.y - min.y) * 1.08f;
            _mapMin = (min + max) * 0.5f - Vector2.one * size * 0.5f; _mapSize = Vector2.one * size;
            for (int i = 0; i < track.N * 3; i++)
            {
                Vector3 a = track.pts[(i / 3) % track.N], b = track.pts[((i / 3) + 1) % track.N];
                Vector3 p = Vector3.Lerp(a, b, (i % 3) / 3f);
                int x = Mathf.RoundToInt((p.x - _mapMin.x) / _mapSize.x * (S - 1)), y = Mathf.RoundToInt((p.z - _mapMin.y) / _mapSize.y * (S - 1));
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int xx = Mathf.Clamp(x + dx, 0, S - 1), yy = Mathf.Clamp(y + dy, 0, S - 1);
                        _mapTex.SetPixel(xx, yy, new Color(1f, 1f, 1f, 0.9f));
                    }
            }
            _mapTex.Apply();
            _mapImg.texture = _mapTex;
        }

        private Vector2 MapPos(Vector3 world)
        {
            Vector2 n = new Vector2((world.x - _mapMin.x) / _mapSize.x, (world.z - _mapMin.y) / _mapSize.y);
            Vector2 sz = _mapRect.sizeDelta;
            return new Vector2((n.x - 0.5f) * sz.x, (n.y - 0.5f) * sz.y);
        }

        private void Update()
        {
            bool active = _race != null && _race.state != RaceState.Idle && _race.player != null;
            if (_root.activeSelf != active) _root.SetActive(active);
            if (!active) { _touchRoot.SetActive(false); KartInput.TouchActive = false; return; }

            if (_mapTrack != _race.track) BuildMap(_race.track);

            // touch controls: shown on phones, or after the first touch; hidden after a key press
            var touch = Touchscreen.current;
            if (Application.isMobilePlatform) _touchWanted = true;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) _touchWanted = true;
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame && !Application.isMobilePlatform) _touchWanted = false;
            if (_touchRoot.activeSelf != _touchWanted) _touchRoot.SetActive(_touchWanted);
            KartInput.TouchActive = _touchWanted;

            var p = _race.player;
            var standings = _race.Standings();
            _place.text = Ordinal(p.place) + "<size=40><color=#dddddd> / " + _race.karts.Count + "</color></size>";
            _lap.text = "LAP " + _race.PlayerLap() + "/" + _race.totalLaps;
            _time.text = TimeString(Mathf.Max(0f, _race.raceTime));
            _speed.text = Mathf.RoundToInt(Mathf.Abs(p.speed) * 4f) + " <size=24>km/h</size>";

            // item slot
            if (p.item == ItemType.None && p.itemRoll <= 0f) { _item.text = ""; _itemBox.color = new Color(0f, 0f, 0f, 0.5f); }
            else
            {
                ItemType show = p.itemRoll > 0f ? (ItemType)(1 + (int)(Time.time * 14f) % 6) : p.item;
                _item.text = show == ItemType.Triple && p.itemRoll <= 0f ? "TRIPLE x" + p.tripleLeft : show.ToString().ToUpper();
                _itemBox.color = show == ItemType.Turbo || show == ItemType.Triple ? new Color(1f, 0.55f, 0.1f, 0.85f) : show == ItemType.Mega ? new Color(0.7f, 0.3f, 0.95f, 0.9f) : show == ItemType.Slick ? new Color(0.9f, 0.8f, 0.1f, 0.85f)
                    : show == ItemType.Bolt ? new Color(0.95f, 0.2f, 0.25f, 0.85f) : new Color(0.2f, 0.8f, 0.95f, 0.85f);
            }

            // countdown, toast, wrong way, drift tier
            if (_race.state == RaceState.Countdown) { int c = Mathf.CeilToInt(_race.countdown); _count.text = c.ToString(); _count.color = c == 1 ? new Color(1f, 0.85f, 0.2f) : Color.white; }
            else _count.text = "";
            _toast.text = Time.time < _race.toastUntil ? _race.toast : "";
            _wrong.gameObject.SetActive(_race.PlayerWrongWay());
            if (p.drifting) { string[] names = { "DRIFT", "MINI-TURBO!", "SUPER TURBO!!", "ULTRA TURBO!!!" }; _drift.text = names[p.DriftTier]; _drift.color = p.DriftTier == 0 ? Color.white : p.DriftTier == 1 ? new Color(0.4f, 0.75f, 1f) : p.DriftTier == 2 ? new Color(1f, 0.65f, 0.2f) : new Color(0.9f, 0.4f, 1f); }
            else _drift.text = "";

            // minimap dots
            for (int i = 0; i < _dots.Length; i++)
            {
                bool has = i < _race.karts.Count;
                _dots[i].gameObject.SetActive(has);
                if (!has) continue;
                var k = _race.karts[i];
                var rt = _dots[i].rectTransform;
                rt.anchoredPosition = MapPos(k.Position);
                rt.sizeDelta = Vector2.one * (k.isPlayer ? 15f : 9f);
                _dots[i].color = k.isPlayer ? new Color(1f, 0.9f, 0.1f) : k.color;
            }
        }
    }
}
