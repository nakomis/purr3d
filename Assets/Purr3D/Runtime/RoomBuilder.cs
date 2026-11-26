using System;
using System.Collections.Generic;
using Purr3D.Core;
using TMPro;
using UnityEngine;

namespace Purr3D
{
    /// <summary>
    /// Lives in the Room scene and builds whichever project's room the player walks into: a floor laid out like a
    /// board (one labelled zone per state, see <see cref="RoomLayout"/>) with a crate per task, walls, and an exit.
    ///
    /// There's one room, reused for every door. <see cref="RoomTravel"/> moves this object into place, calls
    /// <see cref="Show"/>, and later <see cref="Apply"/> with freshly fetched tasks, which slides changed crates
    /// to their new zones instead of rebuilding.
    ///
    /// Room space: the entrance (and exit) is at z = 0, the room runs along +Z, centred on x = 0.
    /// </summary>
    public sealed class RoomBuilder : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Crates")]
        [SerializeField] private TaskCrate cratePrefab;

        [Tooltip("The crate's edge length, for stacking.")]
        [SerializeField, Min(0.05f)] private float crateSize = 0.5f;

        [Header("Layout")]
        [SerializeField, Min(1f)] private float zoneWidth = 4f;
        [SerializeField, Min(1)] private int columns = 5;
        [SerializeField, Min(0.1f)] private float pitch = 0.7f;
        [SerializeField, Min(1)] private int maxRows = 12;

        [Tooltip("Clear floor between the entrance and the first row of crates.")]
        [SerializeField, Min(1f)] private float frontMargin = 3f;

        [Header("Shell")]
        [SerializeField, Min(2f)] private float wallHeight = 4f;
        [SerializeField, Min(0.05f)] private float wallThickness = 0.2f;
        [Tooltip("Floor either side of the outermost zones.")]
        [SerializeField, Min(0f)] private float sideMargin = 1f;
        [Header("Surfaces (MaterialCraft, PURR3D-15)")]
        [Tooltip("Leave empty for Resources/Purr3D/Surfaces/RoomFloor.")]
        [SerializeField] private Material floorMaterial;
        [Tooltip("Leave empty for Resources/Purr3D/Surfaces/CorridorWall.")]
        [SerializeField] private Material wallMaterial;
        [Tooltip("Leave empty for Resources/Purr3D/Surfaces/DoorFace.")]
        [SerializeField] private Material exitMaterial;
        [Tooltip("How many metres one copy of a surface's texture covers.")]
        [SerializeField, Min(0.1f)] private float tileMetres = 2f;

        [Header("Zones")]
        [SerializeField, Range(0f, 1f)] private float zoneBrightness = 0.55f;
        [SerializeField] private float zoneLabelSize = 3f;

        [Header("Arrival")]
        [Tooltip("Where the player appears, in room space: just inside the entrance, clear of the exit trigger.")]
        [SerializeField] private Vector3 arrivalPoint = new Vector3(0f, 0f, 1.5f);

        private readonly Dictionary<string, TaskCrate> _crates = new Dictionary<string, TaskCrate>(StringComparer.Ordinal);
        private Transform _floorRoot;
        private Transform _crateRoot;
        private TMP_Text _status;
        private PlayerTrigger _exit;
        private List<PurrTask> _shown = new List<PurrTask>();
        private Color _colour;

        /// <summary>The project on show, or null when the room is hidden.</summary>
        public CorridorEntry Entry { get; private set; }

        /// <summary>The player walked into the exit.</summary>
        public event Action ExitRequested;

        /// <summary>Where the player should stand on arrival, in world space.</summary>
        public Vector3 ArrivalPoint => transform.TransformPoint(arrivalPoint);

        public IReadOnlyDictionary<string, TaskCrate> Crates => _crates;

        private void Awake()
        {
            _crateRoot = NewChild("Crates");
            // Floating ahead of you as you arrive. TMP reads correctly looking along +Z, the way you face.
            _status = Label(transform, "Status", "", 2.5f, new Vector3(0f, 2.6f, frontMargin + 2f), Quaternion.identity, new Vector2(6f, 1.5f));
            _exit = PlayerTrigger.Create(transform, "Exit trigger", new Vector3(0f, 1.1f, 0.35f), new Vector3(1.6f, 2.2f, 0.7f));
            _exit.Entered += (_, __) => ExitRequested?.Invoke();
        }

        /// <summary>Builds <paramref name="entry"/>'s room from its cached tasks, every crate in its place.</summary>
        public void Show(CorridorEntry entry, Color colour)
        {
            gameObject.SetActive(true);
            ClearCrates();
            Entry = entry;
            _colour = colour;
            _shown = new List<PurrTask>();
            Lay(entry.Tasks, slide: false);
            Status(entry.LoadFailed ? "Offline: no tasks cached" : null);
        }

        /// <summary>
        /// Brings the room up to date with <paramref name="entry"/>'s tasks: new crates appear, gone ones vanish,
        /// and crates whose slot changed slide there. Returns what changed.
        /// </summary>
        public RoomDiff Apply(CorridorEntry entry)
        {
            if (Entry == null || entry != Entry) return new RoomDiff();
            var diff = RoomDiff.Compute(_shown, entry.Tasks);
            Lay(entry.Tasks, slide: true);
            Status(null);
            return diff;
        }

        /// <summary>Shows a line of text floating in the room, or hides it when null.</summary>
        public void Status(string text)
        {
            _status.gameObject.SetActive(!string.IsNullOrEmpty(text));
            _status.text = text ?? "";
        }

        public void Hide()
        {
            ClearCrates();
            Entry = null;
            _shown = new List<PurrTask>();
            gameObject.SetActive(false);
        }

        private void Lay(List<PurrTask> tasks, bool slide)
        {
            var plan = RoomLayout.Build(Entry.Project, tasks, Settings());
            BuildFloor(plan);

            var keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (var task in tasks)
            {
                if (task?.Ref == null || !plan.Slots.TryGetValue(task.Ref, out var slot) || !keep.Add(task.Ref)) continue;
                var position = new Vector3(slot.X, crateSize / 2f + slot.Layer * (crateSize + 0.01f), slot.Z);

                if (_crates.TryGetValue(task.Ref, out var crate))
                {
                    crate.Bind(Entry.Project, task, _colour);
                    if (!slide) crate.PlaceAt(position);
                    else if ((crate.transform.localPosition - position).sqrMagnitude > 0.0001f) crate.MoveTo(position);
                }
                else if (cratePrefab != null)
                {
                    crate = Instantiate(cratePrefab, _crateRoot);
                    crate.Bind(Entry.Project, task, _colour);
                    crate.PlaceAt(position);
                    _crates[task.Ref] = crate;
                }
            }

            var gone = new List<string>();
            foreach (var reference in _crates.Keys)
            {
                if (!keep.Contains(reference)) gone.Add(reference);
            }
            foreach (var reference in gone)
            {
                Destroy(_crates[reference].gameObject);
                _crates.Remove(reference);
            }

            _shown = new List<PurrTask>(tasks);
            if (cratePrefab == null) Status("RoomBuilder has no crate prefab");
        }

        private RoomLayoutSettings Settings() => new RoomLayoutSettings
        {
            ZoneWidth = zoneWidth,
            Columns = columns,
            Pitch = pitch,
            MaxRows = maxRows,
            FrontMargin = frontMargin,
        };

        private void BuildFloor(RoomPlan plan)
        {
            if (_floorRoot != null) Destroy(_floorRoot.gameObject);
            _floorRoot = NewChild("Floor and walls");

            var t = wallThickness;
            var width = Mathf.Max(plan.Width, 4f) + 2f * sideMargin;
            var depth = plan.Depth;

            var floor = floorMaterial != null ? floorMaterial : Purr3DMaterials.RoomFloor;
            var wall = wallMaterial != null ? wallMaterial : Purr3DMaterials.CorridorWall;
            var exit = exitMaterial != null ? exitMaterial : Purr3DMaterials.DoorFace;

            Block("Floor", new Vector3(0f, -t / 2f, depth / 2f), new Vector3(width + 2f * t, t, depth + 2f * t), floor, null);
            Block("Wall left", new Vector3(-(width + t) / 2f, wallHeight / 2f, depth / 2f), new Vector3(t, wallHeight, depth), wall, null);
            Block("Wall right", new Vector3((width + t) / 2f, wallHeight / 2f, depth / 2f), new Vector3(t, wallHeight, depth), wall, null);
            Block("Back wall", new Vector3(0f, wallHeight / 2f, depth + t / 2f), new Vector3(width + 2f * t, wallHeight, t), wall, null);
            Block("Entrance wall", new Vector3(0f, wallHeight / 2f, -t / 2f), new Vector3(width + 2f * t, wallHeight, t), wall, null);

            // The way out: a door-coloured panel on the entrance wall, with its trigger in front.
            Block("Exit", new Vector3(0f, 1.1f, 0.02f), new Vector3(1.2f, 2.2f, 0.04f), exit, _colour, collide: false);
            Label(_floorRoot, "Exit label", "Back to the corridor", 1.2f, new Vector3(0f, 2.45f, 0.05f), Quaternion.Euler(0f, 180f, 0f), new Vector2(3f, 0.4f));

            foreach (var zone in plan.Zones)
            {
                var zoneDepth = depth - frontMargin + 0.6f;
                Block($"Zone {zone.Name}",
                    new Vector3(zone.CentreX, 0.005f, frontMargin - 0.6f + zoneDepth / 2f),
                    new Vector3(zone.Width - 0.2f, 0.01f, zoneDepth),
                    Purr3DMaterials.Shell, ZoneColour(zone), collide: false);

                // Lying flat at the front of the zone, reading from the entrance.
                Label(_floorRoot, $"Label {zone.Name}", $"{zone.Name}\n<size=60%>{zone.Count}</size>", zoneLabelSize,
                    new Vector3(zone.CentreX, 0.02f, frontMargin - 1.2f), Quaternion.Euler(90f, 0f, 0f),
                    new Vector2(zone.Width - 0.4f, 1.1f));
            }
        }

        private Color ZoneColour(RoomZone zone) => StateColours.For(zone.State?.Group, zoneBrightness);

        private void Block(string blockName, Vector3 centre, Vector3 size, Material material, Color? colour, bool collide = true)
        {
            var block = TiledBox.Create(blockName, _floorRoot, centre, size, tileMetres,
                material != null ? material : Purr3DMaterials.Shell, collide);
            var renderer = block.GetComponent<Renderer>();
            if (colour.HasValue)
            {
                var props = new MaterialPropertyBlock();
                props.SetColor(BaseColorId, colour.Value);
                props.SetColor(ColorId, colour.Value);
                renderer.SetPropertyBlock(props);
            }
        }

        private static TMP_Text Label(Transform parent, string labelName, string text, float size, Vector3 position, Quaternion rotation, Vector2 box)
        {
            var go = new GameObject(labelName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;

            var label = go.AddComponent<TextMeshPro>();
            label.rectTransform.sizeDelta = box;
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.color = Color.white;
            return label;
        }

        private void ClearCrates()
        {
            foreach (var crate in _crates.Values)
            {
                if (crate != null) Destroy(crate.gameObject);
            }
            _crates.Clear();
        }

        private Transform NewChild(string childName)
        {
            var child = new GameObject(childName).transform;
            child.SetParent(transform, false);
            return child;
        }
    }
}
