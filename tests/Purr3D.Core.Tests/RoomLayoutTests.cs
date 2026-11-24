using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Purr3D.Core;

namespace Purr3D.Core.Tests
{
    public class RoomLayoutTests
    {
        private static readonly DateTimeOffset Monday = new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

        private static PurrTask T(string reference, string state, DateTimeOffset? updatedAt = null) =>
            new PurrTask { Ref = reference, StateId = state, UpdatedAt = updatedAt };

        private static List<PurrTask> Many(string state, int count, string prefix = "P", int from = 1) =>
            Enumerable.Range(from, count).Select(i => T($"{prefix}-{i}", state)).ToList();

        [Test]
        public void ZonesFollowStateOrderAndEmptyStatesStillGetOne()
        {
            var project = Fixtures.Project(
                Fixtures.State("done", StateGroup.Completed, 300),
                Fixtures.State("new", StateGroup.Backlog, 100),
                Fixtures.State("doing", StateGroup.Started, 200));

            var plan = RoomLayout.Build(project, new[] { T("P-1", "doing") });

            Assert.That(plan.Zones.Select(z => z.State.Id), Is.EqualTo(new[] { "new", "doing", "done" }));
            Assert.That(plan.Zones.Select(z => z.Count), Is.EqualTo(new[] { 0, 1, 0 }));
            Assert.That(plan.Slots["P-1"].ZoneIndex, Is.EqualTo(1));
        }

        [Test]
        public void ZonesSitSideBySideCentredOnTheRoom()
        {
            var settings = new RoomLayoutSettings { ZoneWidth = 4f };
            var plan = RoomLayout.Build(Fixtures.Minimal(), null, settings);

            Assert.That(plan.Width, Is.EqualTo(plan.Zones.Count * 4f));
            Assert.That(plan.Zones[0].CentreX, Is.EqualTo(-plan.Width / 2f + 2f));
            Assert.That(plan.Zones.Last().CentreX, Is.EqualTo(plan.Width / 2f - 2f));
        }

        [Test]
        public void CratesFillColumnsThenRowsThenLayers()
        {
            var settings = new RoomLayoutSettings { Columns = 3, MaxRows = 2, Pitch = 1f, FrontMargin = 0f };
            var project = Fixtures.Project(Fixtures.State("s", StateGroup.Backlog, 1));

            var plan = RoomLayout.Build(project, Many("s", 8), settings);
            var at = Enumerable.Range(1, 8).Select(i => plan.Slots[$"P-{i}"]).Select(s => (s.Column, s.Row, s.Layer)).ToArray();

            Assert.That(at, Is.EqualTo(new[]
            {
                (0, 0, 0), (1, 0, 0), (2, 0, 0),
                (0, 1, 0), (1, 1, 0), (2, 1, 0),
                (0, 0, 1), (1, 0, 1),
            }));
            Assert.That(plan.Zones[0].Rows, Is.EqualTo(2));
            Assert.That(plan.Zones[0].Layers, Is.EqualTo(2));
        }

        [Test]
        public void SlotPositionsAreCentredInTheZoneAndStartPastTheFrontMargin()
        {
            var settings = new RoomLayoutSettings { ZoneWidth = 4f, Columns = 3, Pitch = 1f, FrontMargin = 2f };
            var project = Fixtures.Project(Fixtures.State("s", StateGroup.Backlog, 1));

            var plan = RoomLayout.Build(project, Many("s", 4), settings);

            Assert.That(plan.Slots["P-1"].X, Is.EqualTo(-1f));
            Assert.That(plan.Slots["P-2"].X, Is.EqualTo(0f));
            Assert.That(plan.Slots["P-3"].X, Is.EqualTo(1f));
            Assert.That(plan.Slots["P-1"].Z, Is.EqualTo(2.5f));
            Assert.That(plan.Slots["P-4"].Z, Is.EqualTo(3.5f));
        }

        [Test]
        public void NewestFirstThenUndatedByRefNumber()
        {
            var project = Fixtures.Project(Fixtures.State("s", StateGroup.Backlog, 1));
            var tasks = new[]
            {
                T("P-10", "s"), T("P-9", "s"),
                T("P-1", "s", Monday), T("P-2", "s", Monday.AddDays(1)),
            };

            var plan = RoomLayout.Build(project, tasks);
            var order = plan.Slots.Values.OrderBy(s => s.Layer).ThenBy(s => s.Row).ThenBy(s => s.Column).Select(s => s.Ref);

            Assert.That(order, Is.EqualTo(new[] { "P-2", "P-1", "P-9", "P-10" }));
        }

        [Test]
        public void TheSameTasksInAnyOrderGiveTheSameSlots()
        {
            var project = Fixtures.Standard();
            var tasks = Many("todo", 30).Concat(Many("done", 20, from: 31)).ToList();
            var shuffled = tasks.OrderBy(t => t.Ref.GetHashCode()).ToList();

            var a = RoomLayout.Build(project, tasks);
            var b = RoomLayout.Build(project, shuffled);

            foreach (var reference in a.Slots.Keys) Assert.That(b.Slots[reference].SamePlaceAs(a.Slots[reference]), reference);
        }

        [Test]
        public void TasksInAStateTheProjectDoesNotListGoInAnUnknownZoneOnTheRight()
        {
            var plan = RoomLayout.Build(Fixtures.Minimal(), new[] { T("P-1", "gone"), T("P-2", null) });

            var last = plan.Zones.Last();
            Assert.That(last.State, Is.Null);
            Assert.That(last.Name, Is.EqualTo(RoomLayout.UnknownZoneName));
            Assert.That(last.Count, Is.EqualTo(2));
            Assert.That(plan.Slots["P-1"].ZoneIndex, Is.EqualTo(last.Index));
        }

        [Test]
        public void NoUnknownZoneWhenEveryTaskFits()
        {
            var plan = RoomLayout.Build(Fixtures.Minimal(), null);
            Assert.That(plan.Zones.Any(z => z.State == null), Is.False);
        }

        [Test]
        public void AnEmptyProjectStillHasARoom()
        {
            var settings = new RoomLayoutSettings { MinDepth = 8f };
            var plan = RoomLayout.Build(Fixtures.Project(), new PurrTask[0], settings);

            Assert.That(plan.Zones, Is.Empty);
            Assert.That(plan.Slots, Is.Empty);
            Assert.That(plan.Depth, Is.EqualTo(8f));
        }

        [Test]
        public void DuplicateAndRefLessTasksAreSkipped()
        {
            var project = Fixtures.Project(Fixtures.State("s", StateGroup.Backlog, 1));
            var plan = RoomLayout.Build(project, new[] { T("P-1", "s"), T("P-1", "s"), T(null, "s"), null });

            Assert.That(plan.Slots.Count, Is.EqualTo(1));
            Assert.That(plan.Zones[0].Count, Is.EqualTo(1));
        }

        [Test]
        public void HomeSizedDataStacksInsteadOfRunningAway()
        {
            // HOME on 2026-11-24: 540 tasks, 320 of them Done.
            var settings = new RoomLayoutSettings();
            var project = Fixtures.Standard();
            var tasks = Many("done", 320).Concat(Many("backlog", 117, from: 321)).Concat(Many("cancelled", 80, from: 438)).ToList();

            var plan = RoomLayout.Build(project, tasks, settings);
            var done = plan.Zones.Single(z => z.State?.Id == "done");

            Assert.That(plan.Slots.Count, Is.EqualTo(517));
            Assert.That(done.Rows, Is.EqualTo(settings.MaxRows));
            Assert.That(done.Layers, Is.EqualTo(6)); // 320 / (5 x 12) = 5.3
            Assert.That(plan.Depth, Is.EqualTo(settings.FrontMargin + settings.MaxRows * settings.Pitch + settings.BackMargin).Within(1e-4));
        }

        [Test]
        public void RefNumberParsesTheTail()
        {
            Assert.That(RoomLayout.RefNumber("HOME-42"), Is.EqualTo(42));
            Assert.That(RoomLayout.RefNumber("PURR3D-5"), Is.EqualTo(5));
            Assert.That(RoomLayout.RefNumber("nonsense"), Is.EqualTo(int.MaxValue));
            Assert.That(RoomLayout.RefNumber(null), Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void ANullProjectIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => RoomLayout.Build(null, null));
        }
    }
}
