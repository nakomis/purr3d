using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Purr3D.Core;
using UnityEngine.Networking;

namespace Purr3D
{
    /// <summary>
    /// <see cref="IPurrTransport"/> over UnityWebRequest. It runs on Unity's main thread, and so do the continuations,
    /// so callers can touch GameObjects straight after awaiting.
    /// </summary>
    public sealed class UnityWebRequestTransport : IPurrTransport
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly int _timeoutSeconds;

        public UnityWebRequestTransport(string baseUrl, string apiKey, int timeoutSeconds = 15)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
            _timeoutSeconds = timeoutSeconds;
        }

        public Task<PurrResponse> SendAsync(string method, string path, string jsonBody, CancellationToken cancellationToken)
        {
            var request = new UnityWebRequest(_baseUrl + path, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = _timeoutSeconds,
            };
            if (jsonBody != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("x-api-key", _apiKey);

            // A TaskCompletionSource rather than awaiting the operation directly, so this works on any Unity version.
            var completion = new TaskCompletionSource<PurrResponse>();
            var registration = cancellationToken.Register(() => request.Abort());
            request.SendWebRequest().completed += _ =>
            {
                registration.Dispose();
                // ProtocolError still has a status and a body (Purr's error envelope), so keep it.
                var answered = request.result == UnityWebRequest.Result.Success
                               || request.result == UnityWebRequest.Result.ProtocolError;
                var response = answered
                    ? new PurrResponse((int)request.responseCode, request.downloadHandler.text)
                    : new PurrResponse(0, null);
                request.Dispose();
                if (cancellationToken.IsCancellationRequested) completion.TrySetCanceled(cancellationToken);
                else completion.TrySetResult(response);
            };
            return completion.Task;
        }
    }
}
