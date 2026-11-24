using System;
using System.Collections.Generic;
using NUnit.Framework;
using Purr3D.Core;

namespace Purr3D.Core.Tests
{
    public class TaskCardTests
    {
        private static PurrProject Project() => new PurrProject
        {
            Prefix = "P",
            States = new List<PurrState>
            {
                new PurrState { Id = "todo", Name = "Todo", Group = StateGroup.Unstarted, Order = 1 },
                new PurrState { Id = "done", Name = "Done", Group = StateGroup.Completed, Order = 2 },
            },
        };

        [Test]
        public void ShowsRefTitleStateAndPoints()
        {
            var card = TaskCard.From(Project(), new PurrTask { Ref = "P-7", Title = " Fix the door ", StateId = "done", Points = 3 });

            Assert.That(card.Ref, Is.EqualTo("P-7"));
            Assert.That(card.Title, Is.EqualTo("Fix the door"));
            Assert.That(card.StateName, Is.EqualTo("Done"));
            Assert.That(card.StateGroup, Is.EqualTo(StateGroup.Completed));
            Assert.That(card.PointsText, Is.EqualTo("3 points"));
        }

        [Test]
        public void AStateTheProjectDoesNotListIsUnknown()
        {
            var card = TaskCard.From(Project(), new PurrTask { Ref = "P-1", Title = "x", StateId = "gone" });

            Assert.That(card.StateName, Is.EqualTo(TaskCard.UnknownState));
            Assert.That(card.StateGroup, Is.Null);
        }

        [TestCase(null, "No estimate")]
        [TestCase(0, "0 points")]
        [TestCase(1, "1 point")]
        [TestCase(8, "8 points")]
        public void PointsReadNaturally(int? points, string expected)
        {
            Assert.That(TaskCard.Points(points), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ABlankTitleIsUntitled(string title)
        {
            var card = TaskCard.From(Project(), new PurrTask { Ref = "P-1", Title = title, StateId = "todo" });

            Assert.That(card.Title, Is.EqualTo(TaskCard.Untitled));
        }

        [Test]
        public void NeedsAProjectAndATask()
        {
            Assert.Throws<ArgumentNullException>(() => TaskCard.From(null, new PurrTask()));
            Assert.Throws<ArgumentNullException>(() => TaskCard.From(Project(), null));
        }
    }
}
