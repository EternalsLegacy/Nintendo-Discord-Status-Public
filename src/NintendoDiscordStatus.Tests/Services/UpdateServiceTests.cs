using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;
using NintendoDiscordStatus.Core.Services;
using NintendoDiscordStatus.Tests.Fakes;
using Xunit;

namespace NintendoDiscordStatus.Tests.Services;

public class UpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_HandlesErrorResponsesAndVersionComparisons()
    {
        FakeProcessRunner Runner = new FakeProcessRunner();
        UpdateService DefaultService = new UpdateService(Runner);
        Assert.NotNull(DefaultService);

        int Attempt = 0;
        FakeHttpMessageHandler Handler = new FakeHttpMessageHandler(_ =>
        {
            Attempt++;
            if (Attempt == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            if (Attempt == 2)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null") };
            }

            if (Attempt == 3)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new GitHubReleaseModel { TagName = "invalid-tag" }))
                };
            }

            if (Attempt == 4)
            {
                throw new HttpRequestException("Timeout");
            }

            if (Attempt == 5)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new GitHubReleaseModel
                    {
                        TagName = "v1.2.1",
                        Body = "Current release"
                    }))
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new GitHubReleaseModel
                {
                    TagName = "v1.3.0",
                    Body = "## Release Notes",
                    HtmlUrl = "https://github.com/EternalsLegacy/Nintendo-Discord-Status-Public/releases/tag/v1.3.0",
                    Assets =
                    {
                        new GitHubAssetModel { Name = "app.7z", BrowserDownloadUrl = "https://example.com/app.7z" },
                        new GitHubAssetModel { Name = "app.zip", BrowserDownloadUrl = "https://example.com/app.zip" }
                    }
                }))
            };
        });

        using HttpClient Client = new HttpClient(Handler);
        UpdateService Service = new UpdateService(Runner, Client);

        Assert.Null(await Service.CheckForUpdatesAsync());
        Assert.Null(await Service.CheckForUpdatesAsync());
        Assert.Null(await Service.CheckForUpdatesAsync());
        Assert.Null(await Service.CheckForUpdatesAsync());

        UpdateInfoModel? SameVersionInfo = await Service.CheckForUpdatesAsync();
        Assert.NotNull(SameVersionInfo);
        Assert.False(SameVersionInfo!.IsUpdateAvailable);
        Assert.Equal(string.Empty, SameVersionInfo.DownloadUrl);

        UpdateInfoModel? NewerVersionInfo = await Service.CheckForUpdatesAsync();
        Assert.NotNull(NewerVersionInfo);
        Assert.True(NewerVersionInfo!.IsUpdateAvailable);
        Assert.Equal(new Version(1, 3, 0), NewerVersionInfo.LatestVersion);
        Assert.Equal("app.zip", NewerVersionInfo.AssetFileName);
        Assert.Equal("https://example.com/app.zip", NewerVersionInfo.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_FallsBackToSevenZipOrOtherAsset()
    {
        FakeProcessRunner Runner = new FakeProcessRunner();
        int CallIndex = 0;

        FakeHttpMessageHandler Handler = new FakeHttpMessageHandler(_ =>
        {
            CallIndex++;
            GitHubReleaseModel Release = CallIndex == 1
                ? new GitHubReleaseModel
                {
                    TagName = "V2.0.0",
                    Assets = { new GitHubAssetModel { Name = "pkg.7z", BrowserDownloadUrl = "https://example.com/pkg.7z" } }
                }
                : new GitHubReleaseModel
                {
                    TagName = "2.1.0",
                    Assets = { new GitHubAssetModel { Name = "setup.exe", BrowserDownloadUrl = "https://example.com/setup.exe" } }
                };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(Release))
            };
        });

        using HttpClient Client = new HttpClient(Handler);
        UpdateService Service = new UpdateService(Runner, Client);

        UpdateInfoModel? SevenZipInfo = await Service.CheckForUpdatesAsync();
        Assert.Equal("pkg.7z", SevenZipInfo?.AssetFileName);

        UpdateInfoModel? FallbackAssetInfo = await Service.CheckForUpdatesAsync();
        Assert.Equal("setup.exe", FallbackAssetInfo?.AssetFileName);
    }

    [Fact]
    public async Task DownloadAndInstallAsync_ValidatesPreconditionsDownloadsExtractsAndLaunchesUpdater()
    {
        using TempTestDirectory InstallDir = new TempTestDirectory();
        using TempTestDirectory ArchiveSourceDir = new TempTestDirectory();

        string FakeExeInArchive = Path.Combine(ArchiveSourceDir.DirectoryPath, "SubFolder", AppConstants.ExecutableFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(FakeExeInArchive)!);
        File.WriteAllText(FakeExeInArchive, "fake binary");

        string ValidZipPath = Path.Combine(ArchiveSourceDir.DirectoryPath, "valid.zip");
        ZipFile.CreateFromDirectory(Path.Combine(ArchiveSourceDir.DirectoryPath, "SubFolder"), ValidZipPath);

        string EmptyZipDir = Path.Combine(ArchiveSourceDir.DirectoryPath, "EmptyFolder");
        Directory.CreateDirectory(EmptyZipDir);
        File.WriteAllText(Path.Combine(EmptyZipDir, "readme.txt"), "no exe");
        string MissingExeZipPath = Path.Combine(ArchiveSourceDir.DirectoryPath, "noexe.zip");
        ZipFile.CreateFromDirectory(EmptyZipDir, MissingExeZipPath);

        byte[] ValidZipBytes = File.ReadAllBytes(ValidZipPath);
        byte[] MissingExeZipBytes = File.ReadAllBytes(MissingExeZipPath);

        FakeHttpMessageHandler Handler = new FakeHttpMessageHandler(Request =>
        {
            string Url = Request.RequestUri!.ToString();
            if (Url.EndsWith("fail.zip"))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (Url.EndsWith("noexe.zip"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(MissingExeZipBytes)
                };
            }

            if (Url.EndsWith("corrupt.zip"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 })
                };
            }

            if (Url.EndsWith("valid.7z"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[] { 55, 122 })
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(ValidZipBytes)
            };
        });

        FakeProcessRunner Runner = new FakeProcessRunner { FakeProcessId = 9999 };
        using HttpClient Client = new HttpClient(Handler);
        string MissingSevenZip = Path.Combine(InstallDir.DirectoryPath, "missing7z.exe");
        UpdateService Service = new UpdateService(Runner, Client, InstallDir.DirectoryPath, MissingSevenZip);

        UpdateExecutionResult EmptyUrlResult = await Service.DownloadAndInstallAsync(new UpdateInfoModel { DownloadUrl = "" });
        Assert.Equal(UpdateExecutionStatus.NoDownloadUrl, EmptyUrlResult.Status);

        UpdateExecutionResult MissingSevenZipResult = await Service.DownloadAndInstallAsync(
            new UpdateInfoModel { DownloadUrl = "https://example.com/valid.7z" });
        Assert.Equal(UpdateExecutionStatus.SevenZipMissing, MissingSevenZipResult.Status);

        UpdateExecutionResult DownloadFailResult = await Service.DownloadAndInstallAsync(
            new UpdateInfoModel { DownloadUrl = "https://example.com/fail.zip" });
        Assert.Equal(UpdateExecutionStatus.DownloadFailed, DownloadFailResult.Status);

        UpdateExecutionResult MissingExeResult = await Service.DownloadAndInstallAsync(
            new UpdateInfoModel { DownloadUrl = "https://example.com/noexe.zip" });
        Assert.Equal(UpdateExecutionStatus.ExecutableNotFound, MissingExeResult.Status);

        UpdateExecutionResult CorruptZipResult = await Service.DownloadAndInstallAsync(
            new UpdateInfoModel { DownloadUrl = "https://example.com/corrupt.zip" });
        Assert.Equal(UpdateExecutionStatus.UnexpectedError, CorruptZipResult.Status);

        List<double> ProgressReports = new List<double>();
        IProgress<double> Progress = new SyncProgress<double>(Value => ProgressReports.Add(Value));

        string QuotedDir = Path.Combine(InstallDir.DirectoryPath, "O'Reilly App");
        Directory.CreateDirectory(QuotedDir);
        UpdateService QuotedInstallService = new UpdateService(Runner, Client, QuotedDir, MissingSevenZip);

        UpdateExecutionResult SuccessResult = await QuotedInstallService.DownloadAndInstallAsync(
            new UpdateInfoModel { DownloadUrl = "https://example.com/valid.zip" },
            Progress);

        Assert.Equal(UpdateExecutionStatus.SuccessLaunchedUpdater, SuccessResult.Status);
        Assert.NotEmpty(ProgressReports);
        Assert.Equal(1.0, ProgressReports[^1], 2);
        Assert.Single(Runner.StartHiddenShellCalls);
        Assert.Single(Runner.ExitCodes);
        Assert.Equal(0, Runner.ExitCodes[0]);

        string LaunchedArgs = Runner.StartHiddenShellCalls[0].Arguments;
        int FirstQuote = LaunchedArgs.LastIndexOf("-File \"") + 7;
        string ScriptPath = LaunchedArgs.Substring(FirstQuote, LaunchedArgs.Length - FirstQuote - 1);
        Assert.True(File.Exists(ScriptPath));
        string ScriptContent = File.ReadAllText(ScriptPath);
        Assert.Contains("$TargetPid = 9999", ScriptContent);
        Assert.Contains("O''Reilly App", ScriptContent);

        string TempUpdateFolder = Path.GetDirectoryName(ScriptPath)!;
        Directory.Delete(TempUpdateFolder, true);
    }

    [Fact]
    public async Task DownloadAndInstallAsync_SupportsSevenZipExtractionFailureAndSuccessAndOpenReleasePage()
    {
        using TempTestDirectory InstallDir = new TempTestDirectory();
        string FakeSevenZipExe = Path.Combine(InstallDir.DirectoryPath, "7z.exe");
        File.WriteAllText(FakeSevenZipExe, "stub");

        FakeHttpMessageHandler Handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
            });

        FakeProcessRunner Runner = new FakeProcessRunner { RunAndWaitExitCode = 2 };
        using HttpClient Client = new HttpClient(Handler);
        UpdateService Service = new UpdateService(Runner, Client, InstallDir.DirectoryPath, FakeSevenZipExe);

        UpdateExecutionResult Fail7z = await Service.DownloadAndInstallAsync(
            new UpdateInfoModel { DownloadUrl = "https://example.com/app.7z" });
        Assert.Equal(UpdateExecutionStatus.ExtractionFailed, Fail7z.Status);

        Runner.RunAndWaitExitCode = 0;
        Runner.OnRunAndWait = (FilePath, Arguments) =>
        {
            int OutputStart = Arguments.IndexOf("-o\"") + 3;
            int OutputEnd = Arguments.IndexOf('"', OutputStart);
            string ExtractDir = Arguments.Substring(OutputStart, OutputEnd - OutputStart);
            File.WriteAllText(Path.Combine(ExtractDir, AppConstants.ExecutableFileName), "exe");
        };

        UpdateExecutionResult Ok7z = await Service.DownloadAndInstallAsync(
            new UpdateInfoModel { DownloadUrl = "https://example.com/app.7z" });
        Assert.Equal(UpdateExecutionStatus.SuccessLaunchedUpdater, Ok7z.Status);

        string TempUpdateDir = Path.GetDirectoryName(Path.GetDirectoryName(Ok7z.ExtractedFilePath)!)!;
        if (Directory.Exists(TempUpdateDir))
        {
            Directory.Delete(TempUpdateDir, true);
        }

        Service.OpenReleasePage(new UpdateInfoModel { HtmlUrl = "" });
        Assert.Empty(Runner.OpenedUrls);

        Service.OpenReleasePage(new UpdateInfoModel { HtmlUrl = "https://github.com/demo/releases/latest" });
        Assert.Single(Runner.OpenedUrls);
    }

    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> Handler;

        public SyncProgress(Action<T> Handler)
        {
            this.Handler = Handler;
        }

        public void Report(T Value)
        {
            Handler(Value);
        }
    }
}
