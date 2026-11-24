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

        [Header("Icon (PURR3D-13)")]
        [Tooltip("Shows the project's icon. Leave empty and the door makes its own square tile at Icon Centre.")]
        [SerializeField] private Renderer iconRenderer;

        [Tooltip("The placeholder's letter, for projects with no icon. Leave empty and the door makes its own.")]
        [SerializeField] private TMP_Text iconLetter;

        [Tooltip("Centre of the made tile, in the door's space (+Z is out into the corridor).")]
        [SerializeField] private Vector3 iconCentre = new Vector3(0f, 1.9f, 0.15f);

        [SerializeField, Min(0.05f)] private float iconSize = 0.45f;

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

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private void Awake()
        {
            PlayerTrigger.Create(transform, "Entry trigger", triggerCentre, triggerSize).Entered += (_, __) => Entered?.Invoke(this);
            if (iconRenderer == null) iconRenderer = MakeIconTile();
            if (iconLetter == null) iconLetter = MakeIconLetter();
        }

        /// <summary>
        /// Shows the project's icon, or with null the placeholder: the prefix's first letter on a darker tile in the
        /// door's colour, as Purr's web UI does for a project with no icon.
        /// </summary>
        public void ShowIcon(Texture2D icon)
        {
            if (iconRenderer == null) return;
            var block = new MaterialPropertyBlock();
            iconRenderer.GetPropertyBlock(block);
            var colour = icon != null ? Color.white : Color.Lerp(Colour, Color.black, 0.35f);
            block.SetColor(BaseColorId, colour);
            block.SetColor(ColorId, colour);
            var texture = icon != null ? icon : Texture2D.whiteTexture;
            block.SetTexture(BaseMapId, texture);
            block.SetTexture(MainTexId, texture);
            iconRenderer.SetPropertyBlock(block);

            if (iconLetter != null)
            {
                var prefix = Entry?.Project?.Prefix;
                iconLetter.text = string.IsNullOrEmpty(prefix) ? "?" : prefix.Substring(0, 1).ToUpperInvariant();
                iconLetter.gameObject.SetActive(icon == null);
            }
        }

        /// <summary>A quad facing out into the corridor, unlit, so the icon shows its own colours.</summary>
        private Renderer MakeIconTile()
        {
            var tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
            tile.name = "Icon";
            Destroy(tile.GetComponent<Collider>());
            tile.transform.SetParent(transform, false);
            tile.transform.localPosition = iconCentre;
            // A quad faces -Z; turned round, it faces +Z, the corridor, and reads the right way round from there.
            tile.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            tile.transform.localScale = new Vector3(iconSize, iconSize, 1f);

            var renderer = tile.GetComponent<Renderer>();
            // A real material asset, not Shader.Find, so it survives a build (see Purr3DMaterials).
            renderer.sharedMaterial = Purr3DMaterials.Unlit;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
        }

        private TMP_Text MakeIconLetter()
        {
            var go = new GameObject("Icon letter");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = iconCentre + new Vector3(0f, 0f, 0.005f);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var text = go.AddComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.1f;
            text.fontSizeMax = 5f;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            go.SetActive(false);
            return text;
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

            ShowIcon(null); // the placeholder until the icon arrives (CorridorBuilder asks for it)
        }
    }
}
