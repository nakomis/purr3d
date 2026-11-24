using System;
using Purr3D.Core;
using TMPro;
using UnityEngine;

namespace Purr3D
{
    /// <summary>
    /// Goes on the door prefab. <see cref="CorridorBuilder"/> spawns one per project and calls <see cref="Bind"/>.
    ///
    /// Prefab convention: pivot at the bottom centre of the doorway, the face (and labels) towards +Z. The builder
    /// turns +Z to face into the corridor.
    ///
    /// The door makes its own trigger just in front of the leaf, so walking into it raises <see cref="Entered"/>.
    /// </summary>
    public sealed class ProjectDoor : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP Lit / Unlit
        private static readonly int ColorId = Shader.PropertyToID("_Color");         // built-in shaders

        [Tooltip("The project's name, e.g. \"Home infrastructure\".")]
        [SerializeField] private TMP_Text nameLabel;

        [Tooltip("The ref prefix, e.g. \"HOME\".")]
        [SerializeField] private TMP_Text prefixLabel;

        [Tooltip("Renderers that take the project's colour (the door leaf, say). Leave the frame out to keep it neutral.")]
        [SerializeField] private Renderer[] tinted = new Renderer[0];

        [Header("Entry trigger")]
        [Tooltip("Centre of the trigger box, in the door's space (+Z is out into the corridor).")]
        [SerializeField] private Vector3 triggerCentre = new Vector3(0f, 1.1f, 0.5f);

        [SerializeField] private Vector3 triggerSize = new Vector3(1.2f, 2.2f, 0.7f);

        [Tooltip("How far in front of the door the player comes back out of the room: clear of the trigger.")]
        [SerializeField, Min(0f)] private float returnDistance = 1.8f;

        /// <summary>The player walked into the door.</summary>
        public event Action<ProjectDoor> Entered;

        /// <summary>Where the player stands on coming back out of the room, in world space.</summary>
        public Vector3 ReturnPoint => transform.TransformPoint(new Vector3(0f, 0f, returnDistance));

        /// <summary>The project behind this door, with its cached tasks.</summary>
        public CorridorEntry Entry { get; private set; }

        public Color Colour { get; private set; }

        private void Awake()
        {
            PlayerTrigger.Create(transform, "Entry trigger", triggerCentre, triggerSize).Entered += (_, __) => Entered?.Invoke(this);
        }

        public void Bind(CorridorEntry entry, Color colour)
        {
            Entry = entry;
            Colour = colour;
            name = $"Door {entry.Project.Prefix}";

            if (nameLabel != null) nameLabel.text = entry.LoadFailed ? $"{entry.Project.Name} (offline)" : entry.Project.Name;
            if (prefixLabel != null) prefixLabel.text = entry.Project.Prefix;

            // A property block tints each door without cloning its material.
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
    }
}
