using System;
using System.Collections.Generic;
using NUnit.Framework;
using Purr3D.Core;

namespace Purr3D.Core.Tests
{
    public class CorridorOrderTests
    {
        private static readonly DateTimeOffset Monday = new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

        private static PurrProject P(string prefix) => new PurrProject { Id = "id-" + prefix, Prefix = prefix, Name = prefix };

        private static PurrTask T(string reference, DateTimeOffset? updatedAt) =>
            new PurrTask { Ref = reference, StateId = "s", UpdatedAt = updatedAt };

        private static CorridorEntry E(string prefix, params DateTimeOffset?[] updates)
        {
            var tasks = new List<PurrTask>();
            for (var i = 0; i < updates.Length; i++) tasks.Add(T($"{prefix}-{i + 1}", updates[i]));
            return new CorridorEntry(P(prefix), tasks);
        }

        private static string[] Prefixes(List<CorridorEntry> entries) => entries.ConvertAll(e => e.Project.Prefix).ToArray();

        [Test]
        public void LatestIsTheNewestTaskIgnoringUndatedOnes()
        {
            var tasks = new List<PurrTask> { T("A-1", Monday), T("A-2", null), T("A-3", Monday.AddDays(2)), T("A-4", Monday.AddDays(-9)) };
            Assert.That(CorridorOrder.Latest(tasks), Is.EqualTo(Monday.AddDays(2)));
        }

        [Test]
        public void LatestIsNullWhenNothingHasADate()
        {
            Assert.That(CorridorOrder.Latest(new List<PurrTask>()), Is.Null);
            Assert.That(CorridorOrder.Latest(new List<PurrTask> { T("A-1", null) }), Is.Null);
            Assert.That(CorridorOrder.Latest(null), Is.Null);
        }

        [Test]
        public void TheEntryWorksOutItsOwnLatestActivity()
        {
            Assert.That(E("HOME", Monday, Monday.AddHours(3)).LatestActivity, Is.EqualTo(Monday.AddHours(3)));
        }

        [Test]
        public void MostRecentlyActiveProjectComesFirst()
        {
            var ordered = CorridorOrder.Order(new[]
            {
                E("OLD", Monday.AddDays(-70)),
                E("HOME", Monday.AddDays(-1), Monday.AddDays(-30)),
                E("PURR3D", Monday.AddDays(-80), Monday),
            });
            Assert.That(Prefixes(ordered), Is.EqualTo(new[] { "PURR3D", "HOME", "OLD" }));
        }

        [Test]
        public void TiesGoByPrefix()
        {
            var ordered = CorridorOrder.Order(new[] { E("ZED", Monday), E("ABC", Monday), E("MID", Monday) });
            Assert.That(Prefixes(ordered), Is.EqualTo(new[] { "ABC", "MID", "ZED" }));
        }

        [Test]
        public void ProjectsWithoutActivityAndFailedOnesGoLastByPrefix()
        {
            var failed = CorridorEntry.Failed(P("BROKEN"));
            var ordered = CorridorOrder.Order(new[]
            {
                E("EMPTY"),
                failed,
                E("UNDATED", (DateTimeOffset?)null),
                E("OLD", Monday.AddYears(-3)),
            });
            Assert.That(Prefixes(ordered), Is.EqualTo(new[] { "OLD", "BROKEN", "EMPTY", "UNDATED" }));
            Assert.That(failed.LoadFailed, Is.True);
            Assert.That(failed.Tasks, Is.Empty);
        }

        [Test]
        public void SkipsNullsAndHandlesNoInput()
        {
            Assert.That(CorridorOrder.Order(null), Is.Empty);
            Assert.That(CorridorOrder.Order(new CorridorEntry[0]), Is.Empty);
            Assert.That(Prefixes(CorridorOrder.Order(new[] { null, new CorridorEntry(), E("A", Monday) })),
                Is.EqualTo(new[] { "A" }));
        }

        [Test]
        public void ReturnsANewListAndLeavesTheInputAlone()
        {
            var input = new List<CorridorEntry> { E("B", Monday.AddDays(-1)), E("A", Monday) };
            var ordered = CorridorOrder.Order(input);
            Assert.That(ordered, Is.Not.SameAs(input));
            Assert.That(input[0].Project.Prefix, Is.EqualTo("B"));
        }
    }

    public class ProjectHueTests
    {
        [TestCase("HOME")]
        [TestCase("PURR3D")]
        [TestCase("")]
        [TestCase(null)]
        public void IsInRangeAndStable(string prefix)
        {
            var hue = ProjectHue.For(prefix);
            Assert.That(hue, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            Assert.That(ProjectHue.For(prefix), Is.EqualTo(hue));
        }

        [Test]
        public void IsAFixedValueAcrossRuns()
        {
            // Pinned, so a change of hash (and so every door's colour) is a deliberate decision.
            Assert.That(ProjectHue.For("HOME"), Is.EqualTo(PinnedHome).Within(1e-6));
        }

        [Test]
        public void SpreadsSimilarPrefixesApart()
        {
            var hues = new HashSet<float>();
            foreach (var prefix in new[] { "HOME", "BOOT", "BCON", "PURR", "PURR3D", "NAKIS", "JOBS", "SHEP", "PISH", "RECIP" })
                hues.Add(ProjectHue.For(prefix));
            Assert.That(hues, Has.Count.EqualTo(10));
        }

        private const float PinnedHome = 0.4505556f;
    }
}
