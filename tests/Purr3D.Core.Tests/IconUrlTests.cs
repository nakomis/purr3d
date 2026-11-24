using NUnit.Framework;
using Purr3D.Core;

namespace Purr3D.Core.Tests
{
    public class IconUrlTests
    {
        private const string Api = "https://purr.sandbox.nakomis.com/api";

        [Test]
        public void APathIsServedFromTheSiteRootNotTheApi()
        {
            Assert.That(IconUrl.Resolve(Api, "/icons/HOME.png"), Is.EqualTo("https://purr.sandbox.nakomis.com/icons/HOME.png"));
        }

        [TestCase("https://purr.nakomis.com/api/")]
        [TestCase("https://purr.nakomis.com")]
        [TestCase("https://purr.nakomis.com/some/deeper/api")]
        public void WhateverTheApiPathTheRootIsTheSame(string api)
        {
            Assert.That(IconUrl.Resolve(api, "/icons/EYE.png"), Is.EqualTo("https://purr.nakomis.com/icons/EYE.png"));
        }

        [Test]
        public void ARelativePathGetsItsSlash()
        {
            Assert.That(IconUrl.Resolve(Api, "icons/EYE.png"), Is.EqualTo("https://purr.sandbox.nakomis.com/icons/EYE.png"));
        }

        [Test]
        public void AFullUrlIsLeftAlone()
        {
            Assert.That(IconUrl.Resolve(Api, "https://cdn.example.com/x.png"), Is.EqualTo("https://cdn.example.com/x.png"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void NoIconIsNull(string icon)
        {
            Assert.That(IconUrl.Resolve(Api, icon), Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not a url")]
        [TestCase("file:///etc/passwd")]
        public void NoUsableBaseIsNull(string api)
        {
            Assert.That(IconUrl.Resolve(api, "/icons/HOME.png"), Is.Null);
        }
    }
}
