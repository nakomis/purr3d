using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Purr3D.Core;

namespace Purr3D.Core.Tests
{
    public class PurrSecretsFileTests
    {
        [Test]
        public void ReadsWhatTheFetchScriptWrites()
        {
            const string json = "{\n  \"environment\": \"sandbox\",\n  \"baseUrl\": \"https://purr.sandbox.nakomis.com/api/\",\n  \"apiKey\": \" k-123 \"\n}\n";

            var file = PurrSecretsFile.Parse(json);

            Assert.That(file.Environment, Is.EqualTo("sandbox"));
            Assert.That(file.BaseUrl, Is.EqualTo("https://purr.sandbox.nakomis.com/api"));
            Assert.That(file.ApiKey, Is.EqualTo("k-123"));
            Assert.That(file.HasApiKey, Is.True);
        }

        [Test]
        public void AMissingBaseUrlFollowsTheEnvironment()
        {
            Assert.That(PurrSecretsFile.Parse("{\"environment\":\"prod\",\"apiKey\":\"k\"}").BaseUrl, Is.EqualTo(PurrEnvironments.ProductionUrl));
            Assert.That(PurrSecretsFile.Parse("{\"apiKey\":\"k\"}").BaseUrl, Is.EqualTo(PurrEnvironments.SandboxUrl));
        }

        [Test]
        public void NoKeyIsNoKey()
        {
            Assert.That(PurrSecretsFile.Parse("{}").HasApiKey, Is.False);
            Assert.That(PurrSecretsFile.Parse("{\"apiKey\":\"   \"}").HasApiKey, Is.False);
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[]")]
        public void NotAnObjectIsAFormatError(string json)
        {
            Assert.Throws<PurrFormatException>(() => PurrSecretsFile.Parse(json));
        }

        [Test]
        public void RoundTrips()
        {
            var written = PurrSecretsFile.For("PROD", " k-9 ").ToJson();
            var read = PurrSecretsFile.Parse(written);

            Assert.That(read.Environment, Is.EqualTo("prod"));
            Assert.That(read.BaseUrl, Is.EqualTo(PurrEnvironments.ProductionUrl));
            Assert.That(read.ApiKey, Is.EqualTo("k-9"));
        }

        [Test]
        public void AnUnknownEnvironmentIsRefused()
        {
            Assert.That(PurrEnvironments.UrlFor("staging"), Is.Null);
            Assert.Throws<ArgumentException>(() => PurrSecretsFile.For("staging", "k"));
        }

        [Test]
        public void ToStringNeverShowsTheKey()
        {
            var text = PurrSecretsFile.For("sandbox", "super-secret-key").ToString();
            Assert.That(text, Does.Not.Contain("super-secret-key"));
            Assert.That(text, Does.Contain("with a key"));
        }

        private sealed class OneReply : IPurrTransport
        {
            private readonly PurrResponse _response;
            public string Path;
            public OneReply(int status, string body) => _response = new PurrResponse(status, body);

            public Task<PurrResponse> SendAsync(string method, string path, string jsonBody, CancellationToken cancellationToken)
            {
                Path = path;
                return Task.FromResult(_response);
            }
        }

        [Test]
        public async Task GetMeReturnsTheActor()
        {
            var transport = new OneReply(200, "{\"actor\":\"purr3d\"}");
            Assert.That(await new PurrClient(transport).GetMeAsync(), Is.EqualTo("purr3d"));
            Assert.That(transport.Path, Is.EqualTo("/me"));
        }

        [Test]
        public void GetMeWithABadKeyIsA401()
        {
            var transport = new OneReply(401, "{\"error\":{\"code\":\"UNAUTHORISED\",\"message\":\"unknown API key\"}}");
            var e = Assert.ThrowsAsync<PurrApiException>(() => new PurrClient(transport).GetMeAsync());
            Assert.That(e.Status, Is.EqualTo(401));
        }
    }
}
