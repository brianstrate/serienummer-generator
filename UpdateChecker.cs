using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace seriel_number_generator
{
    public static class UpdateChecker
    {
        // Tilpas med dit GitHub-brugernavn og repo-navn
        private const string RepoOwner = "brianstrate";
        private const string RepoName = "serienummer-generator";

        public static async Task CheckForUpdatesAsync(bool silentIfUpToDate = true)
        {
            try
            {
                using var client = new HttpClient();
                // GitHub API kræver altid en User-Agent header
                client.DefaultRequestHeaders.Add("User-Agent", "WPF-UpdateChecker");

                string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                string json = await client.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Læs det seneste tag (f.eks. "v1.5.0" eller "1.5.0")
                string tagName = root.GetProperty("tag_name").GetString() ?? "";
                string cleanTag = tagName.TrimStart('v', 'V');

                if (!Version.TryParse(cleanTag, out Version? latestVersion))
                    return;

                Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

                // Tjek om GitHub-versionen er højere end den kørende version
                if (latestVersion > currentVersion)
                {
                    // Find download-URL for installationsfilen (.exe)
                    string? downloadUrl = null;
                    string installerName = "SerienummerGenerator_Setup.exe";

                    if (root.TryGetProperty("assets", out var assets))
                    {
                        foreach (var asset in assets.EnumerateArray())
                        {
                            string name = asset.GetProperty("name").GetString() ?? "";
                            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                                installerName = name;
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(downloadUrl))
                        return;

                    var result = MessageBox.Show(
                        $"En ny version ({tagName}) er tilgængelig!\n\nVil du hente og installere den nu?",
                        "Opdatering fundet",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        await DownloadAndRunInstallerAsync(client, downloadUrl, installerName);
                    }
                }
                else if (!silentIfUpToDate)
                {
                    MessageBox.Show("Du bruger allerede den nyeste version.", "Ingen opdateringer", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                if (!silentIfUpToDate)
                {
                    MessageBox.Show($"Kunne ikke tjekke for opdateringer: {ex.Message}", "Fejl", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private static async Task DownloadAndRunInstallerAsync(HttpClient client, string url, string fileName)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), fileName);

            // Hent den nye setup.exe
            byte[] fileBytes = await client.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(tempPath, fileBytes);

            // Start installationsprogrammet
            Process.Start(new ProcessStartInfo
            {
                FileName = tempPath,
                UseShellExecute = true
            });

            // Luk det kørende program, så installationsprogrammet kan overskrive filerne
            Application.Current.Shutdown();
        }
    }
}