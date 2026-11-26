// The Purr REST API, behind a transport port: Unity supplies UnityWebRequest, tests supply a fake.
// The same rule as Purr itself: the outside world stays behind an interface.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Purr3D.Core
{
    /// <summary>An HTTP answer. <see cref="Status"/> 0 means no answer at all (network failure or timeout).</summary>
    public sealed class PurrResponse
    {
        public readonly int Status;
        public readonly string Body;

        public PurrResponse(int status, string body)
        {
            Status = status;
            Body = body;
        }

        public bool IsSuccess => Status >= 200 && Status < 300;
    }

    /// <summary>
    /// Sends one request. Paths are relative to the API root ("/projects"). Implementations add the base URL
    /// and the x-api-key header, and never throw for HTTP errors: they return the status instead.
    /// </summary>
    public interface IPurrTransport
    {
        Task<PurrResponse> SendAsync(string method, string path, string jsonBody, CancellationToken cancellationToken);
    }

    /// <summary>
    /// A Purr call failed. <see cref="Subject"/> is the prefix or task ref it was about, so the game can roll back
    /// the right object. <see cref="Status"/> is the HTTP status, or 0 when there was no answer.
    /// </summary>
    public sealed class PurrApiException : Exception
    {
        public readonly string Operation;
        public readonly string Subject;
        public readonly int Status;
        public readonly string Code;

        public PurrApiException(string operation, string subject, int status, string code, string message, Exception inner = null)
            : base($"{operation} {subject} failed ({status}): {message}", inner)
        {
            Operation = operation;
            Subject = subject;
            Status = status;
            Code = code;
        }
    }

    public sealed class PurrClient
    {
        /// <summary>Tasks per page. The API allows up to 1000; 100 keeps responses small.</summary>
        public const int PageSize = 100;

        /// <summary>Insurance against a server that never stops returning a cursor: 200 x 100 tasks is plenty.</summary>
        public const int MaxPages = 200;

        private readonly IPurrTransport _transport;

        public PurrClient(IPurrTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        /// <summary>
        /// <c>GET /me</c>: the actor the key belongs to, e.g. "purr3d". A cheap way to check a key before keeping it:
        /// a bad key throws <see cref="PurrApiException"/> with status 401.
        /// </summary>
        public async Task<string> GetMeAsync(CancellationToken cancellationToken = default)
        {
            var response = await Send("GetMe", null, "GET", "/me", null, cancellationToken);
            return Parse("GetMe", null, response, PurrJson.ParseActor);
        }

        /// <summary><c>GET /projects</c>, without archived projects.</summary>
        public async Task<List<PurrProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
        {
            var response = await Send("GetProjects", null, "GET", "/projects", null, cancellationToken);
            var projects = Parse("GetProjects", null, response, PurrJson.ParseProjectList);
            projects.RemoveAll(p => p.Archived);
            return projects;
        }

        /// <summary>Every task in a project, open and closed, following cursors.</summary>
        public async Task<List<PurrTask>> GetTasksAsync(string prefix, CancellationToken cancellationToken = default)
        {
            var tasks = new List<PurrTask>();
            string cursor = null;
            for (var page = 1; page <= MaxPages; page++)
            {
                var path = $"/projects/{Uri.EscapeDataString(prefix)}/tasks?all=true&limit={PageSize}";
                if (cursor != null) path += "&cursor=" + Uri.EscapeDataString(cursor);

                var response = await Send("GetTasks", prefix, "GET", path, null, cancellationToken);
                var result = Parse("GetTasks", prefix, response, PurrJson.ParseTaskPage);
                tasks.AddRange(result.Items);
                if (string.IsNullOrEmpty(result.Cursor)) return tasks;
                cursor = result.Cursor;
            }
            throw new PurrApiException("GetTasks", prefix, 0, null, $"gave up after {MaxPages} pages");
        }

        /// <summary><c>POST /tasks/{ref}/state</c>. Returns the updated task.</summary>
        public async Task<PurrTask> SetStateAsync(string taskRef, string stateId, CancellationToken cancellationToken = default)
        {
            // Serialised rather than formatted, so no state id can break the body.
            var body = JsonConvert.SerializeObject(new Dictionary<string, string> { { "state", stateId } });
            var path = $"/tasks/{Uri.EscapeDataString(taskRef)}/state";
            var response = await Send("SetState", taskRef, "POST", path, body, cancellationToken);
            return Parse("SetState", taskRef, response, PurrJson.ParseTask);
        }

        private async Task<PurrResponse> Send(string operation, string subject, string method, string path, string body,
            CancellationToken cancellationToken)
        {
            var response = await _transport.SendAsync(method, path, body, cancellationToken);
            if (response == null || response.Status == 0)
                throw new PurrApiException(operation, subject, 0, null, "no response (network or timeout)");
            if (!response.IsSuccess)
            {
                var message = "unexpected response";
                string code = null;
                if (PurrJson.TryParseError(response.Body, out var c, out var m))
                {
                    code = c;
                    message = $"{c}: {m}";
                }
                throw new PurrApiException(operation, subject, response.Status, code, message);
            }
            return response;
        }

        private static T Parse<T>(string operation, string subject, PurrResponse response, Func<string, T> parse)
        {
            try
            {
                return parse(response.Body);
            }
            catch (PurrFormatException e)
            {
                throw new PurrApiException(operation, subject, response.Status, null, e.Message, e);
            }
        }
    }
}
