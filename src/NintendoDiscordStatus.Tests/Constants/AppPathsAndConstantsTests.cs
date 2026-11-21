using System;
using System.Collections.Generic;
using System.IO;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Services;
using Xunit;

namespace NintendoDiscordStatus.Tests.Constants;

public class AppPathsAndConstantsTests
{
    [Fact]
    public void AppConstants_ExposeValidVersionTitleAndUrls()
    {
        Assert.Equal(new Version(1, 2, 1), AppConstants.CurrentVersion);
        Assert.Equal("1.2.1", AppConstants.CurrentVersionString);
        Assert.Equal("Nintendo Discord Rich Presence", AppConstants.WindowTitle);
        Assert.Equal("Singleplayer", AppConstants.DefaultStateExample);
        Assert.StartsWith("https://", AppConstants.GitHubApiLatestReleaseUrl);
        Assert.StartsWith("https://", AppConstants.GitHubImagesBaseUrl);
        Assert.EndsWith("game_lib.json", AppConstants.GitHubRawGameLibUrl);
    }

    [Fact]
    public void AppPaths_BuildExpectedPathsAndCandidateImageLocations()
    {
        string BaseDir = @"C:\AppBase";
        string CurrentDir = @"C:\WorkingDir";

        Assert.False(string.IsNullOrWhiteSpace(AppPaths.GetDefaultBaseDirectory()));
        Assert.Equal(Path.Combine(BaseDir, AppConstants.SettingsFileName), AppPaths.GetSettingsFilePath(BaseDir));
        Assert.Equal(Path.Combine(BaseDir, AppConstants.GameLibraryFileName), AppPaths.GetGameLibraryFilePath(BaseDir));
        Assert.Equal(Path.Combine(BaseDir, AppConstants.CustomGamesFileName), AppPaths.GetCustomGamesFilePath(BaseDir));
        Assert.Equal(Path.Combine(BaseDir, AppConstants.PresetsFileName), AppPaths.GetPresetsFilePath(BaseDir));
        Assert.Equal(Path.Combine(BaseDir, AppConstants.CrashLogFileName), AppPaths.GetCrashLogFilePath(BaseDir));

        IReadOnlyList<string> Candidates = AppPaths.GetCandidateImagePaths(BaseDir, CurrentDir, "zelda.png");
        Assert.Equal(3, Candidates.Count);
        Assert.Equal(Path.Combine(BaseDir, "images", "zelda.png"), Candidates[0]);
        Assert.Equal(Path.Combine(BaseDir, "..", "..", "..", "..", "images", "zelda.png"), Candidates[1]);
        Assert.Equal(Path.Combine(CurrentDir, "images", "zelda.png"), Candidates[2]);
    }

    [Fact]
    public void SystemTimeProvider_ReturnsCurrentUtcTime()
    {
        SystemTimeProvider Provider = new SystemTimeProvider();
        DateTime Before = DateTime.UtcNow.AddSeconds(-1);
        DateTime Actual = Provider.UtcNow;
        DateTime After = DateTime.UtcNow.AddSeconds(1);

        Assert.Equal(DateTimeKind.Utc, Actual.Kind);
        Assert.InRange(Actual, Before, After);
    }
}
