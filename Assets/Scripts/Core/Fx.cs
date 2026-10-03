using UnityEngine;

namespace KartRacer
{
    /// <summary>Particle helpers. Particles are small round meshes with unlit materials (no particle shaders needed).</summary>
    public static class Fx
    {
        private static Mesh _sphere;

        private static Mesh SphereMesh()
        {
            if (_sphere != null) return _sphere;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(tmp);
            return _sphere;
        }

        public static ParticleSystem Make(Transform parent, Vector3 localPos, Color color, float size, float life, float speed, float rate, int max = 40, bool burst = false)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = !burst; main.playOnAwake = false;
            main.startLifetime = life; main.startSpeed = speed; main.startSize = size;
            main.startColor = color; main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = burst ? 1.2f : -0.05f;
            var em = ps.emission; em.rateOverTime = burst ? 0f : rate; em.enabled = false;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = SphereMesh();
            r.sharedMaterial = Mats.Unlit(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (!burst) ps.Play();
            return ps;
        }

        public static void Set(ParticleSystem ps, bool on)
        {
            if (ps == null) return;
            var em = ps.emission;
            if (em.enabled != on) em.enabled = on;
            if (on && !ps.isPlaying) ps.Play();
        }

        /// <summary>One-shot confetti burst that cleans itself up.</summary>
        public static void Confetti(Vector3 pos)
        {
            var root = new GameObject("Confetti");
            root.transform.position = pos + Vector3.up * 2f;
            Color[] cols = { new Color(1f, 0.3f, 0.35f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.8f, 1f), new Color(0.5f, 1f, 0.5f), new Color(0.85f, 0.45f, 1f) };
            foreach (var c in cols)
            {
                var ps = Make(root.transform, Vector3.zero, c, 0.28f, 2.2f, 9f, 0f, 40, true);
                var sh = ps.shape; sh.radius = 0.6f;
                ps.Emit(34);
                ps.Play();
            }
            Object.Destroy(root, 4f);
        }
    }
}
