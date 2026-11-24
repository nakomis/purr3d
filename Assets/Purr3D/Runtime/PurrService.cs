using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Purr3D.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Purr3D
{
    /// <summary>
    /// The scene's way into Purr. Put one on a GameObject in the first scene; it survives scene loads.
    ///
    /// On start it loads the corridor: every project and all their tasks, in corridor order (see
    /// <see cref="CorridorOrder"/>). Listen for <see cref="CorridorLoaded"/>, or read <see cref="Corridor"/> once
    /// <see cref="IsLoading"/> is false. Rooms read their tasks from the cache with <see cref="TasksFor"/>.
    ///
    /// Failures arrive as <see cref="PurrApiException"/>, whose Subject is the prefix or task ref, so a caller can
    /// roll back the right object.
    /// </summary>
    public sealed class PurrService : MonoBehaviour
    {
        public static PurrService Instance { get; private set; }

        [Tooltip("Fetch every project and its tasks on start, for the corridor and the rooms.")]
        [SerializeField] private bool loadCorridorOnStart = true;

        [Tooltip("Projects fetched at once during the startup load.")]
        [SerializeField, Range(1, 16)] private int maxConcurrentRequests = CorridorLoader.DefaultMaxConcurrent;

        [Header("Testing")]
        [Tooltip("Send an API key Purr won't accept, from now on: shots are refused and their crates snap back. Works live in Play mode; untick to carry on.")]
        [SerializeField] private bool sendWrongApiKey;

        private UnityWebRequestTransport _transport;

        /// <summary>Where Purr is and the key in use (see <see cref="PurrSettings"/>). Never log <c>ApiKey</c>.</summary>
        public PurrSecretsFile Settings { get; private set; }
        public PurrClient Client { get; private set; }

        /// <summary>
        /// Why a key is needed before the corridor can load: none was found, or Purr refused the one in use. Null
        /// when all's well. <see cref="ApiKeyScreen"/> shows itself while this is set (PURR3D-14).
        /// </summary>
        public string KeyProblem { get; private set; }

        /// <summary>Raised with <see cref="KeyProblem"/> when a key is needed.</summary>
        public event Action<string> KeyNeeded;

        /// <summary>Projects' icons, downloaded on request and kept (PURR3D-13).</summary>
        public ProjectIcons Icons { get; private set; }

        /// <summary>Doors in order, most recently active first. Empty until loaded.</summary>
        public IReadOnlyList<CorridorEntry> Corridor => _corridor;

        public bool IsLoading { get; private set; }
        public LoadProgress Progress { get; private set; }

        /// <summary>Why the last load failed, or null.</summary>
        public string LoadError { get; private set; }

        public event Action<LoadProgress> CorridorProgress;
        public event Action<IReadOnlyList<CorridorEntry>> CorridorLoaded;
        public event Action<string> CorridorFailed;

        private List<CorridorEntry> _corridor = new List<CorridorEntry>();
        private CancellationTokenSource _lifetime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _lifetime = new CancellationTokenSource();

            Configure(PurrSettings.Load());
            ApiKeyScreen.Create(transform, this); // shows itself when a key is needed (PURR3D-14)

            // Never log the key itself.
            Debug.Log($"[Purr] {Settings.Environment} at {Settings.BaseUrl}; API key " +
                      (Settings.HasApiKey ? "found" : $"MISSING: enter one in the game, or run scripts/fetch-api-key.sh ({PurrSettings.SecretsPath})"));
        }

        private void Start()
        {
            if (!Settings.HasApiKey) NeedKey("Purr3D needs an API key to reach Purr.");
            else if (loadCorridorOnStart) _ = LoadCorridorAsync();
        }

        /// <summary>Points the client, transport and icons at <paramref name="settings"/>.</summary>
        private void Configure(PurrSecretsFile settings)
        {
            Settings = settings;
            _transport = new UnityWebRequestTransport(settings.BaseUrl, settings.ApiKey) { SpoilKey = sendWrongApiKey };
            Client = new PurrClient(_transport);
            Icons?.Dispose();
            Icons = new ProjectIcons(settings.BaseUrl, _lifetime.Token);
        }

        private void NeedKey(string reason)
        {
            KeyProblem = reason;
            KeyNeeded?.Invoke(reason);
        }

        /// <summary>
        /// Asks Purr who <paramref name="candidate"/>'s key belongs to, without using or saving it: the actor (e.g.
        /// "purr3d"). Throws <see cref="PurrApiException"/> (401 for a bad key, 0 for no connection).
        /// </summary>
        public Task<string> CheckKeyAsync(PurrSecretsFile candidate)
        {
            var client = new PurrClient(new UnityWebRequestTransport(candidate.BaseUrl, candidate.ApiKey));
            return client.GetMeAsync(_lifetime?.Token ?? CancellationToken.None);
        }

        /// <summary>
        /// Switches to <paramref name="settings"/> (a key <see cref="CheckKeyAsync"/> accepted), saves it for next
        /// time, and loads the corridor again with it.
        /// </summary>
        public void UseKey(PurrSecretsFile settings)
        {
            PurrSettings.Save(settings);
            Configure(settings);
            KeyProblem = null;
            _corridor = new List<CorridorEntry>();
            Debug.Log($"[Purr] now using {settings} (saved to {PurrSettings.SecretsPath})");
            _ = LoadCorridorAsync();
        }

        /// <summary>Deletes the saved key file. The key in use stays in use until the game quits or another is entered.</summary>
        public bool ForgetSavedKey()
        {
            var forgot = PurrSettings.Forget();
            Debug.Log(forgot ? $"[Purr] forgot the saved key ({PurrSettings.SecretsPath})" : "[Purr] no saved key to forget");
            return forgot;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            _lifetime?.Cancel();
            Icons?.Dispose();
            Icons = null;
            _lifetime?.Dispose();
            _lifetime = null;
        }

        /// <summary>
        /// (Re)loads every project and its tasks. Safe to call again; a call while loading is ignored.
        /// Never throws: failures go to <see cref="CorridorFailed"/> and <see cref="LoadError"/>.
        /// </summary>
        public async Task LoadCorridorAsync()
        {
            if (IsLoading || _lifetime == null) return;
            IsLoading = true;
            LoadError = null;
            var clock = Stopwatch.StartNew();
            try
            {
                var corridor = await CorridorLoader.LoadAsync(
                    Client,
                    maxConcurrentRequests,
                    new InlineProgress(ReportProgress),
                    (project, e) => Debug.LogWarning($"[Purr] {project.Prefix}: {e.Message}"),
                    _lifetime.Token);
                _corridor = corridor;
                IsLoading = false;
                Debug.Log($"[Purr] corridor: {corridor.Count} projects in {clock.Elapsed.TotalSeconds:0.0}s: " +
                          string.Join(", ", corridor.ConvertAll(e => e.Project.Prefix)));
                CorridorLoaded?.Invoke(_corridor);
            }
            catch (OperationCanceledException)
            {
                IsLoading = false; // shutting down
            }
            catch (PurrApiException e) when ((e.Status == 401 || e.Status == 403) && !sendWrongApiKey)
            {
                // The key itself is the problem: ask for another rather than showing a dead corridor.
                IsLoading = false;
                Debug.LogWarning($"[Purr] {e.Message}");
                NeedKey("Purr didn't accept the saved key. Enter a new one.");
            }
            catch (Exception e)
            {
                IsLoading = false;
                LoadError = e is PurrApiException ? e.Message : $"{e.GetType().Name}: {e.Message}";
                if (e is PurrApiException) Debug.LogWarning($"[Purr] {LoadError}");
                else Debug.LogException(e);
                CorridorFailed?.Invoke(LoadError);
            }
        }

        /// <summary>A project's cached tasks, or null when the prefix isn't in the corridor.</summary>
        public CorridorEntry EntryFor(string prefix) => _corridor.Find(e => e.Project.Prefix == prefix);

        /// <summary>A project's cached tasks (empty if its fetch failed), or null when the prefix isn't known.</summary>
        public List<PurrTask> TasksFor(string prefix) => EntryFor(prefix)?.Tasks;

        /// <summary>A project's tasks were fetched again (see <see cref="RefreshTasksAsync"/>).</summary>
        public event Action<CorridorEntry> TasksRefreshed;

        private readonly Dictionary<string, Task<bool>> _refreshing = new Dictionary<string, Task<bool>>();

        /// <summary>
        /// Fetches one project's tasks again and swaps them into its <see cref="CorridorEntry"/>. True when it
        /// worked; false (cache untouched, warning logged) when it didn't. Concurrent calls for a prefix share one
        /// request. Never throws, except for cancellation at shutdown.
        /// </summary>
        public Task<bool> RefreshTasksAsync(string prefix)
        {
            if (prefix == null || _lifetime == null) return Task.FromResult(false);
            if (_refreshing.TryGetValue(prefix, out var running)) return running;
            var refresh = Refresh(prefix);
            if (!refresh.IsCompleted) _refreshing[prefix] = refresh;
            return refresh;
        }

        private async Task<bool> Refresh(string prefix)
        {
            try
            {
                var entry = EntryFor(prefix);
                if (entry == null) return false;
                var tasks = await Client.GetTasksAsync(prefix, _lifetime.Token);
                entry.Replace(tasks);
                TasksRefreshed?.Invoke(entry);
                return true;
            }
            catch (PurrApiException e)
            {
                Debug.LogWarning($"[Purr] {prefix}: {e.Message}");
                return false;
            }
            finally
            {
                _refreshing.Remove(prefix);
            }
        }

        private void Update()
        {
            if (_transport != null && _transport.SpoilKey != sendWrongApiKey)
            {
                _transport.SpoilKey = sendWrongApiKey;
                Debug.Log(sendWrongApiKey ? "[Purr] Testing: sending a wrong API key" : "[Purr] Back to the real API key");
            }
        }

        /// <summary>
        /// Moves a task to <paramref name="state"/> and returns Purr's copy of it. Throws <see cref="PurrApiException"/>
        /// when Purr refuses or can't be reached.
        /// </summary>
        public Task<PurrTask> SetStateAsync(PurrTask task, PurrState state) =>
            Client.SetStateAsync(task.Ref, state.Id, _lifetime?.Token ?? CancellationToken.None);

        /// <summary>Moves a task one step along (see <see cref="StateCycle"/>). Returns null when it stays put.</summary>
        public async Task<PurrTask> AdvanceAsync(PurrProject project, PurrTask task)
        {
            var next = StateCycle.Next(project, task.StateId);
            if (next == null) return null;
            return await Client.SetStateAsync(task.Ref, next.Id);
        }

        private void ReportProgress(LoadProgress progress)
        {
            Progress = progress;
            CorridorProgress?.Invoke(progress);
        }

        /// <summary>
        /// Reports straight away. Progress&lt;T&gt; would post each report back through the sync context, so the
        /// last one could land after CorridorLoaded. The loader's continuations already run on the main thread.
        /// </summary>
        private sealed class InlineProgress : IProgress<LoadProgress>
        {
            private readonly Action<LoadProgress> _report;
            public InlineProgress(Action<LoadProgress> report) => _report = report;
            public void Report(LoadProgress value) => _report(value);
        }
    }
}
