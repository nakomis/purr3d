// Purr's domain, as plain C#. Mirrors packages/domain/src/types.ts in nakomis/purr.
// Engine-free and C# 9: compiled by Unity and by tests/Purr3D.Core.Tests alike.

using System;
using System.Collections.Generic;

namespace Purr3D.Core
{
    /// <summary>A state's group decides how shooting moves a task through it (see <see cref="StateCycle"/>).</summary>
    public enum StateGroup
    {
        Backlog,
        Unstarted,
        Started,
        Completed,
        Cancelled
    }

    public sealed class PurrState
    {
        public string Id;
        public string Name;
        public StateGroup Group;

        /// <summary>Purr's sort key. The API's array is not in this order, so always sort.</summary>
        public int Order;

        public override string ToString() => $"{Name} ({Group}, {Order})";
    }

    public sealed class PurrProject
    {
        public string Id;

        /// <summary>The ref prefix, e.g. "HOME".</summary>
        public string Prefix;

        public string Name;
        public string Description;

        /// <summary>Site-relative icon path, e.g. "/icons/HOME.png". May be null.</summary>
        public string Icon;

        /// <summary>Sorted by <see cref="PurrState.Order"/> (the parser sorts them).</summary>
        public List<PurrState> States = new List<PurrState>();

        public bool Archived;

        public override string ToString() => $"{Prefix}: {Name}";
    }

    public sealed class PurrTask
    {
        public string Id;

        /// <summary>"HOME-42".</summary>
        public string Ref;

        public string ProjectId;
        public string Title;
        public string StateId;

        /// <summary>none | low | medium | high | urgent</summary>
        public string Priority = "none";

        /// <summary>Purr's points are optional.</summary>
        public int? Points;

        public List<string> Labels = new List<string>();

        /// <summary>Optimistic-concurrency version; bumps on every change.</summary>
        public int Version;

        /// <summary>When the task last changed, in UTC. Null if the API didn't say.</summary>
        public DateTimeOffset? UpdatedAt;

        /// <summary>
        /// A copy in another state, changed at <paramref name="at"/>: what a shot shows straight away, before Purr
        /// answers. The original is left alone, so it can be put back if Purr says no.
        /// </summary>
        public PurrTask WithState(string stateId, DateTimeOffset at) => new PurrTask
        {
            Id = Id,
            Ref = Ref,
            ProjectId = ProjectId,
            Title = Title,
            StateId = stateId,
            Priority = Priority,
            Points = Points,
            Labels = Labels == null ? new List<string>() : new List<string>(Labels),
            Version = Version,
            UpdatedAt = at,
        };

        public override string ToString() => $"{Ref}: {Title}";
    }

    /// <summary>One page of <c>GET /projects/{prefix}/tasks</c>. A null <see cref="Cursor"/> means the last page.</summary>
    public sealed class TaskPage
    {
        public List<PurrTask> Items = new List<PurrTask>();
        public string Cursor;
    }
}
