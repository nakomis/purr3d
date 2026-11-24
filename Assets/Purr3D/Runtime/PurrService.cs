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

        public PurrSettings Settings { get; private set; }
        public PurrClient Client { get; private set; }

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

            Settings = PurrSettings.Load();
            Client = new PurrClient(new UnityWebRequestTransport(Settings.BaseUrl, Settings.ApiKey));

            // Never log the key itself.
            Debug.Log($"[Purr] {Settings.Environment} at {Settings.BaseUrl}; API key {(Settings.HasApiKey ? "found" : "MISSING: run scripts/fetch-api-key.sh")}");
        }

        private void Start()
        {
            if (loadCorridorOnStart) _ = LoadCorridorAsync();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            _lifetime?.Cancel();
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
