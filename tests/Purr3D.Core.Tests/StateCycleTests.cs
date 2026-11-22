using NUnit.Framework;
using Purr3D.Core;
using static Purr3D.Core.Tests.Fixtures;

namespace Purr3D.Core.Tests
{
    public class StateCycleTests
    {
        private static string Next(PurrProject project, string from) =>
            StateCycle.Next(project, from)?.Id ?? "(stays)";

        [Test]
        public void WalksAStandardProjectToDoneAndStops()
        {
            var p = Standard();
            Assert.That(Next(p, "backlog"), Is.EqualTo("todo"));
            Assert.That(Next(p, "todo"), Is.EqualTo("doing"));
            Assert.That(Next(p, "doing"), Is.EqualTo("done"));
            Assert.That(Next(p, "done"), Is.EqualTo("(stays)"));
        }

        [Test]
        public void NeverMovesIntoCancelledAndCancelledStaysPut()
        {
            var p = Standard();
            Assert.That(Next(p, "doing"), Is.EqualTo("done"));
            Assert.That(Next(p, "cancelled"), Is.EqualTo("(stays)"));
        }

        [Test]
        public void TriageSortedLastMovesToTheFirstUnstartedState()
        {
            Assert.That(Next(Standard(), "triage"), Is.EqualTo("todo"));
        }

        [Test]
        public void StepsThroughStartedStatesToTheFirstCompletedNotArchived()
        {
            var admin = PurrJson.ParseProjectList(ProjectList)[0];
            Assert.That(Next(admin, "s-new"), Is.EqualTo("s-ready"));
            Assert.That(Next(admin, "s-triage"), Is.EqualTo("s-ready"));
            Assert.That(Next(admin, "s-doing"), Is.EqualTo("s-test"));
            Assert.That(Next(admin, "s-test"), Is.EqualTo("s-done"));
            Assert.That(Next(admin, "s-archived"), Is.EqualTo("(stays)"));
        }

        [Test]
        public void CopesWithACompletedStateSortedFirst()
        {
            var p = Blog();
            Assert.That(Next(p, "ideas"), Is.EqualTo("review"));
            Assert.That(Next(p, "aws"), Is.EqualTo("review"));
            Assert.That(Next(p, "review"), Is.EqualTo("published"));
            Assert.That(Next(p, "published"), Is.EqualTo("(stays)"));
        }

        [Test]
        public void GoesStraightFromBacklogToDoneWhenNothingIsBetween()
        {
            var p = Minimal();
            Assert.That(Next(p, "backlog"), Is.EqualTo("done"));
            Assert.That(Next(p, "triage"), Is.EqualTo("done"));
        }

        [Test]
        public void StaysPutForAnUnknownStateOrAProjectWithNoStates()
        {
            Assert.That(Next(Standard(), "nope"), Is.EqualTo("(stays)"));
            Assert.That(Next(Project(), "backlog"), Is.EqualTo("(stays)"));
            Assert.That(Next(Standard(), null), Is.EqualTo("(stays)"));
        }

        [Test]
        public void DoesNotDependOnTheArrayOrder()
        {
            var shuffled = Project(
                State("done", StateGroup.Completed, 40000),
                State("doing", StateGroup.Started, 30000),
                State("backlog", StateGroup.Backlog, 10000),
                State("todo", StateGroup.Unstarted, 20000));
            Assert.That(Next(shuffled, "todo"), Is.EqualTo("doing"));
        }

        [Test]
        public void IsFinishedOnlyForCompletedAndCancelled()
        {
            Assert.That(StateCycle.IsFinished(StateGroup.Completed), Is.True);
            Assert.That(StateCycle.IsFinished(StateGroup.Cancelled), Is.True);
            Assert.That(StateCycle.IsFinished(StateGroup.Started), Is.False);
            Assert.That(StateCycle.IsFinished(StateGroup.Backlog), Is.False);
        }
    }
}
