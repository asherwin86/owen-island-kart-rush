using System.Collections.Generic;
using UnityEngine;

namespace KartRacer
{
    /// <summary>Item boxes, boost pads, and the items themselves (turbo, slick puddle, homing bolt, shield).</summary>
    public class ItemSystem : MonoBehaviour
    {
        private class Box { public Transform t; public Vector3 pos; public float respawn; public Material mat; }
        private class Hazard { public GameObject go; public Kart owner; public float immune = 1.2f; public float life = 40f; }
        private class BoltObj { public GameObject go; public Kart owner; public Kart target; public Vector3 dir; public float life = 7f; }

        private readonly List<Box> _boxes = new List<Box>();
        private readonly List<Hazard> _hazards = new List<Hazard>();
        private readonly List<BoltObj> _bolts = new List<BoltObj>();
        private readonly Dictionary<Kart, float> _shieldTimers = new Dictionary<Kart, float>();
        private Track _track;
        private List<Kart> _karts;
        private Transform _root;
        public System.Action<Kart, string> OnEvent; // (kart, message) for HUD toasts
        public System.Action<Vector3> OnSlickDropped;   // online: tell the others
        public System.Action<Kart> OnBoltHitRemote;     // online: tell the victim

        public void Setup(Track track, List<Kart> karts)
        {
            _track = track; _karts = karts;
            _root = new GameObject("Items").transform;
            int i = 0;
            foreach (var p in track.boxPositions)
            {
                var mat = Mats.UnlitUnique(Color.white);
                var go = Mats.Prim(PrimitiveType.Cube, _root, p, Vector3.one * 1.3f, mat);
                go.AddComponent<Wobble>().spin = new Vector3(30f, 90f, 20f);
                _boxes.Add(new Box { t = go.transform, pos = p, mat = mat });
                i++;
            }
        }

        public void Clear()
        {
            if (_root != null) Destroy(_root.gameObject);
            foreach (var h in _hazards) if (h.go != null) Destroy(h.go);
            foreach (var b in _bolts) if (b.go != null) Destroy(b.go);
            _hazards.Clear(); _bolts.Clear(); _boxes.Clear();
        }

        private void Update()
        {
            if (_track == null || _karts == null) return;
            float dt = Time.deltaTime;

            for (int b = 0; b < _boxes.Count; b++)
            {
                var box = _boxes[b];
                box.mat.color = Color.HSVToRGB((Time.time * 0.4f + b * 0.07f) % 1f, 0.55f, 1f);
                if (box.respawn > 0f)
                {
                    box.respawn -= dt;
                    if (box.respawn <= 0f) box.t.gameObject.SetActive(true);
                    continue;
                }
                foreach (var k in _karts)
                {
                    if (!k.raceActive || k.isRemote) continue;
                    if ((k.Position - box.pos).sqrMagnitude < 2.6f * 2.6f)
                    {
                        box.t.gameObject.SetActive(false);
                        box.respawn = 4f;
                        if (k.item == ItemType.None && k.itemRoll <= 0f) GiveItem(k);
                        break;
                    }
                }
            }

            foreach (var k in _karts)
            {
                if (k.itemRoll > 0f) k.itemRoll -= dt;
                if (_shieldTimers.TryGetValue(k, out float st))
                {
                    st -= dt;
                    if (st <= 0f) { k.shield = false; _shieldTimers.Remove(k); } else _shieldTimers[k] = st;
                }
                if (!k.raceActive || k.isRemote || k.padCooldown > 0f) continue;
                foreach (var pad in _track.pads)
                {
                    Vector3 d = k.Position - new Vector3(pad.x, pad.y, pad.z);
                    Quaternion rot = Quaternion.Euler(0f, pad.w, 0f);
                    Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
                    if (Mathf.Abs(Vector3.Dot(d, f)) < 4f && Mathf.Abs(Vector3.Dot(d, r)) < _track.halfWidth * 0.27f)
                    {
                        k.Boost(1.1f); k.padCooldown = 1.5f;
                        if (k.isPlayer) OnEvent?.Invoke(k, "BOOST!");
                        break;
                    }
                }
            }

            for (int h = _hazards.Count - 1; h >= 0; h--)
            {
                var hz = _hazards[h];
                hz.immune -= dt; hz.life -= dt;
                bool remove = hz.life <= 0f;
                if (!remove)
                    foreach (var k in _karts)
                    {
                        if (k.isRemote || (k == hz.owner && hz.immune > 0f)) continue;
                        if ((k.Position - hz.go.transform.position).sqrMagnitude < 1.7f * 1.7f)
                        {
                            if (k.Hit() && k.isPlayer) OnEvent?.Invoke(k, "SPIN OUT!");
                            remove = true; break;
                        }
                    }
                if (remove) { Destroy(hz.go); _hazards.RemoveAt(h); }
            }

            for (int b = _bolts.Count - 1; b >= 0; b--)
            {
                var bolt = _bolts[b];
                bolt.life -= dt;
                bool remove = bolt.life <= 0f;
                if (bolt.target != null)
                {
                    Vector3 want = (bolt.target.Position - bolt.go.transform.position); want.y = 0f;
                    if (want.sqrMagnitude > 0.01f) bolt.dir = Vector3.RotateTowards(bolt.dir, want.normalized, 2.2f * dt, 0f);
                }
                bolt.go.transform.position += bolt.dir * 62f * dt;
                if (!remove)
                    foreach (var k in _karts)
                    {
                        if (k == bolt.owner && bolt.life > 6.4f) continue;
                        if ((k.Position - bolt.go.transform.position).sqrMagnitude < 1.8f * 1.8f)
                        {
                            if (k.isRemote) { OnBoltHitRemote?.Invoke(k); remove = true; break; }
                            if (k.Hit() && k.isPlayer) OnEvent?.Invoke(k, "HIT BY A BOLT!");
                            remove = true; break;
                        }
                    }
                if (remove) { Destroy(bolt.go); _bolts.RemoveAt(b); }
            }
        }

        private void GiveItem(Kart k)
        {
            int n = _karts.Count;
            float ratio = n <= 1 ? 0f : (k.place - 1f) / (n - 1f);
            float wTurbo = 20f + 40f * ratio, wSlick = 10f + 30f * (1f - ratio), wBolt = 8f + 35f * ratio, wShield = 10f + 30f * (1f - ratio);
            float wTriple = 6f + 18f * ratio, wMega = 1f + 16f * ratio * ratio;
            float roll = Random.value * (wTurbo + wSlick + wBolt + wShield + wTriple + wMega);
            ItemType it;
            if ((roll -= wTurbo) < 0f) it = ItemType.Turbo;
            else if ((roll -= wSlick) < 0f) it = ItemType.Slick;
            else if ((roll -= wBolt) < 0f) it = ItemType.Bolt;
            else if ((roll -= wShield) < 0f) it = ItemType.Shield;
            else if ((roll -= wTriple) < 0f) it = ItemType.Triple;
            else it = ItemType.Mega;
            k.item = it;
            if (it == ItemType.Triple) k.tripleLeft = 3;
            if (k.isPlayer) Sfx.Play("pickup", 0.7f);
            k.itemRoll = 0.9f;
        }

        public void SpawnSlick(Vector3 pos, Kart owner)
        {
            var go = Mats.Prim(PrimitiveType.Cylinder, _root, pos, new Vector3(2.4f, 0.04f, 2.4f), Mats.Unlit(new Color(1f, 0.9f, 0.1f)));
            _hazards.Add(new Hazard { go = go, owner = owner });
        }

        public void Use(Kart k)
        {
            if (k.item == ItemType.None || k.itemRoll > 0f || !k.raceActive) return;
            if (k.isPlayer) Sfx.Play("use", 0.6f);
            switch (k.item)
            {
                case ItemType.Triple:
                    k.Boost(1.0f);
                    k.tripleLeft--;
                    if (k.tripleLeft > 0) { k.itemRoll = 0.4f; return; }
                    break;
                case ItemType.Mega:
                    k.Boost(2.6f);
                    k.shield = true; _shieldTimers[k] = 2.8f;
                    break;
                case ItemType.Turbo:
                    k.Boost(1.6f);
                    break;
                case ItemType.Slick:
                {
                    Vector3 sp = k.Position - k.Forward * 2.4f - Vector3.up * 0.55f;
                    SpawnSlick(sp, k);
                    OnSlickDropped?.Invoke(sp);
                    break;
                }
                case ItemType.Bolt:
                {
                    Kart target = null; float best = float.MaxValue;
                    foreach (var o in _karts)
                    {
                        if (o == k) continue;
                        float diff = o.Progress - k.Progress;
                        if (diff > 0f && diff < best) { best = diff; target = o; }
                    }
                    var go = Mats.Prim(PrimitiveType.Sphere, _root, k.Position + k.Forward * 2.2f, Vector3.one * 0.8f, Mats.Unlit(new Color(1f, 0.15f, 0.2f)));
                    _bolts.Add(new BoltObj { go = go, owner = k, target = target, dir = k.Forward });
                    break;
                }
                case ItemType.Shield:
                    k.shield = true;
                    _shieldTimers[k] = 9f;
                    break;
            }
            k.item = ItemType.None;
        }
    }
}
