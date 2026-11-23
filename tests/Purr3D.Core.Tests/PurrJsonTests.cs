using System;
using NUnit.Framework;
using Purr3D.Core;
using static Purr3D.Core.Tests.Fixtures;

namespace Purr3D.Core.Tests
{
    public class PurrJsonTests
    {
        [TestCase("backlog", StateGroup.Backlog)]
        [TestCase("unstarted", StateGroup.Unstarted)]
        [TestCase("started", StateGroup.Started)]
        [TestCase("completed", StateGroup.Completed)]
        [TestCase("cancelled", StateGroup.Cancelled)]
        [TestCase("someday", StateGroup.Backlog)]
        [TestCase(null, StateGroup.Backlog)]
        public void MapsStateGroupsAndParksUnknownOnesInBacklog(string group, StateGroup expected)
        {
            Assert.That(PurrJson.ParseStateGroup(group), Is.EqualTo(expected));
        }

        [Test]
        public void ParsesProjectsSkipsJunkAndSortsStatesByOrder()
        {
            var projects = PurrJson.ParseProjectList(ProjectList);
            Assert.That(projects, Has.Count.EqualTo(3));

            var admin = projects[0];
            Assert.That(admin.Prefix, Is.EqualTo("ADMIN"));
            Assert.That(admin.Name, Is.EqualTo("Nakom Admin"));
            Assert.That(admin.Icon, Is.EqualTo("/icons/ADMIN.png"));
            Assert.That(admin.Archived, Is.False);
            Assert.That(admin.States, Has.Count.EqualTo(7));
            Assert.That(admin.States[0].Id, Is.EqualTo("s-new"));
            Assert.That(admin.States[1].Id, Is.EqualTo("s-ready"));
            Assert.That(admin.States[6].Id, Is.EqualTo("s-triage"));
            Assert.That(admin.States[4].Order, Is.EqualTo(50000));
            Assert.That(admin.States[4].Group, Is.EqualTo(StateGroup.Completed));

            Assert.That(projects[1].Archived, Is.True);
            Assert.That(projects[2].Name, Is.EqualTo("NONAME"), "name falls back to the prefix");
        }

        [Test]
        public void RejectsABodyThatIsNotAProjectList()
        {
            Assert.Throws<PurrFormatException>(() => PurrJson.ParseProjectList("<html>"));
            Assert.Throws<PurrFormatException>(() => PurrJson.ParseProjectList(ErrorBody));
            Assert.Throws<PurrFormatException>(() => PurrJson.ParseProjectList(null));
        }

        [Test]
        public void ParsesTasksOptionalPointsLabelsAndTheCursor()
        {
            var page = PurrJson.ParseTaskPage(TaskPageWithCursor);
            Assert.That(page.Cursor, Is.EqualTo("eyJwayI6IkFETUlOIn0"));
            Assert.That(page.Items, Has.Count.EqualTo(2), "junk tasks skipped");

            var first = page.Items[0];
            Assert.That(first.Ref, Is.EqualTo("ADMIN-1"));
            Assert.That(first.StateId, Is.EqualTo("s-done"));
            Assert.That(first.Points, Is.Null);
            Assert.That(first.Labels, Is.EqualTo(new[] { "aws", "lambda" }));
            Assert.That(first.Version, Is.EqualTo(3));

            Assert.That(page.Items[1].Points, Is.EqualTo(3));
            Assert.That(page.Items[1].Priority, Is.EqualTo("high"));
        }

        [Test]
        public void TreatsANullOrMissingCursorAsTheLastPage()
        {
            var last = PurrJson.ParseTaskPage(LastTaskPage);
            Assert.That(last.Cursor, Is.Null);
            Assert.That(last.Items[0].Points, Is.Null, "points: null means no points");
            Assert.That(last.Items[0].Priority, Is.EqualTo("none"), "priority defaults to none");

            Assert.That(PurrJson.ParseTaskPage(LastTaskPageNoCursor).Cursor, Is.Null);
        }

        [Test]
        public void ParsesASingleTaskBody()
        {
            var task = PurrJson.ParseTask(TaskBody);
            Assert.That(task.Ref, Is.EqualTo("ADMIN-2"));
            Assert.That(task.StateId, Is.EqualTo("s-test"));
            Assert.That(task.Version, Is.EqualTo(2));
            Assert.Throws<PurrFormatException>(() => PurrJson.ParseTask(@"{""id"": ""no ref""}"));
        }

        [Test]
        public void ReadsPurrsErrorEnvelope()
        {
            Assert.That(PurrJson.TryParseError(ErrorBody, out var code, out var message), Is.True);
            Assert.That(code, Is.EqualTo("NOT_FOUND"));
            Assert.That(message, Is.EqualTo("no project PURR3D"));

            Assert.That(PurrJson.TryParseError(@"{""items"": []}", out _, out _), Is.False);
            Assert.That(PurrJson.TryParseError("<html>", out _, out _), Is.False);
        }

        [Test]
        public void ParsesUpdatedAtAsUtc()
        {
            var task = PurrJson.ParseTask(@"{""ref"": ""HOME-1"", ""stateId"": ""s"", ""updatedAt"": ""2026-09-14T21:18:56.691Z""}");
            Assert.That(task.UpdatedAt, Is.EqualTo(new DateTimeOffset(2026, 9, 14, 21, 18, 56, 691, TimeSpan.Zero)));
            Assert.That(task.UpdatedAt.Value.Offset, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void ConvertsAnOffsetUpdatedAtToUtc()
        {
            var task = PurrJson.ParseTask(@"{""ref"": ""HOME-1"", ""stateId"": ""s"", ""updatedAt"": ""2026-11-23T10:00:00+02:00""}");
            Assert.That(task.UpdatedAt, Is.EqualTo(new DateTimeOffset(2026, 11, 23, 8, 0, 0, TimeSpan.Zero)));
        }

        [TestCase(@"{""ref"": ""HOME-1"", ""stateId"": ""s""}")]
        [TestCase(@"{""ref"": ""HOME-1"", ""stateId"": ""s"", ""updatedAt"": null}")]
        [TestCase(@"{""ref"": ""HOME-1"", ""stateId"": ""s"", ""updatedAt"": ""last Tuesday""}")]
        [TestCase(@"{""ref"": ""HOME-1"", ""stateId"": ""s"", ""updatedAt"": 42}")]
        public void LeavesAMissingOrJunkUpdatedAtNull(string body)
        {
            Assert.That(PurrJson.ParseTask(body).UpdatedAt, Is.Null);
        }
    }
}
