using System;
using UnityEngine;

namespace Purr3D
{
    /// <summary>
    /// The visible half of a shot, built in code: a glowing capsule with a trail that flies from the hand to where
    /// the camera's ray hit, then calls back and leaves a small puff. What was hit is decided before it sets off,
    /// so it never collides with anything itself.
    /// </summary>
    public sealed class ShotBolt : MonoBehaviour
    {
        private Vector3 _to;
        private float _speed;
        private Action _arrived;
        private Color _colour;

        public static ShotBolt Fire(Vector3 from, Vector3 to, Color colour, float speed, Action arrived)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Shot";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = from;
            go.transform.localScale = new Vector3(0.08f, 0.16f, 0.08f);
            var direction = to - from;
            if (direction.sqrMagnitude > 0.0001f)
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);

            Glow(go.GetComponent<Renderer>(), colour);

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.startWidth = 0.07f;
            trail.endWidth = 0f;
            trail.sharedMaterial = go.GetComponent<Renderer>().sharedMaterial;
            trail.startColor = colour;
            trail.endColor = new Color(colour.r, colour.g, colour.b, 0f);

            var bolt = go.AddComponent<ShotBolt>();
            bolt._to = to;
            bolt._speed = speed;
            bolt._arrived = arrived;
            bolt._colour = colour;
            return bolt;
        }

        private void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, _to, _speed * Time.deltaTime);
            if ((transform.position - _to).sqrMagnitude > 0.0001f) return;

            Puff(_to, _colour);
            var arrived = _arrived;
            _arrived = null;
            Destroy(gameObject);
            arrived?.Invoke();
        }

        /// <summary>A small bright sphere that grows and fades where the shot landed.</summary>
        private static void Puff(Vector3 at, Color colour)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Shot puff";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = at;
            Glow(go.GetComponent<Renderer>(), colour);
            go.AddComponent<PuffFade>();
        }

        private static readonly System.Collections.Generic.Dictionary<Color, Material> Glows =
            new System.Collections.Generic.Dictionary<Color, Material>();

        /// <summary>
        /// The shared Unlit material (see <see cref="Purr3DMaterials"/>) in the shot's colour: one copy per colour,
        /// made once, rather than a new material every shot (and no Shader.Find, which fails in a build).
        /// </summary>
        private static void Glow(Renderer renderer, Color colour)
        {
            if (!Glows.TryGetValue(colour, out var material) || material == null)
            {
                var unlit = Purr3DMaterials.Unlit;
                if (unlit == null) return;
                material = new Material(unlit) { name = "Shot glow" };
                material.SetColor("_BaseColor", colour);
                material.SetColor("_Color", colour);
                Glows[colour] = material;
            }
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private sealed class PuffFade : MonoBehaviour
        {
            private const float Seconds = 0.18f;
            private float _age;

            private void Update()
            {
                _age += Time.deltaTime;
                var t = _age / Seconds;
                transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 0.45f, t);
                if (t >= 1f) Destroy(gameObject);
            }
        }
    }
}
