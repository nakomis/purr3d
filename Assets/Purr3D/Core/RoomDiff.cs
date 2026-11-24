// What changed in a project between two fetches, by ref: the room's log line on re-entry, and the shape
// PURR3D-7's shot will reuse to slide one crate into its new zone.

using System;
using System.Collections.Generic;

namespace Purr3D.Core
{
    public sealed class RoomDiff
    {
        /// <summary>Tasks that weren't there before.</summary>
        public List<PurrTask> Added = new List<PurrTask>();

        /// <summary>Refs that have gone (deleted, or moved to another project).</summary>
        public List<string> Removed = new List<string>();

        /// <summary>Tasks now in a different state.</summary>
        public List<PurrTask> Moved = new List<PurrTask>();

        /// <summary>Tasks in the same state whose version changed (a new title, say).</summary>
        public List<PurrTask> Updated = new List<PurrTask>();

        public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && Moved.Count == 0 && Updated.Count == 0;

        public override string ToString() =>
            IsEmpty ? "no changes" : $"{Added.Count} new, {Moved.Count} moved, {Updated.Count} updated, {Removed.Count} gone";

        public static RoomDiff Compute(IEnumerable<PurrTask> before, IEnumerable<PurrTask> after)
        {
            var diff = new RoomDiff();
            var old = new Dictionary<string, PurrTask>(StringComparer.Ordinal);
            if (before != null)
            {
                foreach (var task in before)
                {
                    if (task?.Ref != null && !old.ContainsKey(task.Ref)) old[task.Ref] = task;
                }
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (after != null)
            {
                foreach (var task in after)
                {
                    if (task?.Ref == null || !seen.Add(task.Ref)) continue;
                    if (!old.TryGetValue(task.Ref, out var was)) diff.Added.Add(task);
                    else if (!string.Equals(was.StateId, task.StateId, StringComparison.Ordinal)) diff.Moved.Add(task);
                    else if (was.Version != task.Version) diff.Updated.Add(task);
                }
            }

            foreach (var reference in old.Keys)
            {
                if (!seen.Contains(reference)) diff.Removed.Add(reference);
            }
            diff.Removed.Sort(string.CompareOrdinal);
            return diff;
        }
    }
}
