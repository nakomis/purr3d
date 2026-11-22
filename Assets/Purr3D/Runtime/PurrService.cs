using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Purr3D.Core;
using UnityEngine;

namespace Purr3D
{
    /// <summary>
    /// The scene's way into Purr. Put one on a GameObject in the first scene; it survives scene loads.
    ///
    ///   var projects = await PurrService.Instance.Client.GetProjectsAsync();
    ///
    /// Failures arrive as <see cref="PurrApiException"/>, whose Subject is the prefix or task ref, so a caller can
    /// roll back the right object.
    /// </summary>
    public sealed class PurrService : MonoBehaviour
    {
        public static PurrService Instance { get; private set; }

        [Tooltip("Log the project list on start: a quick check that the key and URL work.")]
        [SerializeField] private bool logProjectsOnStart = true;

        public PurrSettings Settings { get; private set; }
        public PurrClient Client { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Settings = PurrSettings.Load();
            Client = new PurrClient(new UnityWebRequestTransport(Settings.BaseUrl, Settings.ApiKey));

            // Never log the key itself.
            Debug.Log($"[Purr] {Settings.Environment} at {Settings.BaseUrl}; API key {(Settings.HasApiKey ? "found" : "MISSING: run scripts/fetch-api-key.sh")}");
        }

        private async void Start()
        {
            if (!logProjectsOnStart) return;
            try
            {
                List<PurrProject> projects = await Client.GetProjectsAsync();
                Debug.Log($"[Purr] {projects.Count} projects: {string.Join(", ", projects.ConvertAll(p => p.Prefix))}");
            }
            catch (PurrApiException e)
            {
                Debug.LogWarning($"[Purr] {e.Message}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Moves a task one step along (see <see cref="StateCycle"/>). Returns null when it stays put.</summary>
        public async Task<PurrTask> AdvanceAsync(PurrProject project, PurrTask task)
        {
            var next = StateCycle.Next(project, task.StateId);
            if (next == null) return null;
            return await Client.SetStateAsync(task.Ref, next.Id);
        }
    }
}
