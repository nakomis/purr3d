using System;
using System.Threading;
using System.Threading.Tasks;
using Purr3D.Core;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Purr3D
{
    /// <summary>
    /// Takes the player through a door into its project's room and back out again. Put it on the Corridor object
    /// beside <see cref="CorridorBuilder"/>, which hands it each door the player walks into.
    ///
    /// The Room scene is loaded additively once, at startup, and its one <see cref="RoomBuilder"/> is reused for
    /// every door, so going through a door costs only the crates. Behind a short fade, the room is turned to face
    /// the way you walked in (so the camera needs no turning), built from the cached tasks, and you're moved there.
    /// Then the project is fetched again and changed crates slide into their new zones.
    /// </summary>
    public sealed class RoomTravel : MonoBehaviour
    {
        [Tooltip("The scene holding the RoomBuilder. It must be in Build Profiles' scene list.")]
        [SerializeField] private string roomScene = "Room";

        [Tooltip("Where rooms are built, well away from the corridor.")]
        [SerializeField] private Vector3 roomOrigin = new Vector3(300f, 0f, 0f);

        [SerializeField, Min(0f)] private float fadeSeconds = 0.25f;

        private RoomBuilder _room;
        private ScreenFade _fade;
        private TaskHud _hud;
        private TaskStandReader _reader;
        private TaskShooter _shooter;
        private ProjectDoor _door;
        private bool _travelling;
        private CancellationTokenSource _lifetime;

        /// <summary>The door the player came through, or null in the corridor.</summary>
        public ProjectDoor CurrentDoor => _door;

        private void Awake()
        {
            _lifetime = new CancellationTokenSource();
            _fade = ScreenFade.Create(transform);
            _hud = TaskHud.Create(transform);
        }

        private async void Start()
        {
            // Reading the crate underfoot (PURR3D-6): the player gets a reader that drives the HUD.
            var player = GameObject.FindWithTag(PlayerTrigger.PlayerTag);
            if (player != null)
            {
                // Not ??: Unity's fake-null objects slip past it.
                _reader = player.GetComponent<TaskStandReader>();
                if (_reader == null) _reader = player.AddComponent<TaskStandReader>();
                _reader.StandingOnChanged += OnStandingOnChanged;

                // Shooting (PURR3D-7): off until you're in a room.
                _shooter = player.GetComponent<TaskShooter>();
                if (_shooter == null) _shooter = player.AddComponent<TaskShooter>();
                _shooter.Hud = _hud;
                _shooter.enabled = false;
            }

            try
            {
                _room = FindAnyObjectByType<RoomBuilder>(FindObjectsInactive.Include);
                if (_room == null)
                {
                    var load = SceneManager.LoadSceneAsync(roomScene, LoadSceneMode.Additive);
                    if (load == null)
                    {
                        Debug.LogWarning($"[Purr] RoomTravel: no scene called \"{roomScene}\" in the build list.");
                        return;
                    }
                    while (!load.isDone) await Awaitable.NextFrameAsync(_lifetime.Token);
                    _room = FindAnyObjectByType<RoomBuilder>(FindObjectsInactive.Include);
                }
                if (_room == null)
                {
                    Debug.LogWarning($"[Purr] RoomTravel: the \"{roomScene}\" scene has no RoomBuilder.");
                    return;
                }
                _room.ExitRequested += Exit;
                _room.Hide();
            }
            catch (OperationCanceledException)
            {
                // Play mode stopped while loading.
            }
        }

        private void OnDestroy()
        {
            if (_room != null) _room.ExitRequested -= Exit;
            if (_reader != null) _reader.StandingOnChanged -= OnStandingOnChanged;
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
        }

        /// <summary>Goes through <paramref name="door"/> into its room. Ignored while already travelling.</summary>
        public async void Enter(ProjectDoor door)
        {
            if (_travelling || _door != null || door?.Entry == null) return;
            if (_room == null)
            {
                Debug.LogWarning("[Purr] RoomTravel: the room isn't loaded (yet).");
                return;
            }
            var player = GameObject.FindWithTag(PlayerTrigger.PlayerTag);
            if (player == null) return;

            _travelling = true;
            try
            {
                var ct = _lifetime.Token;
                await _fade.Out(fadeSeconds, ct);

                // The room's +Z runs into the door, the way you were walking, so the view carries straight on.
                _room.transform.SetPositionAndRotation(roomOrigin, Quaternion.LookRotation(-door.transform.forward, Vector3.up));
                _room.Show(door.Entry, door.Colour);
                _door = door;
                Teleport(player, _room.ArrivalPoint);
                Armed(true);

                await _fade.In(fadeSeconds, ct);
                _travelling = false;

                await Refresh(door.Entry);
            }
            catch (OperationCanceledException)
            {
                // Play mode stopped mid-fade.
            }
            finally
            {
                _travelling = false;
            }
        }

        /// <summary>Leaves the room for the corridor, in front of the door you came in by.</summary>
        public async void Exit()
        {
            if (_travelling || _door == null) return;
            var player = GameObject.FindWithTag(PlayerTrigger.PlayerTag);
            if (player == null) return;

            _travelling = true;
            try
            {
                var ct = _lifetime.Token;
                await _fade.Out(fadeSeconds, ct);
                Teleport(player, _door.ReturnPoint);
                _door = null;
                Armed(false);
                _room.Hide();
                _hud.Hide();
                await _fade.In(fadeSeconds, ct);
            }
            catch (OperationCanceledException)
            {
                // Play mode stopped mid-fade.
            }
            finally
            {
                _travelling = false;
            }
        }

        /// <summary>Shooting and the crosshair are for rooms only.</summary>
        private void Armed(bool inRoom)
        {
            _hud.ShowCrosshair(inRoom);
            if (_shooter == null) return;
            _shooter.Room = inRoom ? _room : null;
            _shooter.enabled = inRoom;
        }

        private void OnStandingOnChanged(TaskCrate crate)
        {
            var card = crate != null ? crate.Card : null;
            if (card == null) _hud.Hide();
            else _hud.Show(card, crate.Colour);
        }

        private async Task Refresh(CorridorEntry entry)
        {
            var service = PurrService.Instance;
            if (service == null) return;

            var prefix = entry.Project.Prefix;
            var ok = await service.RefreshTasksAsync(prefix);

            // You may have walked out (or into another room) while it was fetching.
            if (_room == null || _room.Entry != entry) return;
            if (!ok)
            {
                _room.Status("Offline: showing cached tasks");
                return;
            }
            var diff = _room.Apply(entry);
            Debug.Log($"[Purr] {prefix} room: {entry.Tasks.Count} tasks, {diff}");
        }

        /// <summary>
        /// Moves the player without the CharacterController undoing it, and tells Cinemachine, so the camera jumps
        /// with them instead of swooping across the map. The player keeps facing the same way.
        /// </summary>
        private static void Teleport(GameObject player, Vector3 position)
        {
            var root = player.transform;
            var delta = position - root.position;

            var controller = player.GetComponent<CharacterController>();
            var wasEnabled = controller != null && controller.enabled;
            if (controller != null) controller.enabled = false;
            root.position = position;
            Physics.SyncTransforms();
            if (controller != null) controller.enabled = wasEnabled;

            // The follow camera tracks PlayerCameraRoot, a child of the player.
            CinemachineCore.OnTargetObjectWarped(root, delta);
            foreach (Transform child in root) CinemachineCore.OnTargetObjectWarped(child, delta);
        }
    }
}
