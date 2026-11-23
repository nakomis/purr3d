// The corridor's running order: the project with the most recent task activity nearest the start.
// A project's own updatedAt is no use here (most still carry the 14 Sep 2026 migration date), so its tasks decide.

using System;
using System.Collections.Generic;

namespace Purr3D.Core
{
    /// <summary>One door: a project, its tasks (cached for the room), and when it last saw activity.</summary>
    public sealed class CorridorEntry
    {
        public PurrProject Project;

        /// <summary>Every task, open and closed. Empty when the project has none or its fetch failed.</summary>
        public List<PurrTask> Tasks = new List<PurrTask>();

        /// <summary>The newest task's <see cref="PurrTask.UpdatedAt"/>, or null when nothing has a date.</summary>
        public DateTimeOffset? LatestActivity;

        /// <summary>The tasks couldn't be fetched. The door still appears, so the corridor matches Purr.</summary>
        public bool LoadFailed;

        public CorridorEntry() { }

        public CorridorEntry(PurrProject project, List<PurrTask> tasks)
        {
            Project = project;
            Tasks = tasks ?? new List<PurrTask>();
            LatestActivity = CorridorOrder.Latest(Tasks);
        }

        public static CorridorEntry Failed(PurrProject project) =>
            new CorridorEntry { Project = project, LoadFailed = true };

        public override string ToString() => $"{Project?.Prefix} ({LatestActivity?.ToString("u") ?? "no activity"})";
    }

    public static class CorridorOrder
    {
        /// <summary>The newest <see cref="PurrTask.UpdatedAt"/>, or null when no task has one.</summary>
        public static DateTimeOffset? Latest(IEnumerable<PurrTask> tasks)
        {
            DateTimeOffset? latest = null;
            if (tasks == null) return null;
            foreach (var task in tasks)
            {
                if (task?.UpdatedAt == null) continue;
                if (latest == null || task.UpdatedAt.Value > latest.Value) latest = task.UpdatedAt;
            }
            return latest;
        }

        /// <summary>
        /// Most recent activity first; ties by prefix. Projects with no dated activity (no tasks, or a failed
        /// fetch) come last, by prefix. Returns a new list.
        /// </summary>
        public static List<CorridorEntry> Order(IEnumerable<CorridorEntry> entries)
        {
            var ordered = new List<CorridorEntry>();
            if (entries == null) return ordered;
            foreach (var entry in entries)
            {
                if (entry?.Project != null) ordered.Add(entry);
            }
            ordered.Sort(Compare);
            return ordered;
        }

        private static int Compare(CorridorEntry a, CorridorEntry b)
        {
            var aActive = !a.LoadFailed && a.LatestActivity.HasValue;
            var bActive = !b.LoadFailed && b.LatestActivity.HasValue;
            if (aActive != bActive) return aActive ? -1 : 1;
            if (aActive)
            {
                var byTime = b.LatestActivity.Value.CompareTo(a.LatestActivity.Value);
                if (byTime != 0) return byTime;
            }
            // Prefixes are unique in Purr, so this makes the order total and List.Sort's instability harmless.
            return string.CompareOrdinal(a.Project.Prefix, b.Project.Prefix);
        }
    }
}
