using System;
using System.Diagnostics;
using System.IO;
using Purr3D.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Purr3D
{
    /// <summary>
    /// Where Purr is and the key to it: a <see cref="PurrSecretsFile"/> (environment, base URL, API key) kept outside
    /// the project's assets, so it's never in a build.
    ///
    /// - In the editor: <c>UserSettings/PurrSecrets.json</c>, per-user and gitignored, as scripts/fetch-api-key.sh
    ///   writes it.
    /// - In a build: the app's own per-user folder, <c>Application.persistentDataPath</c>, which on macOS is
    ///   <c>~/Library/Application Support/&lt;Company&gt;/&lt;Product&gt;/</c>. (The editor's path would land inside the .app.)
    ///
    /// The PURR_API_KEY / PURR_BASE_URL environment variables win when set. Apps opened from Finder or the Dock
    /// don't see your shell's environment, so the file, or the key screen that writes it, is the normal route.
    /// </summary>
    public static class PurrSettings
    {
        public const string FileName = "PurrSecrets.json";

        public static string SecretsPath => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "UserSettings", FileName))
            : Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>The saved settings with any environment overrides applied; sandbox with no key when there's none.</summary>
        public static PurrSecretsFile Load()
        {
            var settings = new PurrSecretsFile();
            try
            {
                if (File.Exists(SecretsPath)) settings = PurrSecretsFile.Parse(File.ReadAllText(SecretsPath));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is PurrFormatException)
            {
                Debug.LogWarning($"[Purr] couldn't read {SecretsPath}: {e.Message}");
            }

            var envKey = System.Environment.GetEnvironmentVariable("PURR_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey)) settings.ApiKey = envKey.Trim();
            var envUrl = System.Environment.GetEnvironmentVariable("PURR_BASE_URL");
            if (!string.IsNullOrWhiteSpace(envUrl)) settings.BaseUrl = envUrl.Trim().TrimEnd('/');
            return settings;
        }

        /// <summary>Writes <paramref name="settings"/> to <see cref="SecretsPath"/>, readable only by you.</summary>
        public static void Save(PurrSecretsFile settings)
        {
            var path = SecretsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, settings.ToJson());
            OwnerOnly(path);
        }

        /// <summary>Deletes the saved key, if any. True when there was one.</summary>
        public static bool Forget()
        {
            if (!File.Exists(SecretsPath)) return false;
            File.Delete(SecretsPath);
            return true;
        }

        /// <summary>chmod 600: .NET Standard 2.1 has no API for Unix permissions, so ask the system's chmod.</summary>
        private static void OwnerOnly(string path)
        {
            if (Application.platform != RuntimePlatform.OSXPlayer && Application.platform != RuntimePlatform.OSXEditor &&
                Application.platform != RuntimePlatform.LinuxPlayer && Application.platform != RuntimePlatform.LinuxEditor)
                return;
            try
            {
                using (var chmod = Process.Start(new ProcessStartInfo("/bin/chmod", $"600 \"{path}\"")
                       {
                           UseShellExecute = false,
                           CreateNoWindow = true,
                       }))
                {
                    chmod?.WaitForExit(2000);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Purr] couldn't restrict {path} to its owner: {e.Message}");
            }
        }
    }
}
