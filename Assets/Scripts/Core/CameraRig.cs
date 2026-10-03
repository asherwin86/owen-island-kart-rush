using UnityEngine;

namespace KartRacer
{
    /// <summary>Chase camera for the player's kart. Swings out during drifts and widens the FOV with speed.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Kart target;
        public Camera cam;
        private float _yaw, _fov = 62f;
        private Vector3 _menuPos = new Vector3(0f, 6f, -14f);

        public static CameraRig Create()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var c = go.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.55f, 0.8f, 1f);
            c.nearClipPlane = 0.3f; c.farClipPlane = 900f; c.fieldOfView = 62f;
            go.AddComponent<AudioListener>();
            var rig = go.AddComponent<CameraRig>();
            rig.cam = c;
            go.transform.position = rig._menuPos;
            return rig;
        }

        public void Snap(Kart k)
        {
            target = k;
            if (k != null) { _yaw = k.heading; Place(1f); }
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Place(Time.deltaTime);
        }

        private void Place(float dt)
        {
            float follow = target.drifting ? target.heading + target.driftDir * 14f : target.heading;
            _yaw = Mathf.LerpAngle(_yaw, follow, Mathf.Clamp01(dt * (target.drifting ? 3.5f : 6f)));
            Quaternion q = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 anchor = target.Position + Vector3.up * 1.2f;
            float back = 7.2f + Mathf.Clamp01(target.speed / 40f) * 1.4f;
            Vector3 desired = anchor - q * Vector3.forward * back + Vector3.up * 2.9f;
            transform.position = Vector3.Lerp(transform.position, desired, Mathf.Clamp01(dt * 14f));
            transform.rotation = Quaternion.LookRotation((anchor + q * Vector3.forward * 4f) - transform.position, Vector3.up);
            float fovTarget = 62f + Mathf.Clamp01(target.speed / 45f) * 14f + (target.Boosting ? 10f : 0f);
            _fov = Mathf.Lerp(_fov, fovTarget, Mathf.Clamp01(dt * 5f));
            cam.fieldOfView = _fov;
        }
    }
}
