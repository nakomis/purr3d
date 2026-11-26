// The key file: {"environment", "baseUrl", "apiKey"}. scripts/fetch-api-key.sh writes it, the game's key screen
// writes it, and PurrSettings reads it, so the format lives here once.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Purr3D.Core
{
    /// <summary>Purr's two deployments.</summary>
    public static class PurrEnvironments
    {
        public const string Sandbox = "sandbox";
        public const string Production = "prod";

        public const string SandboxUrl = "https://purr.sandbox.nakomis.com/api";
        public const string ProductionUrl = "https://purr.nakomis.com/api";

        /// <summary>The API base URL of "sandbox" or "prod" (any case); null for anything else.</summary>
        public static string UrlFor(string environment)
        {
            switch (environment?.Trim().ToLowerInvariant())
            {
                case Sandbox: return SandboxUrl;
                case Production: return ProductionUrl;
                default: return null;
            }
        }
    }

    public sealed class PurrSecretsFile
    {
        public string Environment = PurrEnvironments.Sandbox;
        public string BaseUrl = PurrEnvironments.SandboxUrl;
        public string ApiKey = "";

        public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

        /// <summary>
        /// Reads the file's text. Missing fields keep their defaults (sandbox, no key); a base URL that's missing
        /// follows the environment. Throws <see cref="PurrFormatException"/> when it isn't a JSON object.
        /// </summary>
        public static PurrSecretsFile Parse(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json ?? "");
            }
            catch (JsonException e)
            {
                throw new PurrFormatException("the key file isn't JSON", e);
            }

            var file = new PurrSecretsFile();
            var environment = (string)root["environment"];
            if (!string.IsNullOrWhiteSpace(environment)) file.Environment = environment.Trim();

            var baseUrl = (string)root["baseUrl"];
            file.BaseUrl = !string.IsNullOrWhiteSpace(baseUrl)
                ? baseUrl.Trim().TrimEnd('/')
                : PurrEnvironments.UrlFor(file.Environment) ?? PurrEnvironments.SandboxUrl;

            file.ApiKey = ((string)root["apiKey"] ?? "").Trim();
            return file;
        }

        /// <summary>The file's text, as fetch-api-key.sh writes it.</summary>
        public string ToJson()
        {
            var root = new JObject
            {
                ["environment"] = Environment,
                ["baseUrl"] = BaseUrl,
                ["apiKey"] = ApiKey,
            };
            return root.ToString(Formatting.Indented) + "\n";
        }

        /// <summary>A file for <paramref name="environment"/>'s URL with <paramref name="apiKey"/>.</summary>
        public static PurrSecretsFile For(string environment, string apiKey)
        {
            var url = PurrEnvironments.UrlFor(environment) ?? throw new ArgumentException($"unknown environment '{environment}'", nameof(environment));
            return new PurrSecretsFile
            {
                Environment = environment.Trim().ToLowerInvariant(),
                BaseUrl = url,
                ApiKey = (apiKey ?? "").Trim(),
            };
        }

        /// <summary>Never the key itself: safe to log.</summary>
        public override string ToString() => $"{Environment} at {BaseUrl}, {(HasApiKey ? "with a key" : "no key")}";
    }
}
