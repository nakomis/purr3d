// Startup: every project and every task, fetched a few projects at a time, then put in corridor order.
// No ConfigureAwait(false) anywhere, so in Unity every continuation stays on the main thread (UnityWebRequest needs it).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Purr3D.Core
{
    /// <summary>How far the load has got: projects whose tasks have arrived (or failed), out of all of them.</summary>
    public readonly struct LoadProgress
    {
        public readonly int Done;
        public readonly int Total;

        public LoadProgress(int done, int total)
        {
            Done = done;
            Total = total;
        }

        public override string ToString() => $"{Done}/{Total}";
    }

    public static class CorridorLoader
    {
        /// <summary>Six requests in flight: quick, without hammering one Lambda.</summary>
        public const int DefaultMaxConcurrent = 6;

        /// <summary>
        /// Fetches the projects, then each project's tasks, and returns them in corridor order. A failure for one
        /// project marks that entry <see cref="CorridorEntry.LoadFailed"/>; a failure fetching the project list
        /// throws, because there's no corridor without it.
        /// </summary>
        /// <param name="failed">Called once per project whose tasks couldn't be fetched (for logging).</param>
        public static async Task<List<CorridorEntry>> LoadAsync(
            PurrClient client,
            int maxConcurrent = DefaultMaxConcurrent,
            IProgress<LoadProgress> progress = null,
            Action<PurrProject, PurrApiException> failed = null,
            CancellationToken cancellationToken = default)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (maxConcurrent < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrent));

            var projects = await client.GetProjectsAsync(cancellationToken);
            var total = projects.Count;
            var done = 0;
            progress?.Report(new LoadProgress(0, total));

            using (var gate = new SemaphoreSlim(maxConcurrent))
            {
                var loads = new List<Task<CorridorEntry>>();
                foreach (var project in projects)
                {
                    loads.Add(LoadOne(project));
                }
                var entries = await Task.WhenAll(loads);
                return CorridorOrder.Order(entries);

                async Task<CorridorEntry> LoadOne(PurrProject project)
                {
                    // Outside the try: a cancelled wait never took a slot, so it mustn't release one.
                    await gate.WaitAsync(cancellationToken);
                    try
                    {
                        var tasks = await client.GetTasksAsync(project.Prefix, cancellationToken);
                        return new CorridorEntry(project, tasks);
                    }
                    catch (PurrApiException e)
                    {
                        failed?.Invoke(project, e);
                        return CorridorEntry.Failed(project);
                    }
                    finally
                    {
                        gate.Release();
                        progress?.Report(new LoadProgress(Interlocked.Increment(ref done), total));
                    }
                }
            }
        }
    }
}
