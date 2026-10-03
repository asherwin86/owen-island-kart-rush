using TMPro;
using UnityEngine;

namespace KartRacer
{
    public enum ItemType { None, Turbo, Slick, Bolt, Shield }

    /// <summary>One kart: a sphere rigidbody that is steered arcade-style (heading + grip), with
    /// drifting, mini-turbo boosts, spin-outs and a held item. A driver (player or AI) sets the inputs.</summary>
    public class Kart : MonoBehaviour
    {
        public string racerName;
        public Color color;
        public bool isPlayer;
        public int id;
        public int netId;
        public bool isRemote;
        public Track track;

        // inputs, written by the driver
        public float steer, throttle, brake;
        public bool driftHeld;

        // tuning
        public float maxSpeed = 34f, accel = 20f, turnRate = 100f;
        public float speedMul = 1f;

        // state
        public Rigidbody rb;
        public Transform model;
        public float heading;
        public bool drifting;
        public int driftDir;
        public float driftCharge, boostTimer, spinTimer, speed, visualSpin, padCooldown, itemRoll, stuckTimer;
        public bool shield, raceActive;
        public ItemType item = ItemType.None;
        public int trackIdx, crossings, place = 1, startSlot;
        public float finishTime;
        public bool finished;
        private bool _prevDrift;
        private float _roll;
        public KartVisual visual;

        public float Progress => crossings * (float)(track != null ? track.N : 1) + trackIdx;
        public Vector3 Position => rb != null ? rb.position : transform.position;
        public int DriftTier => driftCharge > 2.6f ? 3 : driftCharge > 1.7f ? 2 : driftCharge > 0.9f ? 1 : 0;
        public bool Boosting => boostTimer > 0f;

        public static Kart Create(string racerName, Color color, bool isPlayer, int id, Track track, Vector3 pos, float headingDeg)
        {
            var go = new GameObject("Kart_" + racerName);
            go.transform.position = pos + Vector3.up * 0.6f;
            var k = go.AddComponent<Kart>();
            k.racerName = racerName; k.color = color; k.isPlayer = isPlayer; k.id = id; k.track = track;
            k.heading = headingDeg;

            var sc = go.AddComponent<SphereCollider>();
            sc.radius = 0.6f;
            sc.material = new PhysicsMaterial { dynamicFriction = 0f, staticFriction = 0f, bounciness = 0.15f, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Average };
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f; rb.linearDamping = 0.05f; rb.angularDamping = 5f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            k.rb = rb;

            var modelGo = new GameObject("Model");
            modelGo.transform.SetParent(go.transform, false);
            modelGo.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            k.model = modelGo.transform;
            k.visual = KartVisual.Build(k);
            k.trackIdx = track.Nearest(pos, 0, track.N / 2);
            return k;
        }

        public Vector3 Forward => Quaternion.Euler(0f, heading, 0f) * Vector3.forward;

        /// <summary>Turns this kart into a network proxy: no physics, no collisions, driven by snapshots.</summary>
        public void SetRemote()
        {
            isRemote = true;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;
            var sc = GetComponent<SphereCollider>();
            if (sc != null) sc.isTrigger = true;
        }

        private void FixedUpdate()
        {
            if (isRemote) return;
            float dt = Time.fixedDeltaTime;
            Vector3 vel = rb.linearVelocity;
            Vector3 planar = new Vector3(vel.x, 0f, vel.z);
            Vector3 fwd = Forward;

            if (!raceActive)
            {
                rb.linearVelocity = new Vector3(0f, vel.y, 0f);
                speed = 0f;
                return;
            }

            speed = Vector3.Dot(planar, fwd);
            float lat = track != null ? track.Lateral(rb.position, trackIdx) : 0f;
            bool off = Mathf.Abs(lat) > track.halfWidth + 1.4f;

            float cap = maxSpeed * speedMul * (off ? 0.58f : 1f);
            float acc = accel * (off ? 0.7f : 1f);
            if (boostTimer > 0f) { cap = maxSpeed * speedMul * 1.5f; acc = accel * 3.5f; boostTimer -= dt; }

            float inThrottle = throttle, inSteer = steer;
            if (spinTimer > 0f)
            {
                spinTimer -= dt;
                visualSpin += 720f * dt;
                inThrottle = 0f; inSteer = 0f;
                cap *= 0.25f;
                drifting = false;
            }
            else visualSpin = Mathf.MoveTowardsAngle(visualSpin, 0f, 900f * dt);

            // drift start / end
            if (driftHeld && !_prevDrift && !drifting && speed > 11f && Mathf.Abs(inSteer) > 0.25f && spinTimer <= 0f)
            {
                drifting = true; driftDir = inSteer > 0f ? 1 : -1; driftCharge = 0f;
            }
            if (drifting && (!driftHeld || speed < 8f))
            {
                EndDrift();
            }
            _prevDrift = driftHeld;

            // heading
            float speedFactor = Mathf.Clamp01(Mathf.Abs(speed) / 9f);
            float dir = speed >= -0.5f ? 1f : -1f;
            float turnInput = inSteer;
            if (drifting)
            {
                turnInput = driftDir * (0.85f + 0.45f * inSteer * driftDir);
                driftCharge += dt * (0.8f + 0.5f * Mathf.Abs(inSteer * driftDir > 0f ? inSteer : 0f));
            }
            heading += turnInput * turnRate * speedFactor * dt * dir;

            fwd = Forward;

            // speed along the heading
            if (inThrottle > 0f)
            {
                if (speed < cap) speed = Mathf.MoveTowards(speed, cap, acc * inThrottle * dt);
                else speed = Mathf.MoveTowards(speed, cap, 28f * dt);
            }
            else if (brake > 0f) speed = Mathf.MoveTowards(speed, -maxSpeed * 0.25f, 42f * dt);
            else speed = Mathf.MoveTowards(speed, 0f, 7f * dt);
            if (speed > cap) speed = Mathf.MoveTowards(speed, cap, 30f * dt);

            // grip: low while drifting so the kart slides
            float grip = drifting ? 2.3f : (spinTimer > 0f ? 1.5f : 9f);
            Vector3 desired = fwd * speed;
            planar = Vector3.Lerp(planar, desired, 1f - Mathf.Exp(-grip * dt));
            rb.linearVelocity = new Vector3(planar.x, vel.y, planar.z);

            if (padCooldown > 0f) padCooldown -= dt;

            // respawn if the kart fell, or has been stuck for a while
            if (rb.position.y < -6f) Respawn();
            if (Mathf.Abs(speed) < 1.2f && planar.magnitude < 1.5f) stuckTimer += dt; else stuckTimer = 0f;
            if (stuckTimer > 4f) { Respawn(); stuckTimer = 0f; }
        }

        private void EndDrift()
        {
            if (!drifting) return;
            drifting = false;
            int tier = DriftTier;
            if (tier > 0) Boost(tier == 1 ? 0.7f : tier == 2 ? 1.2f : 1.8f);
            driftCharge = 0f;
        }

        public void Boost(float seconds)
        {
            boostTimer = Mathf.Max(boostTimer, seconds);
            speed = Mathf.Max(speed, maxSpeed * 0.95f);
        }

        /// <summary>Returns true if the hit landed (false if a shield absorbed it).</summary>
        public bool Hit()
        {
            if (spinTimer > 0f) return false;
            if (shield) { shield = false; return false; }
            spinTimer = 1.4f;
            boostTimer = 0f;
            drifting = false; driftCharge = 0f;
            Vector3 v = rb.linearVelocity; rb.linearVelocity = new Vector3(v.x * 0.3f, v.y, v.z * 0.3f);
            return true;
        }

        public void Respawn()
        {
            int idx = track.Nearest(rb.position, trackIdx, 30);
            idx = track.Wrap(idx - 2);
            rb.position = track.pts[idx] + Vector3.up * 0.7f;
            rb.linearVelocity = Vector3.zero;
            heading = Mathf.Atan2(track.tan[idx].x, track.tan[idx].z) * Mathf.Rad2Deg;
            speed = 0f; drifting = false; spinTimer = 0f; boostTimer = 0f;
            trackIdx = idx;
        }

        private void LateUpdate()
        {
            if (model == null) return;
            float targetRoll = (drifting ? driftDir * 7f : -steer * 5f);
            _roll = Mathf.Lerp(_roll, targetRoll, 10f * Time.deltaTime);
            float yawOffset = drifting ? driftDir * 22f : 0f;
            model.rotation = Quaternion.Euler(0f, heading + yawOffset + visualSpin, _roll);
            if (visual != null) visual.Tick(this);
        }
    }
}
