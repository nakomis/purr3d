using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Purr3D.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Purr3D
{
    /// <summary>
    /// Downloads projects' icons (<see cref="PurrProject.Icon"/>, e.g. "/icons/HOME.png") a few at a time and keeps
    /// them, one texture per URL. Icons are public files on Purr's site, so no API key is sent. <see cref="PurrService"/>
    /// owns one: use <c>PurrService.Instance.Icons</c>.
    /// </summary>
    public sealed class ProjectIcons : IDisposable
    {
        private readonly string _apiBaseUrl;
        private readonly CancellationToken _lifetime;
        private readonly SemaphoreSlim _gate;
        private readonly Dictionary<string, Task<Texture2D>> _byUrl = new Dictionary<string, Task<Texture2D>>(StringComparer.Ordinal);
        private readonly List<Texture2D> _loaded = new List<Texture2D>();

        public ProjectIcons(string apiBaseUrl, CancellationToken lifetime, int maxConcurrent = 4)
        {
            _apiBaseUrl = apiBaseUrl;
            _lifetime = lifetime;
            _gate = new SemaphoreSlim(Math.Max(1, maxConcurrent));
        }

        /// <summary>
        /// The project's icon, or null when it has none or it couldn't be fetched (a warning is logged once per URL).
        /// Never throws, except for cancellation when Play mode stops.
        /// </summary>
        public Task<Texture2D> GetAsync(PurrProject project)
        {
            var url = IconUrl.Resolve(_apiBaseUrl, project?.Icon);
            if (url == null) return Task.FromResult<Texture2D>(null);
            if (!_byUrl.TryGetValue(url, out var load))
            {
                load = Fetch(url);
                _byUrl[url] = load;
            }
            return load;
        }

        private async Task<Texture2D> Fetch(string url)
        {
            await _gate.WaitAsync(_lifetime);
            try
            {
                using (var request = UnityWebRequestTexture.GetTexture(url, nonReadable: true))
                {
                    request.timeout = 15;
                    var operation = request.SendWebRequest();
                    while (!operation.isDone) await Awaitable.NextFrameAsync(_lifetime);

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[Purr] icon {url}: {request.error}");
                        return null;
                    }
                    var texture = DownloadHandlerTexture.GetContent(request);
                    texture.name = url;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    _loaded.Add(texture);
                    return texture;
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Dispose()
        {
            foreach (var texture in _loaded)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
            }
            _loaded.Clear();
            _byUrl.Clear();
            _gate.Dispose();
        }
    }
}
