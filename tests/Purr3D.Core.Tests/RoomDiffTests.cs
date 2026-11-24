using System.Collections.Generic;
using NUnit.Framework;
using Purr3D.Core;

namespace Purr3D.Core.Tests
{
    public class RoomDiffTests
    {
        private static PurrTask T(string reference, string state, int version = 1) =>
            new PurrTask { Ref = reference, StateId = state, Version = version };

        [Test]
        public void SeesAddedRemovedMovedAndUpdated()
        {
            var before = new[] { T("P-1", "todo"), T("P-2", "todo"), T("P-3", "todo"), T("P-4", "todo") };
            var after = new[] { T("P-1", "todo"), T("P-2", "done", 2), T("P-3", "todo", 2), T("P-5", "todo") };

            var diff = RoomDiff.Compute(before, after);

            Assert.That(diff.Added.ConvertAll(t => t.Ref), Is.EqualTo(new[] { "P-5" }));
            Assert.That(diff.Moved.ConvertAll(t => t.Ref), Is.EqualTo(new[] { "P-2" }));
            Assert.That(diff.Updated.ConvertAll(t => t.Ref), Is.EqualTo(new[] { "P-3" }));
            Assert.That(diff.Removed, Is.EqualTo(new[] { "P-4" }));
            Assert.That(diff.ToString(), Is.EqualTo("1 new, 1 moved, 1 updated, 1 gone"));
        }

        [Test]
        public void UnchangedTasksAreLeftAlone()
        {
            var tasks = new[] { T("P-1", "todo"), T("P-2", "done") };
            var diff = RoomDiff.Compute(tasks, new[] { T("P-1", "todo"), T("P-2", "done") });

            Assert.That(diff.IsEmpty, Is.True);
            Assert.That(diff.ToString(), Is.EqualTo("no changes"));
        }

        [Test]
        public void TheFirstEntryAddsEverything()
        {
            var diff = RoomDiff.Compute(null, new List<PurrTask> { T("P-1", "todo"), T("P-2", "done") });

            Assert.That(diff.Added.Count, Is.EqualTo(2));
            Assert.That(diff.Removed, Is.Empty);
        }

        [Test]
        public void AnEmptyFetchRemovesEverything()
        {
            var diff = RoomDiff.Compute(new[] { T("P-2", "todo"), T("P-1", "todo") }, null);
            Assert.That(diff.Removed, Is.EqualTo(new[] { "P-1", "P-2" }));
        }

        [Test]
        public void DuplicatesAndRefLessTasksAreIgnored()
        {
            var diff = RoomDiff.Compute(new[] { T("P-1", "todo"), null }, new[] { T("P-1", "done"), T("P-1", "todo"), T(null, "x") });

            Assert.That(diff.Moved.ConvertAll(t => t.Ref), Is.EqualTo(new[] { "P-1" }));
            Assert.That(diff.Added, Is.Empty);
        }
    }
}
