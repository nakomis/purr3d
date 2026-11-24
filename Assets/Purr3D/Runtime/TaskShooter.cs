using System;
using System.Collections.Generic;
using Purr3D.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Purr3D
{
    /// <summary>
    /// Goes on the player (<see cref="RoomTravel"/> adds it, and enables it only in rooms). The Fire action in
    /// StarterAssets.inputactions reaches <see cref="OnFire"/> through PlayerInput's Send Messages.
    ///
    /// A shot: a ray from the camera through the middle of the screen picks the target; a bolt flies there from the
    /// hand; on arrival a crate's task moves to <see cref="StateCycle.Next"/> straight away, and Purr is told. If
    /// Purr refuses, the task is put back and the crate flies home.
    /// </summary>
    public sealed class TaskShooter : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float range = 60f;
        [SerializeField, Min(1f)] private float boltSpeed = 40f;
        [SerializeField] private Color boltColour = new Color(0.4f, 0.95f, 1f);
        [SerializeField, Min(0f)] private float secondsBetweenShots = 0.2f;

        [Header("Sounds (empty: synthesised placeholders)")]
        [SerializeField] private AudioClip shotSound;
        [SerializeField] private AudioClip finishedSound;
        [SerializeField] private AudioClip refusedSound;

        private readonly HashSet<string> _inFlight = new HashSet<string>(StringComparer.Ordinal);
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private AudioSource _audio;
        private Transform _hand;
        private float _nextShot;

        /// <summary>Who the shot's outcome is shown to, and which room's crates count. Set by <see cref="RoomTravel"/>.</summary>
        public RoomBuilder Room { get; set; }

        public TaskHud Hud { get; set; }

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;

            // A Humanoid rig (Dante is one) knows where its right hand is; otherwise shots leave from the chest.
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman) _hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        /// <summary>Called by PlayerInput (Send Messages) for the Fire action.</summary>
        public void OnFire(InputValue value)
        {
            if (!value.isPressed || !isActiveAndEnabled || Time.time < _nextShot) return;
            _nextShot = Time.time + secondsBetweenShots;
            Shoot();
        }

        private void Shoot()
        {
            var camera = Camera.main;
            if (camera == null) return;

            var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var crate = Aim(ray, out var point);

            var from = _hand != null ? _hand.position : transform.position + Vector3.up * 1.3f;
            Play(shotSound != null ? shotSound : ShotSounds.Zap);
            ShotBolt.Fire(from, point, boltColour, boltSpeed, () => Landed(crate));
        }

        /// <summary>The nearest thing on the ray that isn't the player or a trigger, and the crate it belongs to, if any.</summary>
        private TaskCrate Aim(Ray ray, out Vector3 point)
        {
            point = ray.GetPoint(range);
            var count = Physics.RaycastNonAlloc(ray, _hits, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var nearest = float.MaxValue;
            TaskCrate crate = null;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.distance >= nearest || hit.collider.transform.IsChildOf(transform)) continue;
                nearest = hit.distance;
                point = hit.point;
                crate = hit.collider.GetComponentInParent<TaskCrate>();
            }
            return crate;
        }

        private async void Landed(TaskCrate crate)
        {
            // The crate may have gone (you left the room, or a refresh removed it) while the bolt was flying.
            if (crate == null || Room == null || Room.Entry == null || crate.Task == null) return;
            var entry = Room.Entry;
            if (crate.Project != entry.Project) return;
            var before = crate.Task;
            if (_inFlight.Contains(before.Ref)) return;

            var next = StateCycle.Next(entry.Project, before.StateId);
            if (next == null)
            {
                crate.Wobble();
                Play(finishedSound != null ? finishedSound : ShotSounds.Thunk);
                return;
            }

            var service = PurrService.Instance;
            if (service == null) return;

            // Show it straight away; Purr's answer follows.
            _inFlight.Add(before.Ref);
            entry.ReplaceTask(before.WithState(next.Id, DateTimeOffset.UtcNow));
            Room.Apply(entry);
            Debug.Log($"[Purr] Shot {before.Ref}: {StateName(entry, before.StateId)} → {next.Name}");

            try
            {
                var after = await service.SetStateAsync(before, next);
                entry.ReplaceTask(after);
                if (Room != null) Room.Apply(entry);
            }
            catch (PurrApiException e)
            {
                entry.ReplaceTask(before);
                if (Room != null) Room.Apply(entry);
                Play(refusedSound != null ? refusedSound : ShotSounds.Buzz);
                if (Hud != null) Hud.Toast($"Purr said no to {before.Ref}: {Reason(e)}");
                Debug.LogWarning($"[Purr] Shot {before.Ref} refused, put back: {e.Message}");
            }
            catch (OperationCanceledException)
            {
                // Play mode stopped mid-request.
            }
            finally
            {
                _inFlight.Remove(before.Ref);
            }
        }

        private static string StateName(CorridorEntry entry, string stateId) =>
            StateCycle.Find(entry.Project, stateId)?.Name ?? TaskCard.UnknownState;

        private static string Reason(PurrApiException e)
        {
            switch (e.Status)
            {
                case 0: return "no connection";
                case 401:
                case 403: return "the API key wasn't accepted";
                case 409: return "it changed in Purr meanwhile";
                default: return $"HTTP {e.Status}";
            }
        }

        private void Play(AudioClip clip)
        {
            if (clip != null) _audio.PlayOneShot(clip);
        }
    }
}
