// What the HUD shows for a task: its ref, title, state and points, worked out from the task and its project.

using System;

namespace Purr3D.Core
{
    public sealed class TaskCard
    {
        public const string Untitled = "(untitled)";
        public const string UnknownState = "Unknown state";
        public const string NoEstimate = "No estimate";

        public string Ref;
        public string Title;
        public string StateName;

        /// <summary>Null when the task's state isn't one the project lists.</summary>
        public StateGroup? StateGroup;

        public string PointsText;

        public static TaskCard From(PurrProject project, PurrTask task)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (task == null) throw new ArgumentNullException(nameof(task));

            var state = project.States?.Find(s => s != null && s.Id == task.StateId);
            return new TaskCard
            {
                Ref = task.Ref,
                Title = string.IsNullOrWhiteSpace(task.Title) ? Untitled : task.Title.Trim(),
                StateName = state?.Name ?? UnknownState,
                StateGroup = state?.Group,
                PointsText = Points(task.Points),
            };
        }

        public static string Points(int? points) =>
            points == null ? NoEstimate : points == 1 ? "1 point" : $"{points} points";

        public override string ToString() => $"{Ref}: {Title} [{StateName}, {PointsText}]";
    }
}
