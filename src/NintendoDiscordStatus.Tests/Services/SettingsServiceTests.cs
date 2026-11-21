using System;
using System.IO;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;
using NintendoDiscordStatus.Core.Services;
using NintendoDiscordStatus.Tests.Fakes;
using Xunit;

namespace NintendoDiscordStatus.Tests.Services;

public class SettingsServiceTests
{
    [Fact]
    public void Global_ReturnsSameSingletonInstance()
    {
        SettingsService First = SettingsService.Global;
        SettingsService Second = SettingsService.Global;

        Assert.Same(First, Second);
        Assert.NotNull(First.Current);
    }

    [Fact]
    public void Load_WhenFileMissingNullOrCorrupt_UsesDefaultsWithoutThrowing()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        SettingsService Service = new SettingsService(TempDir.DirectoryPath);

        Service.Load();
        Assert.True(Service.Current.AutoCheckUpdates);
        Assert.Equal(AppConstants.DefaultConsoleName, Service.Current.LastConsole);

        string SettingsPath = AppPaths.GetSettingsFilePath(TempDir.DirectoryPath);
        File.WriteAllText(SettingsPath, "null");
        Service.Load();
        Assert.True(Service.Current.MinimizeToTrayOnClose);

        File.WriteAllText(SettingsPath, "{ invalid json");
        Service.Load();
        Assert.True(Service.Current.ShowElapsedTimer);
    }

    [Fact]
    public void SaveUpdateAndResetToDefaults_PersistAndRaiseEvents()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        string NestedDir = Path.Combine(TempDir.DirectoryPath, "Config");
        SettingsService Service = new SettingsService(NestedDir);

        int EventCount = 0;
        Service.SettingsChanged += (Sender, Config) => EventCount++;

        Service.Update(Config =>
        {
            Config.AutoCheckUpdates = false;
            Config.DynamicConsoleTheme = false;
            Config.ShowElapsedTimer = false;
        });

        Assert.Equal(1, EventCount);
        Assert.True(File.Exists(AppPaths.GetSettingsFilePath(NestedDir)));

        SettingsService Reloaded = new SettingsService(NestedDir);
        Reloaded.Load();
        Assert.False(Reloaded.Current.AutoCheckUpdates);
        Assert.False(Reloaded.Current.DynamicConsoleTheme);
        Assert.False(Reloaded.Current.ShowElapsedTimer);

        Service.ResetToDefaults();
        Assert.Equal(2, EventCount);
        Assert.True(Service.Current.AutoCheckUpdates);
        Assert.True(Service.Current.DynamicConsoleTheme);
    }

    [Fact]
    public void AddRecentGame_IgnoresHomeDeduplicatesAndCapsAtMaxRecentGames()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        FakeTimeProvider TimeProvider = new FakeTimeProvider();
        SettingsService Service = new SettingsService(TempDir.DirectoryPath, TimeProvider);

        Service.AddRecentGame("", "Zelda");
        Service.AddRecentGame("Nintendo Switch", " ");
        Service.AddRecentGame("Nintendo Switch", "Home");
        Assert.Empty(Service.Current.RecentGames);

        Service.AddRecentGame("Nintendo Switch", "Game One");
        TimeProvider.Advance(TimeSpan.FromMinutes(1));
        Service.AddRecentGame("Nintendo Wii U", "Game Two");
        TimeProvider.Advance(TimeSpan.FromMinutes(1));
        Service.AddRecentGame("Nintendo Wii", "Game Three");
        TimeProvider.Advance(TimeSpan.FromMinutes(1));
        Service.AddRecentGame("nintendo switch", "game one");

        Assert.Equal(3, Service.Current.RecentGames.Count);
        Assert.Equal("game one", Service.Current.RecentGames[0].GameName);
        Assert.Equal("Game Three", Service.Current.RecentGames[1].GameName);
        Assert.Equal("Game Two", Service.Current.RecentGames[2].GameName);

        TimeProvider.Advance(TimeSpan.FromMinutes(1));
        Service.AddRecentGame("Nintendo Switch", "Game Four");

        Assert.Equal(AppConstants.MaxRecentGames, Service.Current.RecentGames.Count);
        Assert.Equal("Game Four", Service.Current.RecentGames[0].GameName);
        Assert.DoesNotContain(Service.Current.RecentGames, Item => Item.GameName == "Game Two");
    }

    [Fact]
    public void SaveLastStatus_RespectsRememberLastStatusAndPersistsFields()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        SettingsService Service = new SettingsService(TempDir.DirectoryPath);

        Service.Current.RememberLastStatus = false;
        Service.SaveLastStatus(new PresenceRequest
        {
            ConsoleName = "Nintendo Wii",
            GameName = "Super Mario Galaxy"
        });
        Assert.Equal(AppConstants.HomeGameName, Service.Current.LastGame);

        Service.Current.RememberLastStatus = true;
        Service.SaveLastStatus(new PresenceRequest
        {
            ConsoleName = "Nintendo Switch",
            GameName = "Mario Kart 8 Deluxe",
            Details = "Time Trials",
            State = "Singleplayer"
        });

        SettingsService Reloaded = new SettingsService(TempDir.DirectoryPath);
        Reloaded.Load();
        Assert.Equal("Nintendo Switch", Reloaded.Current.LastConsole);
        Assert.Equal("Mario Kart 8 Deluxe", Reloaded.Current.LastGame);
        Assert.Equal("Time Trials", Reloaded.Current.LastDetails);
        Assert.Equal("Singleplayer", Reloaded.Current.LastState);
    }
}
