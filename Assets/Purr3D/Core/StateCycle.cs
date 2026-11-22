// Where a shot task goes next.
//
// Real projects make "next by order" wrong: Triage is a backlog state sorted after Done,
// and the blog project lists Published (completed) first. So:
//   - any backlog state  -> the first state of the earliest later group (unstarted, else started, else completed)
//   - otherwise          -> the next state in (group, order) order, never into cancelled
//   - completed / cancelled tasks stay put

using System.Collections.Generic;

namespace Purr3D.Core
{
    public static class StateCycle
    {
        /// <summary>The state a shot moves <paramref name="currentStateId"/> to, or null when the task stays put.</summary>
        public static PurrState Next(PurrProject project, string currentStateId)
        {
            var current = Find(project, currentStateId);
            if (current == null || IsFinished(current.Group)) return null;

            // The path: every non-cancelled state, by group then order (position breaks ties, keeping it stable).
            var path = new List<KeyValuePair<int, PurrState>>();
            for (var i = 0; i < project.States.Count; i++)
            {
                if (project.States[i].Group != StateGroup.Cancelled)
                    path.Add(new KeyValuePair<int, PurrState>(i, project.States[i]));
            }
            path.Sort((a, b) =>
            {
                var byGroup = Rank(a.Value.Group).CompareTo(Rank(b.Value.Group));
                if (byGroup != 0) return byGroup;
                var byOrder = a.Value.Order.CompareTo(b.Value.Order);
                return byOrder != 0 ? byOrder : a.Key.CompareTo(b.Key);
            });

            if (current.Group == StateGroup.Backlog)
            {
                // Every backlog state means "not yet": leave the group altogether.
                foreach (var entry in path)
                {
                    if (entry.Value.Group != StateGroup.Backlog) return entry.Value;
                }
                return null;
            }

            var index = path.FindIndex(entry => entry.Value.Id == currentStateId);
            return index >= 0 && index + 1 < path.Count ? path[index + 1].Value : null;
        }

        /// <summary>A state by id, or null if the project has no such state.</summary>
        public static PurrState Find(PurrProject project, string stateId)
        {
            if (project == null || stateId == null) return null;
            return project.States.Find(s => s.Id == stateId);
        }

        /// <summary>Completed or cancelled: shooting does nothing.</summary>
        public static bool IsFinished(StateGroup group) =>
            group == StateGroup.Completed || group == StateGroup.Cancelled;

        private static int Rank(StateGroup group)
        {
            switch (group)
            {
                case StateGroup.Backlog: return 0;
                case StateGroup.Unstarted: return 1;
                case StateGroup.Started: return 2;
                case StateGroup.Completed: return 3;
                default: return 4;
            }
        }
    }
}
