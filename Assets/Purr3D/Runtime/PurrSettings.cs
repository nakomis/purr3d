using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Purr3D
{
    /// <summary>
    /// Where Purr is and how to authenticate. Read from UserSettings/PurrSecrets.json, which
    /// scripts/fetch-api-key.sh writes. It's per-user and gitignored, and never in Assets/, so it's never in a build.
    /// The PURR_API_KEY / PURR_BASE_URL environment variables win when set. (An editor launched from the Dock
    /// doesn't inherit your shell's environment, so the file is the normal route.)
    /// </summary>
    public sealed class PurrSettings
    {
        public const string SandboxUrl = "https://purr.sandbox.nakomis.com/api";

        public string Environment = "sandbox";
        public string BaseUrl = SandboxUrl;
        public string ApiKey = "";

        public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);

        public static string SecretsPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "UserSettings", "PurrSecrets.json"));

        public static PurrSettings Load()
        {
            var settings = new PurrSettings();
            try
            {
                if (File.Exists(SecretsPath))
                {
                    var json = JObject.Parse(File.ReadAllText(SecretsPath));
                    settings.Environment = (string)json["environment"] ?? settings.Environment;
                    settings.BaseUrl = (string)json["baseUrl"] ?? settings.BaseUrl;
                    settings.ApiKey = (string)json["apiKey"] ?? "";
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Purr] couldn't read {SecretsPath}: {e.Message}");
            }

            var envKey = System.Environment.GetEnvironmentVariable("PURR_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey)) settings.ApiKey = envKey;
            var envUrl = System.Environment.GetEnvironmentVariable("PURR_BASE_URL");
            if (!string.IsNullOrWhiteSpace(envUrl)) settings.BaseUrl = envUrl;

            settings.ApiKey = settings.ApiKey.Trim();
            settings.BaseUrl = settings.BaseUrl.TrimEnd('/');
            return settings;
        }
    }
}
