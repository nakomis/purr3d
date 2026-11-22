using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Purr3D.Core;
using static Purr3D.Core.Tests.Fixtures;

namespace Purr3D.Core.Tests
{
    public class PurrClientTests
    {
        /// Replays canned responses and records every request.
        private sealed class FakeTransport : IPurrTransport
        {
            public readonly List<(string Method, string Path, string Body)> Requests = new List<(string, string, string)>();
            private readonly Queue<PurrResponse> _responses = new Queue<PurrResponse>();

            public FakeTransport Reply(int status, string body)
            {
                _responses.Enqueue(new PurrResponse(status, body));
                return this;
            }

            public Task<PurrResponse> SendAsync(string method, string path, string jsonBody, CancellationToken cancellationToken)
            {
                Requests.Add((method, path, jsonBody));
                return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : new PurrResponse(500, "{}"));
            }
        }

        private const string PageOne = @"{""items"": [{""ref"": ""HOME-1"", ""stateId"": ""a""}], ""cursor"": ""c/2+""}";
        private const string PageTwo = @"{""items"": [{""ref"": ""HOME-2"", ""stateId"": ""b""}], ""cursor"": null}";

        [Test]
        public async Task GetsProjectsAndDropsArchivedOnes()
        {
            var transport = new FakeTransport().Reply(200, ProjectList);
            var projects = await new PurrClient(transport).GetProjectsAsync();

            Assert.That(projects, Has.Count.EqualTo(2));
            Assert.That(projects.Exists(p => p.Archived), Is.False);
            Assert.That(transport.Requests[0], Is.EqualTo(("GET", "/projects", (string)null)));
        }

        [Test]
        public async Task FollowsCursorsUntilTheLastPage()
        {
            var transport = new FakeTransport().Reply(200, PageOne).Reply(200, PageTwo);
            var tasks = await new PurrClient(transport).GetTasksAsync("HOME");

            Assert.That(tasks.ConvertAll(t => t.Ref), Is.EqualTo(new[] { "HOME-1", "HOME-2" }));
            Assert.That(transport.Requests[0].Path, Is.EqualTo("/projects/HOME/tasks?all=true&limit=100"));
            Assert.That(transport.Requests[1].Path, Is.EqualTo("/projects/HOME/tasks?all=true&limit=100&cursor=c%2F2%2B"),
                "the cursor is URL-encoded");
        }

        [Test]
        public void GivesUpOnAServerThatNeverStopsPaging()
        {
            var transport = new FakeTransport();
            for (var i = 0; i < PurrClient.MaxPages + 5; i++) transport.Reply(200, PageOne);

            var ex = Assert.ThrowsAsync<PurrApiException>(() => new PurrClient(transport).GetTasksAsync("HOME"));
            Assert.That(ex.Operation, Is.EqualTo("GetTasks"));
            Assert.That(ex.Subject, Is.EqualTo("HOME"));
            Assert.That(transport.Requests, Has.Count.EqualTo(PurrClient.MaxPages));
        }

        [Test]
        public async Task SetsStateWithAJsonBody()
        {
            var transport = new FakeTransport().Reply(200, TaskBody);
            var task = await new PurrClient(transport).SetStateAsync("ADMIN-2", "s-test");

            Assert.That(task.StateId, Is.EqualTo("s-test"));
            var request = transport.Requests[0];
            Assert.That(request.Method, Is.EqualTo("POST"));
            Assert.That(request.Path, Is.EqualTo("/tasks/ADMIN-2/state"));
            Assert.That(Newtonsoft.Json.Linq.JObject.Parse(request.Body)["state"]?.ToString(), Is.EqualTo("s-test"));
        }

        [Test]
        public void SetStateEscapesAWeirdStateIdSafely()
        {
            var transport = new FakeTransport().Reply(200, TaskBody);
            Assert.DoesNotThrowAsync(() => new PurrClient(transport).SetStateAsync("ADMIN-2", "a\"b"));
            Assert.That(Newtonsoft.Json.Linq.JObject.Parse(transport.Requests[0].Body)["state"]?.ToString(), Is.EqualTo("a\"b"));
        }

        [Test]
        public void TurnsPurrErrorsIntoExceptionsCarryingTheSubject()
        {
            var transport = new FakeTransport().Reply(409, @"{""error"": {""code"": ""CONFLICT"", ""message"": ""version mismatch""}}");
            var ex = Assert.ThrowsAsync<PurrApiException>(() => new PurrClient(transport).SetStateAsync("HOME-7", "s"));

            Assert.That(ex.Operation, Is.EqualTo("SetState"));
            Assert.That(ex.Subject, Is.EqualTo("HOME-7"));
            Assert.That(ex.Status, Is.EqualTo(409));
            Assert.That(ex.Code, Is.EqualTo("CONFLICT"));
            Assert.That(ex.Message, Does.Contain("version mismatch"));
        }

        [Test]
        public void ReportsNetworkFailuresAsStatusZero()
        {
            var transport = new FakeTransport().Reply(0, null);
            var ex = Assert.ThrowsAsync<PurrApiException>(() => new PurrClient(transport).GetProjectsAsync());

            Assert.That(ex.Status, Is.EqualTo(0));
            Assert.That(ex.Message, Does.Contain("no response"));
        }

        [Test]
        public void ReportsAnUnparseableSuccessBody()
        {
            var transport = new FakeTransport().Reply(200, "<html>oops</html>");
            var ex = Assert.ThrowsAsync<PurrApiException>(() => new PurrClient(transport).GetProjectsAsync());
            Assert.That(ex.Status, Is.EqualTo(200));
        }

        [Test]
        public async Task EncodesPrefixesAndRefsInPaths()
        {
            var transport = new FakeTransport().Reply(200, PageTwo).Reply(200, TaskBody);
            var client = new PurrClient(transport);
            await client.GetTasksAsync("A B");
            await client.SetStateAsync("A B-1", "s");

            Assert.That(transport.Requests[0].Path, Does.StartWith("/projects/A%20B/tasks"));
            Assert.That(transport.Requests[1].Path, Is.EqualTo("/tasks/A%20B-1/state"));
        }
    }
}
