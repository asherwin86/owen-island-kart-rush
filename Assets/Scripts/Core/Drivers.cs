using UnityEngine;
using UnityEngine.InputSystem;

namespace KartRacer
{
    /// <summary>Static input state, filled by the keyboard / gamepad here and by TouchControls on phones.</summary>
    public static class KartInput
    {
        public static float TouchSteer;
        public static bool TouchDrift, TouchBrake, TouchItem, TouchActive;
    }

    public class PlayerDriver : MonoBehaviour
    {
        public Kart kart;
        public System.Action<Kart> OnItemPressed;
        public bool itemPressed;

        private void Update()
        {
            if (kart == null) return;
            float steer = 0f, throttle = 0f, brake = 0f;
            bool drift = false, item = false;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle = 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) brake = 1f;
                if (kb.spaceKey.isPressed || kb.leftShiftKey.isPressed) drift = true;
                if (kb.eKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame || kb.leftCtrlKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) item = true;
                if (kb.rKey.wasPressedThisFrame && kart.raceActive) kart.Respawn();
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 ls = pad.leftStick.ReadValue();
                if (Mathf.Abs(ls.x) > 0.15f) steer = ls.x;
                if (pad.rightTrigger.ReadValue() > 0.1f || pad.buttonSouth.isPressed) throttle = Mathf.Max(throttle, 1f);
                if (pad.leftTrigger.ReadValue() > 0.1f || pad.buttonEast.isPressed) brake = 1f;
                if (pad.rightShoulder.isPressed || pad.leftShoulder.isPressed) drift = true;
                if (pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame) item = true;
            }
            if (KartInput.TouchActive)
            {
                if (Mathf.Abs(KartInput.TouchSteer) > 0.05f) steer = KartInput.TouchSteer;
                throttle = KartInput.TouchBrake ? 0f : 1f; // phones auto-accelerate
                if (KartInput.TouchBrake) brake = 1f;
                if (KartInput.TouchDrift) drift = true;
                if (KartInput.TouchItem) { item = true; KartInput.TouchItem = false; }
            }
            kart.steer = Mathf.MoveTowards(kart.steer, steer, 6f * Time.deltaTime * 2f);
            kart.throttle = throttle;
            kart.brake = brake;
            kart.driftHeld = drift;
            itemPressed = item;
            if (item) OnItemPressed?.Invoke(kart);
        }
    }

    /// <summary>Follows the centre line, with a little personality: lane offset, skill and item use.</summary>
    public class AIDriver : MonoBehaviour
    {
        public Kart kart;
        public float skill = 0.9f;        // 0..1, scales top speed (set each frame by RaceManager)
        public float baseSkill = 0.9f;
        public float laneOffset;          // metres from the centre line
        public System.Action<Kart> UseItem;
        private float _itemDelay;
        private float _wobble;

        private void Start() { _wobble = Random.value * 10f; }

        private void Update()
        {
            if (kart == null || !kart.raceActive) { if (kart != null) { kart.throttle = 0f; } return; }
            var t = kart.track;
            int look = 6 + Mathf.RoundToInt(Mathf.Clamp(kart.speed, 0f, 40f) * 0.22f);
            int target = t.Wrap(kart.trackIdx + look);
            Vector3 tp = t.pts[target] + t.right[target] * (laneOffset + Mathf.Sin(Time.time * 0.6f + _wobble) * 1.5f);
            Vector3 to = tp - kart.Position; to.y = 0f;
            float angle = Vector3.SignedAngle(kart.Forward, to, Vector3.up);
            float steer = Mathf.Clamp(angle / 22f, -1f, 1f);
            kart.steer = Mathf.MoveTowards(kart.steer, steer, 5f * Time.deltaTime * 2f);
            float aa = Mathf.Abs(angle);
            kart.throttle = aa > 55f ? 0.35f : 1f;
            kart.brake = 0f;
            // drift through hard corners once up to speed
            kart.driftHeld = aa > 24f && kart.speed > 20f;
            kart.speedMul = skill;

            if (kart.item != ItemType.None && kart.itemRoll <= 0f)
            {
                _itemDelay -= Time.deltaTime;
                if (_itemDelay <= 0f)
                {
                    UseItem?.Invoke(kart);
                    _itemDelay = 1.5f + Random.value * 3f;
                }
            }
        }
    }
}
