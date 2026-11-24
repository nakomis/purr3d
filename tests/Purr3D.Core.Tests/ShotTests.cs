using System;
using System.Collections.Generic;
using NUnit.Framework;
using Purr3D.Core;

namespace Purr3D.Core.Tests
{
    /// <summary>The Core half of a shot: copy the task into its next state, and swap it in and out of the cache.</summary>
    public class ShotTests
    {
        private static readonly DateTimeOffset Earlier = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 11, 24, 21, 0, 0, TimeSpan.Zero);

        private static PurrTask T(string reference, string state, DateTimeOffset? at = null) => new PurrTask
        {
            Id = "id-" + reference, Ref = reference, Title = "Title " + reference, StateId = state, Points = 2,
            Labels = new List<string> { "x" }, Version = 4, UpdatedAt = at ?? Earlier,
        };

        [Test]
        public void WithStateCopiesTheTaskIntoTheNewStateAndLeavesTheOriginalAlone()
        {
            var original = T("P-1", "todo");

            var moved = original.WithState("doing", Now);
            moved.Labels.Add("y");

            Assert.That(moved, Is.Not.SameAs(original));
            Assert.That(moved.StateId, Is.EqualTo("doing"));
            Assert.That(moved.UpdatedAt, Is.EqualTo(Now));
            Assert.That(moved.Ref, Is.EqualTo("P-1"));
            Assert.That(moved.Title, Is.EqualTo("Title P-1"));
            Assert.That(moved.Points, Is.EqualTo(2));
            Assert.That(moved.Version, Is.EqualTo(4));

            Assert.That(original.StateId, Is.EqualTo("todo"));
            Assert.That(original.UpdatedAt, Is.EqualTo(Earlier));
            Assert.That(original.Labels, Is.EqualTo(new[] { "x" }));
        }

        [Test]
        public void ReplaceTaskSwapsByRefAndReturnsTheOldOne()
        {
            var old = T("P-2", "todo");
            var entry = new CorridorEntry(new PurrProject(), new List<PurrTask> { T("P-1", "todo"), old, T("P-3", "todo") });

            var replaced = entry.ReplaceTask(old.WithState("doing", Now));

            Assert.That(replaced, Is.SameAs(old));
            Assert.That(entry.Tasks.Count, Is.EqualTo(3));
            Assert.That(entry.Tasks[1].StateId, Is.EqualTo("doing"));
            Assert.That(entry.LatestActivity, Is.EqualTo(Now));
        }

        [Test]
        public void PuttingTheOldTaskBackUndoesTheShot()
        {
            var old = T("P-1", "todo");
            var entry = new CorridorEntry(new PurrProject(), new List<PurrTask> { old });

            entry.ReplaceTask(old.WithState("doing", Now));
            entry.ReplaceTask(old);

            Assert.That(entry.Tasks[0], Is.SameAs(old));
            Assert.That(entry.LatestActivity, Is.EqualTo(Earlier));
        }

        [Test]
        public void ReplacingAnUnknownRefChangesNothing()
        {
            var entry = new CorridorEntry(new PurrProject(), new List<PurrTask> { T("P-1", "todo") });

            Assert.That(entry.ReplaceTask(T("P-9", "doing", Now)), Is.Null);
            Assert.That(entry.ReplaceTask(null), Is.Null);
            Assert.That(entry.Tasks.Count, Is.EqualTo(1));
            Assert.That(entry.LatestActivity, Is.EqualTo(Earlier));
        }
    }
}
