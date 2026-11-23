using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Purr3D.Core;

namespace Purr3D.Core.Tests
{
    public class CorridorLoaderTests
    {
        /// Answers by path, with a short delay so requests genuinely overlap, and records the peak in flight.
        private sealed class RoutingTransport : IPurrTransport
        {
            private readonly Dictionary<string, PurrResponse> _routes = new Dictionary<string, PurrResponse>();
            private int _inFlight;
            public int PeakInFlight;
            public readonly ConcurrentQueue<string> Paths = new ConcurrentQueue<string>();

            public RoutingTransport Route(string path, int status, string body)
            {
                _routes[path] = new PurrResponse(status, body);
                return this;
            }

            public async Task<PurrResponse> SendAsync(string method, string path, string jsonBody, CancellationToken cancellationToken)
            {
                Paths.Enqueue(path);
                var now = Interlocked.Increment(ref _inFlight);
                int peak;
                while (now > (peak = PeakInFlight) && Interlocked.CompareExchange(ref PeakInFlight, now, peak) != peak) { }
                try
                {
                    await Task.Delay(15, cancellationToken);
                    return _routes.TryGetValue(path, out var response) ? response : new PurrResponse(404, "{}");
                }
                finally
                {
                    Interlocked.Decrement(ref _inFlight);
                }
            }
        }

        /// IProgress that records synchronously (Progress<T> posts to a thread pool and can arrive late).
        private sealed class Recorder : IProgress<LoadProgress>
        {
            public readonly ConcurrentQueue<LoadProgress> Reports = new ConcurrentQueue<LoadProgress>();
            public void Report(LoadProgress value) => Reports.Enqueue(value);
        }

        private static string Projects(params string[] prefixes)
        {
            var items = new List<string>();
            foreach (var p in prefixes) items.Add($@"{{""id"": ""id-{p}"", ""prefix"": ""{p}"", ""name"": ""{p} project"", ""states"": []}}");
            return $@"{{""items"": [{string.Join(",", items)}]}}";
        }

        private static string Tasks(string prefix, params string[] updatedAts)
        {
            var items = new List<string>();
            for (var i = 0; i < updatedAts.Length; i++)
                items.Add($@"{{""ref"": ""{prefix}-{i + 1}"", ""stateId"": ""s"", ""updatedAt"": ""{updatedAts[i]}""}}");
            return $@"{{""items"": [{string.Join(",", items)}], ""cursor"": null}}";
        }

        private static string TasksPath(string prefix) => $"/projects/{prefix}/tasks?all=true&limit={PurrClient.PageSize}";

        [Test]
        public async Task LoadsEveryProjectsTasksAndOrdersByLatestActivity()
        {
            var transport = new RoutingTransport()
                .Route("/projects", 200, Projects("HOME", "BOOT", "BCON"))
                .Route(TasksPath("HOME"), 200, Tasks("HOME", "2026-10-01T00:00:00Z", "2026-11-01T00:00:00Z"))
                .Route(TasksPath("BOOT"), 200, Tasks("BOOT", "2026-11-22T12:00:00Z"))
                .Route(TasksPath("BCON"), 200, Tasks("BCON"));

            var corridor = await CorridorLoader.LoadAsync(new PurrClient(transport));

            Assert.That(corridor.ConvertAll(e => e.Project.Prefix), Is.EqualTo(new[] { "BOOT", "HOME", "BCON" }));
            Assert.That(corridor[1].Tasks.ConvertAll(t => t.Ref), Is.EqualTo(new[] { "HOME-1", "HOME-2" }), "tasks are cached for the room");
            Assert.That(corridor.TrueForAll(e => !e.LoadFailed), Is.True);
        }

        [Test]
        public async Task OneProjectFailingStillGivesItADoor()
        {
            var transport = new RoutingTransport()
                .Route("/projects", 200, Projects("HOME", "BOOT"))
                .Route(TasksPath("HOME"), 200, Tasks("HOME", "2026-11-01T00:00:00Z"))
                .Route(TasksPath("BOOT"), 500, @"{""error"": {""code"": ""INTERNAL"", ""message"": ""boom""}}");
            var failures = new List<string>();

            var corridor = await CorridorLoader.LoadAsync(new PurrClient(transport),
                failed: (project, e) => failures.Add($"{project.Prefix} {e.Status}"));

            Assert.That(corridor.ConvertAll(e => e.Project.Prefix), Is.EqualTo(new[] { "HOME", "BOOT" }));
            Assert.That(corridor[1].LoadFailed, Is.True);
            Assert.That(failures, Is.EqualTo(new[] { "BOOT 500" }));
        }

        [Test]
        public void NoProjectListMeansNoCorridor()
        {
            var transport = new RoutingTransport().Route("/projects", 403, @"{""error"": {""code"": ""FORBIDDEN"", ""message"": ""bad key""}}");
            var ex = Assert.ThrowsAsync<PurrApiException>(() => CorridorLoader.LoadAsync(new PurrClient(transport)));
            Assert.That(ex.Status, Is.EqualTo(403));
        }

        [Test]
        public async Task NeverHasMoreThanTheLimitInFlight()
        {
            var prefixes = new string[20];
            for (var i = 0; i < prefixes.Length; i++) prefixes[i] = "P" + i;
            var transport = new RoutingTransport().Route("/projects", 200, Projects(prefixes));
            foreach (var p in prefixes) transport.Route(TasksPath(p), 200, Tasks(p, "2026-11-01T00:00:00Z"));

            var corridor = await CorridorLoader.LoadAsync(new PurrClient(transport), maxConcurrent: 3);

            Assert.That(corridor, Has.Count.EqualTo(20));
            Assert.That(transport.PeakInFlight, Is.LessThanOrEqualTo(3));
            Assert.That(transport.PeakInFlight, Is.GreaterThan(1), "requests should overlap");
        }

        [Test]
        public async Task ReportsProgressUpToTheTotal()
        {
            var transport = new RoutingTransport()
                .Route("/projects", 200, Projects("A", "B", "C"))
                .Route(TasksPath("A"), 200, Tasks("A"))
                .Route(TasksPath("B"), 200, Tasks("B"))
                .Route(TasksPath("C"), 200, Tasks("C"));
            var recorder = new Recorder();

            await CorridorLoader.LoadAsync(new PurrClient(transport), progress: recorder);

            var reports = new List<LoadProgress>(recorder.Reports);
            Assert.That(reports[0], Is.EqualTo(new LoadProgress(0, 3)));
            Assert.That(reports, Has.Count.EqualTo(4));
            Assert.That(reports.Exists(r => r.Done == 3 && r.Total == 3), Is.True);
        }

        [Test]
        public async Task AnEmptyPurrIsAnEmptyCorridor()
        {
            var transport = new RoutingTransport().Route("/projects", 200, @"{""items"": []}");
            Assert.That(await CorridorLoader.LoadAsync(new PurrClient(transport)), Is.Empty);
        }

        [Test]
        public void RejectsANonsenseLimit()
        {
            var client = new PurrClient(new RoutingTransport());
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CorridorLoader.LoadAsync(client, maxConcurrent: 0));
        }
    }
}
