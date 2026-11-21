using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NintendoDiscordStatus.Core.Abstractions;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;

namespace NintendoDiscordStatus.Core.Services;

public class UpdateService
{
    private readonly HttpClient HttpClientInstance;
    private readonly IProcessRunner ProcessRunner;
    private readonly string BaseDirectory;
    private readonly string SevenZipPath;

    public UpdateService(
        IProcessRunner ProcessRunner,
        HttpClient? HttpClientInstance = null,
        string? BaseDirectory = null,
        string? SevenZipPath = null)
    {
        this.ProcessRunner = ProcessRunner;
        this.HttpClientInstance = HttpClientInstance ?? new HttpClient();
        this.BaseDirectory = string.IsNullOrWhiteSpace(BaseDirectory)
            ? AppPaths.GetDefaultBaseDirectory()
            : BaseDirectory;
        this.SevenZipPath = string.IsNullOrWhiteSpace(SevenZipPath)
            ? AppConstants.DefaultSevenZipPath
            : SevenZipPath;
    }

    public async Task<UpdateInfoModel?> CheckForUpdatesAsync(CancellationToken Token = default)
    {
        try
        {
            using HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Get, AppConstants.GitHubApiLatestReleaseUrl);
            Request.Headers.TryAddWithoutValidation("User-Agent", AppConstants.UpdaterUserAgent);

            using HttpResponseMessage Response = await HttpClientInstance.SendAsync(Request, Token);
            if (!Response.IsSuccessStatusCode)
            {
                return null;
            }

            string JsonText = await Response.Content.ReadAsStringAsync(Token);
            GitHubReleaseModel? Release = JsonSerializer.Deserialize<GitHubReleaseModel>(JsonText);
            if (Release == null || string.IsNullOrWhiteSpace(Release.TagName))
            {
                return null;
            }

            string VersionString = Release.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(VersionString, out Version? LatestVersion))
            {
                return null;
            }

            GitHubAssetModel? PreferredAsset = Release.Assets.FirstOrDefault(Asset =>
                Asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            PreferredAsset ??= Release.Assets.FirstOrDefault(Asset =>
                Asset.Name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase));

            PreferredAsset ??= Release.Assets.FirstOrDefault();

            return new UpdateInfoModel
            {
                IsUpdateAvailable = LatestVersion > AppConstants.CurrentVersion,
                LatestVersion = LatestVersion,
                TagName = Release.TagName,
                ReleaseNotes = Release.Body,
                DownloadUrl = PreferredAsset?.BrowserDownloadUrl ?? string.Empty,
                AssetFileName = PreferredAsset?.Name ?? string.Empty,
                HtmlUrl = Release.HtmlUrl
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<UpdateExecutionResult> DownloadAndInstallAsync(
        UpdateInfoModel UpdateInfo,
        IProgress<double>? Progress = null,
        CancellationToken Token = default)
    {
        if (string.IsNullOrWhiteSpace(UpdateInfo.DownloadUrl))
        {
            return new UpdateExecutionResult
            {
                Status = UpdateExecutionStatus.NoDownloadUrl,
                ErrorMessage = "No downloadable release asset was found."
            };
        }

        bool IsSevenZip = UpdateInfo.DownloadUrl.EndsWith(".7z", StringComparison.OrdinalIgnoreCase);
        if (IsSevenZip && !File.Exists(SevenZipPath))
        {
            return new UpdateExecutionResult
            {
                Status = UpdateExecutionStatus.SevenZipMissing,
                ErrorMessage = $"7-Zip is required to extract .7z archives ({SevenZipPath})."
            };
        }

        string TempDir = Path.Combine(Path.GetTempPath(), $"NDS_Update_{Guid.NewGuid():N}");
        Directory.CreateDirectory(TempDir);

        try
        {
            string ArchiveName = IsSevenZip ? "update.7z" : "update.zip";
            string TempArchivePath = Path.Combine(TempDir, ArchiveName);
            string ExtractDir = Path.Combine(TempDir, "Extracted");
            Directory.CreateDirectory(ExtractDir);

            using (HttpResponseMessage Response = await HttpClientInstance.GetAsync(
                UpdateInfo.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                Token))
            {
                if (!Response.IsSuccessStatusCode)
                {
                    CleanupDirectory(TempDir);
                    return new UpdateExecutionResult
                    {
                        Status = UpdateExecutionStatus.DownloadFailed,
                        ErrorMessage = $"Download failed with status {(int)Response.StatusCode}."
                    };
                }

                long TotalBytes = Response.Content.Headers.ContentLength ?? -1L;
                await using Stream ContentStream = await Response.Content.ReadAsStreamAsync(Token);
                await using FileStream FileStream = new FileStream(
                    TempArchivePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    true);

                byte[] Buffer = new byte[81920];
                long TotalRead = 0L;
                int BytesRead;

                while ((BytesRead = await ContentStream.ReadAsync(Buffer, 0, Buffer.Length, Token)) > 0)
                {
                    await FileStream.WriteAsync(Buffer, 0, BytesRead, Token);
                    TotalRead += BytesRead;

                    if (TotalBytes > 0)
                    {
                        Progress?.Report((double)TotalRead / TotalBytes);
                    }
                }
            }

            if (IsSevenZip)
            {
                int ExitCode = ProcessRunner.RunAndWait(SevenZipPath, $"x \"{TempArchivePath}\" -o\"{ExtractDir}\" -y");
                if (ExitCode != 0)
                {
                    CleanupDirectory(TempDir);
                    return new UpdateExecutionResult
                    {
                        Status = UpdateExecutionStatus.ExtractionFailed,
                        ErrorMessage = $"7-Zip extraction failed with exit code {ExitCode}."
                    };
                }
            }
            else
            {
                ZipFile.ExtractToDirectory(TempArchivePath, ExtractDir, true);
            }

            string? ExtractedExe = Directory
                .GetFiles(ExtractDir, AppConstants.ExecutableFileName, SearchOption.AllDirectories)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(ExtractedExe))
            {
                CleanupDirectory(TempDir);
                return new UpdateExecutionResult
                {
                    Status = UpdateExecutionStatus.ExecutableNotFound,
                    ErrorMessage = $"{AppConstants.ExecutableFileName} was not found in the downloaded archive."
                };
            }

            string ExtractedPayloadDir = Path.GetDirectoryName(ExtractedExe)!;
            string NormalizedInstallDir = BaseDirectory.TrimEnd('\\', '/');
            string TargetExePath = Path.Combine(NormalizedInstallDir, AppConstants.ExecutableFileName);
            string ScriptPath = Path.Combine(TempDir, "update.ps1");
            int CurrentPid = ProcessRunner.GetCurrentProcessId();

            string ScriptContent = BuildUpdaterPowerShellScript(
                CurrentPid,
                ExtractedPayloadDir,
                NormalizedInstallDir,
                TargetExePath,
                TempDir);

            await File.WriteAllTextAsync(ScriptPath, ScriptContent, Token);

            ProcessRunner.StartHiddenShell(
                "powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{ScriptPath}\"");

            ProcessRunner.ExitApplication(0);

            return new UpdateExecutionResult
            {
                Status = UpdateExecutionStatus.SuccessLaunchedUpdater,
                ExtractedFilePath = ExtractedExe
            };
        }
        catch (Exception Ex)
        {
            CleanupDirectory(TempDir);
            return new UpdateExecutionResult
            {
                Status = UpdateExecutionStatus.UnexpectedError,
                ErrorMessage = Ex.Message
            };
        }
    }

    public void OpenReleasePage(UpdateInfoModel UpdateInfo)
    {
        if (!string.IsNullOrWhiteSpace(UpdateInfo.HtmlUrl))
        {
            ProcessRunner.OpenUrl(UpdateInfo.HtmlUrl);
        }
    }

    private static string BuildUpdaterPowerShellScript(
        int TargetPid,
        string PayloadDir,
        string InstallDir,
        string TargetExePath,
        string TempDir)
    {
        string EscapedPayloadDir = EscapePowerShellLiteral(PayloadDir);
        string EscapedInstallDir = EscapePowerShellLiteral(InstallDir);
        string EscapedTargetExe = EscapePowerShellLiteral(TargetExePath);
        string EscapedTempDir = EscapePowerShellLiteral(TempDir);

        return $@"
$TargetPid = {TargetPid}
while (Get-Process -Id $TargetPid -ErrorAction SilentlyContinue) {{
    Start-Sleep -Milliseconds 250
}}
Get-Process | Where-Object {{ $_.Path -eq '{EscapedTargetExe}' }} | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
$Copied = $false
for ($Attempt = 1; $Attempt -le 20; $Attempt++) {{
    try {{
        $ErrorActionPreference = 'Stop'
        Copy-Item -Path '{EscapedPayloadDir}\*' -Destination '{EscapedInstallDir}' -Recurse -Force
        $Copied = $true
        break
    }}
    catch {{
        Start-Sleep -Milliseconds 350
    }}
}}
if ($Copied) {{
    Start-Process -FilePath '{EscapedTargetExe}' -WorkingDirectory '{EscapedInstallDir}'
}}
Remove-Item -Path '{EscapedTempDir}' -Recurse -Force -ErrorAction SilentlyContinue
";
    }

    private static string EscapePowerShellLiteral(string Value)
    {
        return Value.Replace("'", "''");
    }

    private static void CleanupDirectory(string PathToDelete)
    {
        if (Directory.Exists(PathToDelete))
        {
            Directory.Delete(PathToDelete, true);
        }
    }
}
