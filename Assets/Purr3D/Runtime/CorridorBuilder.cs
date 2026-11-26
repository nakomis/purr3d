using System;
using System.Collections.Generic;
using Purr3D.Core;
using TMPro;
using UnityEngine;

namespace Purr3D
{
    /// <summary>
    /// Lays out one door per project once <see cref="PurrService"/> has loaded the corridor: alternating left and
    /// right, most recently active project first, running along this object's local +Z. Put this object where the
    /// corridor starts (floor level, centre line) and the player just in front of it.
    ///
    /// It can also build a placeholder shell (floor and walls, optionally a ceiling) sized to the number of doors,
    /// so the corridor is walkable before there's any art. Turn that off once real corridor pieces exist.
    /// </summary>
    public sealed class CorridorBuilder : MonoBehaviour
    {
        [Header("Doors")]
        [SerializeField] private ProjectDoor doorPrefab;

        [Tooltip("Takes the player through a door into its room. Found on this object if left empty.")]
        [SerializeField] private RoomTravel roomTravel;

        [Tooltip("Distance between doors on the same side, in metres. Doors alternate sides, so neighbours are half this apart.")]
        [SerializeField, Min(1f)] private float spacing = 4f;

        [Tooltip("Walking distance before the first door.")]
        [SerializeField, Min(0f)] private float startOffset = 6f;

        [Header("Shell")]
        [SerializeField] private bool buildShell = true;

        [Tooltip("Off by default: a lid blocks the directional light and crowds the third-person camera.")]
        [SerializeField] private bool buildCeiling;

        [SerializeField, Min(2f)] private float width = 5f;
        [SerializeField, Min(2f)] private float height = 4f;
        [SerializeField, Min(0.05f)] private float wallThickness = 0.2f;

        [Tooltip("Room behind the start, so the player isn't spawned against the back wall.")]
        [SerializeField, Min(0f)] private float backPadding = 3f;

        [Tooltip("Leave empty for the render pipeline's default material.")]
        [SerializeField] private Material shellMaterial;

        [Header("Colour")]
        [SerializeField, Range(0f, 1f)] private float saturation = 0.55f;
        [SerializeField, Range(0f, 1f)] private float brightness = 0.85f;

        [Header("Loading")]
        [Tooltip("Optional world-space text near the start: shows progress, then hides. Shows the error if loading fails.")]
        [SerializeField] private TMP_Text loadingLabel;

        [Tooltip("Doors drawn by the editor gizmo, to preview the layout without playing.")]
        [SerializeField, Min(0)] private int previewDoorCount = 45;

        private readonly List<ProjectDoor> _doors = new List<ProjectDoor>();
        private Transform _doorRoot;
        private Transform _shellRoot;
        private PurrService _service;

        /// <summary>The spawned doors, in corridor order.</summary>
        public IReadOnlyList<ProjectDoor> Doors => _doors;

        /// <summary>The corridor's length for <paramref name="doorCount"/> doors, from the start to the end wall.</summary>
        public float LengthFor(int doorCount) => startOffset + Mathf.Max(0, (doorCount + 1) / 2 - 1) * spacing + spacing;

        private void Start()
        {
            // Floor first: the player spawns before the corridor has loaded and would otherwise fall into the void.
            // Sized for the preview count; Build() replaces it with the real length.
            if (buildShell) BuildShell(LengthFor(previewDoorCount));

            if (roomTravel == null) roomTravel = GetComponent<RoomTravel>();

            _service = PurrService.Instance;
            if (_service == null)
            {
                ShowLabel("No PurrService in the scene");
                Debug.LogWarning("[Purr] CorridorBuilder needs a PurrService in the scene.");
                return;
            }

            _service.CorridorProgress += OnProgress;
            _service.CorridorLoaded += Build;
            _service.CorridorFailed += OnFailed;
            _service.KeyNeeded += OnKeyNeeded;

            if (_service.KeyProblem != null) OnKeyNeeded(_service.KeyProblem);
            else if (_service.IsLoading) OnProgress(_service.Progress);
            else if (_service.LoadError != null) OnFailed(_service.LoadError);
            else if (_service.Corridor.Count > 0) Build(_service.Corridor);
            else ShowLabel("Loading projects…");
        }

        private void OnDestroy()
        {
            if (_service == null) return;
            _service.CorridorProgress -= OnProgress;
            _service.CorridorLoaded -= Build;
            _service.CorridorFailed -= OnFailed;
            _service.KeyNeeded -= OnKeyNeeded;
        }

        /// <summary>Clears any previous doors and lays out <paramref name="corridor"/>.</summary>
        public void Build(IReadOnlyList<CorridorEntry> corridor)
        {
            Clear();
            if (doorPrefab == null)
            {
                ShowLabel("CorridorBuilder has no door prefab");
                Debug.LogWarning("[Purr] CorridorBuilder: assign a door prefab.");
                return;
            }

            _doorRoot = NewChild("Doors");
            for (var i = 0; i < corridor.Count; i++)
            {
                var entry = corridor[i];
                DoorPose(i, out var position, out var rotation);
                var door = Instantiate(doorPrefab, _doorRoot);
                door.transform.localPosition = position;
                door.transform.localRotation = rotation;
                door.Bind(entry, ColourFor(entry.Project.Prefix));
                door.Entered += OnDoorEntered;
                _doors.Add(door);
                LoadIcon(door);
            }

            if (buildShell) BuildShell(LengthFor(corridor.Count));
            ShowLabel(null);
        }

        /// <summary>Door <paramref name="index"/>'s local pose: even on the left (-X), odd on the right, +Z facing in.</summary>
        public void DoorPose(int index, out Vector3 position, out Quaternion rotation)
        {
            var side = index % 2 == 0 ? -1f : 1f;
            position = new Vector3(side * width / 2f, 0f, startOffset + index / 2 * spacing);
            rotation = Quaternion.LookRotation(new Vector3(-side, 0f, 0f), Vector3.up);
        }

        public Color ColourFor(string prefix) => Color.HSVToRGB(ProjectHue.For(prefix), saturation, brightness);

        private void BuildShell(float length)
        {
            _shellRoot = NewChild("Shell");
            var t = wallThickness;
            var outer = width + 2f * t;
            var zMid = (length - backPadding) / 2f;
            var zSpan = length + backPadding;

            Block("Floor", new Vector3(0f, -t / 2f, zMid), new Vector3(outer, t, zSpan + 2f * t));
            Block("Wall left", new Vector3(-(width + t) / 2f, height / 2f, zMid), new Vector3(t, height, zSpan));
            Block("Wall right", new Vector3((width + t) / 2f, height / 2f, zMid), new Vector3(t, height, zSpan));
            Block("Back wall", new Vector3(0f, height / 2f, -backPadding - t / 2f), new Vector3(outer, height, t));
            Block("End wall", new Vector3(0f, height / 2f, length + t / 2f), new Vector3(outer, height, t));
            if (buildCeiling) Block("Ceiling", new Vector3(0f, height + t / 2f, zMid), new Vector3(outer, t, zSpan + 2f * t));
        }

        private void Block(string blockName, Vector3 centre, Vector3 size)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = blockName;
            block.transform.SetParent(_shellRoot, false);
            block.transform.localPosition = centre;
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = shellMaterial != null ? shellMaterial : Purr3DMaterials.Shell;
        }

        private void Clear()
        {
            _doors.Clear();
            if (_doorRoot != null) Destroy(_doorRoot.gameObject);
            if (_shellRoot != null) Destroy(_shellRoot.gameObject);
            _doorRoot = null;
            _shellRoot = null;
        }

        private Transform NewChild(string childName)
        {
            var child = new GameObject(childName).transform;
            child.SetParent(transform, false);
            return child;
        }

        /// <summary>
        /// Swaps the door's placeholder for its project's icon when that arrives. The corridor never waits: a door
        /// with no icon, or one that can't be fetched, keeps its lettered placeholder.
        /// </summary>
        private async void LoadIcon(ProjectDoor door)
        {
            var icons = PurrService.Instance != null ? PurrService.Instance.Icons : null;
            if (icons == null || door.Entry == null) return;
            try
            {
                var icon = await icons.GetAsync(door.Entry.Project);
                if (icon != null && door != null) door.ShowIcon(icon); // the door may have gone (a rebuild)
            }
            catch (OperationCanceledException)
            {
                // Play mode stopped mid-download.
            }
        }

        private void OnDoorEntered(ProjectDoor door)
        {
            if (roomTravel != null) roomTravel.Enter(door);
            else Debug.LogWarning("[Purr] CorridorBuilder: add a RoomTravel to open the rooms.");
        }

        private void OnProgress(LoadProgress progress) =>
            ShowLabel(progress.Total == 0 ? "Loading projects…" : $"Loading projects… {progress.Done}/{progress.Total}");

        // Shows behind the Connect to Purr screen; loading progress replaces it once a key is accepted.
        private void OnKeyNeeded(string reason) => ShowLabel("Waiting for an API key");

        private void OnFailed(string error) => ShowLabel($"Couldn't reach Purr\n<size=50%>{error}</size>");

        private void ShowLabel(string text)
        {
            if (loadingLabel == null) return;
            loadingLabel.gameObject.SetActive(text != null);
            if (text != null) loadingLabel.text = text;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            var length = LengthFor(previewDoorCount);
            var zMid = (length - backPadding) / 2f;
            Gizmos.color = new Color(1f, 1f, 1f, 0.4f);
            Gizmos.DrawWireCube(new Vector3(0f, height / 2f, zMid), new Vector3(width, height, length + backPadding));

            for (var i = 0; i < previewDoorCount; i++)
            {
                DoorPose(i, out var position, out var rotation);
                Gizmos.color = Color.HSVToRGB((float)i / Mathf.Max(1, previewDoorCount), saturation, brightness);
                Gizmos.DrawCube(position + Vector3.up * 1.1f, new Vector3(0.1f, 2.2f, 1.2f));
                Gizmos.DrawRay(position + Vector3.up * 1.1f, rotation * Vector3.forward * 0.6f);
            }
        }
    }
}
