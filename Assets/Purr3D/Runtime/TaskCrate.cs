using System.Collections;
using Purr3D.Core;
using TMPro;
using UnityEngine;

namespace Purr3D
{
    /// <summary>
    /// Goes on the task crate prefab: a Rigidbody box with the task's ref on it. <see cref="RoomBuilder"/> spawns
    /// one per task, and slides it with <see cref="MoveTo"/> when the task changes state.
    ///
    /// Prefab convention: pivot at the centre of the box. Crates stand unrotated in room space, where you look
    /// along +Z, so the front label goes on the -Z face and the other lies on top.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TaskCrate : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("Labels that show the ref, e.g. \"HOME-42\": one on the front, one on top, say.")]
        [SerializeField] private TMP_Text[] refLabels = new TMP_Text[0];

        [Tooltip("Renderers that take the project's colour.")]
        [SerializeField] private Renderer[] tinted = new Renderer[0];

        [SerializeField, Min(0.01f)] private float slideSeconds = 0.5f;

        private Rigidbody _body;
        private Coroutine _slide;

        public PurrTask Task { get; private set; }

        public PurrProject Project { get; private set; }

        /// <summary>The door's colour, which the crate wears.</summary>
        public Color Colour { get; private set; }

        /// <summary>What the HUD shows for this crate, worked out afresh so it's current after a refresh.</summary>
        public TaskCard Card => Project != null && Task != null ? TaskCard.From(Project, Task) : null;

        private void Awake() => _body = GetComponent<Rigidbody>();

        public void Bind(PurrProject project, PurrTask task, Color colour)
        {
            Project = project;
            Task = task;
            Colour = colour;
            name = $"Crate {task.Ref}";
            foreach (var label in refLabels)
            {
                if (label != null) label.text = task.Ref;
            }

            var block = new MaterialPropertyBlock();
            foreach (var r in tinted)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, colour);
                block.SetColor(ColorId, colour);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>Puts the crate at a room-local position, upright and at rest.</summary>
        public void PlaceAt(Vector3 localPosition)
        {
            if (_slide != null) StopCoroutine(_slide);
            _slide = null;
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.identity;
            Settle();
        }

        /// <summary>Slides the crate to a room-local position, ignoring physics on the way.</summary>
        public void MoveTo(Vector3 localPosition)
        {
            if (_slide != null) StopCoroutine(_slide);
            _slide = isActiveAndEnabled ? StartCoroutine(Slide(localPosition)) : null;
            if (_slide == null) PlaceAt(localPosition);
        }

        private IEnumerator Slide(Vector3 target)
        {
            _body.isKinematic = true;
            var from = transform.localPosition;
            var fromRotation = transform.localRotation;
            var lift = Vector3.up * 0.6f; // hop over the crates in between
            for (var t = 0f; t < 1f; t += Time.deltaTime / slideSeconds)
            {
                var eased = Mathf.SmoothStep(0f, 1f, t);
                transform.localPosition = Vector3.Lerp(from, target, eased) + lift * Mathf.Sin(eased * Mathf.PI);
                transform.localRotation = Quaternion.Slerp(fromRotation, Quaternion.identity, eased);
                yield return null;
            }
            _slide = null;
            PlaceAt(target);
        }

        /// <summary>A little shudder in place: the shot landed, but the task has nowhere to go.</summary>
        public void Wobble()
        {
            if (_slide != null || !isActiveAndEnabled) return;
            _slide = StartCoroutine(Shudder());
        }

        private IEnumerator Shudder()
        {
            _body.isKinematic = true;
            var home = transform.localPosition;
            const float seconds = 0.3f;
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                var fade = 1f - t / seconds;
                transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 60f) * 8f * fade, 0f);
                transform.localPosition = home + Vector3.up * (0.05f * Mathf.Sin(t / seconds * Mathf.PI));
                yield return null;
            }
            _slide = null;
            PlaceAt(home);
        }

        private void Settle()
        {
            if (_body == null) _body = GetComponent<Rigidbody>();
            _body.isKinematic = false;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.Sleep(); // hundreds of crates cost nothing until something knocks them
        }
    }
}
