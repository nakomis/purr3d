using System;
using UnityEngine;

namespace Purr3D
{
    /// <summary>
    /// Goes on the player (<see cref="RoomTravel"/> adds it at runtime) and says which <see cref="TaskCrate"/>, if
    /// any, they're standing on: a short sphere-cast down from the feet each frame. One cast beats a trigger on every
    /// crate (HOME has 540), and on a stack it finds the crate you're actually on.
    /// </summary>
    public sealed class TaskStandReader : MonoBehaviour
    {
        [Tooltip("How long you must be off a crate before it counts, so a bump or a hop to the next crate doesn't flicker the HUD.")]
        [SerializeField, Min(0f)] private float graceSeconds = 0.15f;

        [Tooltip("How far below the feet a crate still counts as underfoot.")]
        [SerializeField, Min(0.01f)] private float reach = 0.3f;

        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private CharacterController _controller;
        private TaskCrate _current;
        private float _lostAt = -1f;

        /// <summary>The crate underfoot, or null after stepping off.</summary>
        public TaskCrate StandingOn => _current;

        /// <summary>Raised with the new crate, or null when you step off.</summary>
        public event Action<TaskCrate> StandingOnChanged;

        private void Awake() => _controller = GetComponent<CharacterController>();

        private void Update()
        {
            var found = CrateUnderfoot();
            if (found != null)
            {
                _lostAt = -1f;
                if (found != _current) Set(found);
                return;
            }
            if (_current == null) return;

            // _current is null-ish (destroyed) when the room was rebuilt or left: drop it straight away.
            if (!_current) { Set(null); return; }
            if (_lostAt < 0f) _lostAt = Time.time;
            if (Time.time - _lostAt >= graceSeconds) Set(null);
        }

        private void OnDisable()
        {
            if (_current != null) Set(null);
        }

        private void Set(TaskCrate crate)
        {
            _current = crate;
            _lostAt = -1f;
            StandingOnChanged?.Invoke(crate);
        }

        private TaskCrate CrateUnderfoot()
        {
            if (_controller != null && !_controller.isGrounded) return null;

            var radius = _controller != null ? _controller.radius * 0.8f : 0.2f;
            var origin = transform.position + Vector3.up * (radius + 0.05f);
            var count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, _hits, reach + 0.05f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            TaskCrate best = null;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= bestDistance) continue;

                // The nearest thing underfoot decides: standing on the floor next to a crate isn't standing on it.
                bestDistance = hit.distance;
                best = hit.collider.GetComponentInParent<TaskCrate>();
            }
            return best;
        }
    }
}
