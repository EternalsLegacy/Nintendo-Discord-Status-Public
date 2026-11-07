#region Usings

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NintendoDiscordStatus.Models;

#endregion

namespace NintendoDiscordStatus.Services;

#region Public Classes

public class UpdateService
{
    #region Constants

    public static readonly Version CurrentVersion = new Version(1, 1, 1);
    public const string CurrentVersionString = "1.1.1";
    private const string GitHubApiUrl = "https://api.github.com/repos/EternalsLegacy/Nintendo-Discord-Status-Public/releases/latest";

    #endregion

    #region Public Methods

    public async Task<UpdateModel?> CheckForUpdateAsync()
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "NintendoDiscordStatus-Updater");

            var response = await client.GetAsync(GitHubApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagProp))
            {
                return null;
            }

            string tagName = tagProp.GetString() ?? string.Empty;
            var cleanTag = Regex.Replace(tagName, @"^[^\d]+", "");

            if (!TryParseVersion(cleanTag, out var remoteVersion) || remoteVersion <= CurrentVersion)
            {
                return null;
            }

            string releaseName = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? tagName : tagName;
            string body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty;

            string downloadUrl = string.Empty;
            string assetName = string.Empty;

            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                // Prefer .zip, fallback to .7z or .exe
                var assets = assetsProp.EnumerateArray().ToList();
                var preferredAsset = assets.FirstOrDefault(a => 
                    (a.TryGetProperty("name", out var n) && n.GetString()?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true));

                if (preferredAsset.ValueKind == JsonValueKind.Undefined)
                {
                    preferredAsset = assets.FirstOrDefault(a => 
                        (a.TryGetProperty("name", out var n) && n.GetString()?.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) == true));
                }

                if (preferredAsset.ValueKind != JsonValueKind.Undefined)
                {
                    assetName = preferredAsset.GetProperty("name").GetString() ?? string.Empty;
                    downloadUrl = preferredAsset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(downloadUrl))
            {
                return null;
            }

            return new UpdateModel
            {
                TagName = tagName,
                ReleaseName = releaseName,
                Changelog = string.IsNullOrWhiteSpace(body) ? "Keine Release-Notizen vorhanden." : body,
                DownloadUrl = downloadUrl,
                AssetName = assetName,
                NewVersion = remoteVersion
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Update] Error checking for updates: {ex.Message}");
            return null;
        }
    }

    public async Task ApplyUpdateAsync(UpdateModel update, Action<string> onStatusChanged)
    {
        string tempBase = Path.Combine(Path.GetTempPath(), "NDS_Update_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempBase);

        string archivePath = Path.Combine(tempBase, update.AssetName);
        string extractDir = Path.Combine(tempBase, "extracted");
        Directory.CreateDirectory(extractDir);

        try
        {
            onStatusChanged("Download wird gestartet...");
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "NintendoDiscordStatus-Updater");
                using var response = await client.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var fs = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await stream.CopyToAsync(fs);
            }

            onStatusChanged("Dateien werden entpackt...");
            if (update.IsZipArchive)
            {
                ZipFile.ExtractToDirectory(archivePath, extractDir, true);
            }
            else
            {
                // Fallback for .7z using 7-Zip or powershell
                string sevenZipExe = @"C:\Program Files\7-Zip\7z.exe";
                if (File.Exists(sevenZipExe))
                {
                    var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = sevenZipExe,
                        Arguments = $"x \"{archivePath}\" -o\"{extractDir}\" -y",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    p?.WaitForExit();
                }
                else
                {
                    throw new InvalidOperationException("7-Zip ist nicht installiert, um .7z Archive zu entpacken.");
                }
            }

            // Find the directory that contains NintendoDiscordStatus.exe
            var foundExes = Directory.GetFiles(extractDir, "NintendoDiscordStatus.exe", SearchOption.AllDirectories);
            if (foundExes.Length == 0)
            {
                throw new FileNotFoundException("NintendoDiscordStatus.exe wurde im heruntergeladenen Paket nicht gefunden.");
            }

            string sourceDir = Path.GetDirectoryName(foundExes[0])!;
            string targetDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            int currentPid = Environment.ProcessId;

            onStatusChanged("Update wird installiert...");

            // Launch helper script to replace files and restart app
            string script = $"-NoProfile -ExecutionPolicy Bypass -Command \"" +
                            $"Start-Sleep -Milliseconds 800; " +
                            $"Wait-Process -Id {currentPid} -Timeout 15 -ErrorAction SilentlyContinue; " +
                            $"Copy-Item -Path '{sourceDir}\\*' -Destination '{targetDir}' -Recurse -Force; " +
                            $"Remove-Item -Path '{tempBase}' -Recurse -Force -ErrorAction SilentlyContinue; " +
                            $"Start-Process -FilePath '{Path.Combine(targetDir, "NintendoDiscordStatus.exe")}'\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = script,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            });

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Update] Error applying update: {ex.Message}");
            onStatusChanged($"Fehler: {ex.Message}");
            throw;
        }
    }

    private static bool TryParseVersion(string input, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(input)) return false;

        var parts = input.Split('.');
        if (parts.Length == 1) input += ".0.0";
        else if (parts.Length == 2) input += ".0";

        return Version.TryParse(input, out version!);
    }

    #endregion
}

#endregion
