using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;
using NintendoDiscordStatus.Core.Presenters;
using NintendoDiscordStatus.Core.Services;
using NintendoDiscordStatus.Tests.Fakes;
using Xunit;

namespace NintendoDiscordStatus.Tests.Presenters;

public class MainPresenterTests
{
    [Fact]
    public async Task InitializeAsync_RestoresSavedStateSyncsLibraryChecksUpdatesAndCachesImageByKey()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        FakeTimeProvider TimeProvider = new FakeTimeProvider();
        FakeDiscordRpcClientFactory DiscordFactory = new FakeDiscordRpcClientFactory();
        FakeProcessRunner ProcessRunner = new FakeProcessRunner();

        GamesLibModel InitialLib = new GamesLibModel
        {
            Consoles =
            {
                new ConsoleModel
                {
                    ConsoleName = "Nintendo Switch",
                    ConsoleIconKey = "nintendo_switch_icon_v2",
                    Games =
                    {
                        new GameModel { GameName = "Home", ImageIconKey = "nintendo_switch_icon_v2" },
                        new GameModel { GameName = "BotW", ImageIconKey = "botw" },
                        new GameModel { GameName = "TotK", ImageIconKey = "totk" }
                    }
                },
                new ConsoleModel
                {
                    ConsoleName = "Nintendo Wii U",
                    ConsoleIconKey = "wiiu",
                    Games =
                    {
                        new GameModel { GameName = "Splatoon", ImageIconKey = "splatoon" }
                    }
                }
            }
        };
        File.WriteAllText(AppPaths.GetGameLibraryFilePath(TempDir.DirectoryPath), JsonSerializer.Serialize(InitialLib));

        SettingsService PreSettings = new SettingsService(TempDir.DirectoryPath, TimeProvider);
        PreSettings.Update(Config =>
        {
            Config.RememberLastStatus = true;
            Config.AutoSyncGameLibrary = true;
            Config.AutoCheckUpdates = true;
            Config.LastConsole = "Nintendo Switch";
            Config.LastGame = "BotW";
            Config.LastDetails = "Exploring";
            Config.LastState = "Singleplayer";
        });

        int ImageHttpCalls = 0;
        byte[] BotwBytes = { 1, 1, 1 };
        byte[] TotkBytes = { 2, 2, 2 };

        FakeHttpMessageHandler HttpHandler = new FakeHttpMessageHandler(Request =>
        {
            string Url = Request.RequestUri!.ToString();

            if (Url == AppConstants.GitHubRawGameLibUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(InitialLib))
                };
            }

            if (Url == AppConstants.GitHubApiLatestReleaseUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new GitHubReleaseModel
                    {
                        TagName = "v1.3.0",
                        Body = "# Big Update"
                    }))
                };
            }

            ImageHttpCalls++;
            byte[] Payload = Url.EndsWith("totk.png") ? TotkBytes : BotwBytes;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Payload)
            };
        });

        using HttpClient Client = new HttpClient(HttpHandler);
        SettingsService Settings = new SettingsService(TempDir.DirectoryPath, TimeProvider);
        LibraryService Library = new LibraryService(TempDir.DirectoryPath, Client);
        PresetService Presets = new PresetService(TempDir.DirectoryPath);
        using DiscordService Discord = new DiscordService(DiscordFactory, TimeProvider);
        ImageCacheService ImageCache = new ImageCacheService(Client, TempDir.DirectoryPath, TempDir.DirectoryPath);
        UpdateService Updates = new UpdateService(ProcessRunner, Client, TempDir.DirectoryPath);

        using MainPresenter Presenter = new MainPresenter(
            Settings,
            Library,
            Presets,
            Discord,
            ImageCache,
            Updates,
            TimeProvider);

        UpdateInfoModel? ObservedUpdate = null;
        string ObservedAccent = string.Empty;
        byte[]? ObservedImage = null;
        PresenceRequest? RestoredRequest = null;
        LivePreviewModel? LatestPreview = null;
        int ImageEvents = 0;
        int LibUpdatedEvents = 0;
        List<DiscordConnectionState> ConnectionEvents = new List<DiscordConnectionState>();
        List<string> ErrorEvents = new List<string>();

        Presenter.StateRestored += (Sender, Req) => RestoredRequest = Req;
        Presenter.PreviewUpdated += (Sender, Preview) => LatestPreview = Preview;
        Presenter.UpdateAvailable += (Sender, Info) => ObservedUpdate = Info;
        Presenter.ThemeUpdated += (Sender, Accent) => ObservedAccent = Accent;
        Presenter.PreviewImageReady += (Sender, Bytes) =>
        {
            ImageEvents++;
            ObservedImage = Bytes;
        };
        Presenter.LibraryUpdated += (Sender, Args) => LibUpdatedEvents++;
        Presenter.ConnectionStateChanged += (Sender, State) => ConnectionEvents.Add(State);
        Presenter.DiscordError += (Sender, Error) => ErrorEvents.Add(Error);

        await Presenter.InitializeAsync();

        Assert.NotNull(RestoredRequest);
        Assert.NotNull(LatestPreview);
        Assert.Equal("BotW", LatestPreview!.GameTitle);
        Assert.Equal("Exploring", LatestPreview.DetailsLine);
        Assert.Equal("Singleplayer", LatestPreview.StateLine);
        Assert.Equal("Nintendo Switch", Presenter.CurrentDraft.ConsoleName);
        Assert.Equal("BotW", Presenter.CurrentDraft.GameName);
        Assert.Equal(ThemeConstants.SwitchAccentHex, ObservedAccent);
        Assert.Equal(BotwBytes, ObservedImage);
        Assert.Equal(1, ImageHttpCalls);
        Assert.Equal(1, LibUpdatedEvents);
        Assert.NotNull(ObservedUpdate);
        Assert.True(ObservedUpdate!.IsUpdateAvailable);
        Assert.False(Presenter.IsPresenceActive);
        Assert.Empty(Settings.Current.RecentGames);

        DiscordFactory.CreatedClients[0].SimulateReady("Tester");
        Assert.Equal(DiscordConnectionState.Connected, Presenter.ConnectionState);
        Assert.True(Presenter.ApplyStatus(Presenter.CurrentDraft, RecordAsRecent: true));
        Assert.True(Presenter.IsPresenceActive);

        DiscordFactory.CreatedClients[0].SimulateError("Minor Warning");
        Assert.Equal(new[] { "Minor Warning" }, ErrorEvents);
        Assert.Contains(DiscordConnectionState.Connected, ConnectionEvents);

        Presenter.UpdatePreviewDraft(new PresenceRequest
        {
            ConsoleName = "Nintendo Switch",
            GameName = "BotW",
            Details = "Shrines",
            State = "120/120"
        });
        Assert.Equal(1, ImageHttpCalls);
        Assert.Equal(1, ImageEvents);

        Presenter.UpdatePreviewDraft(new PresenceRequest
        {
            ConsoleName = "Nintendo Wii U",
            GameName = "Unknown Homebrew"
        });
        await Task.Delay(50);
        Assert.Equal(ThemeConstants.WiiUAccentHex, ObservedAccent);
        Assert.Null(ObservedImage);

        Presenter.UpdatePreviewDraft(new PresenceRequest
        {
            ConsoleName = "Nintendo Wii U",
            GameName = "Home"
        });
        await Task.Delay(50);
        Assert.Equal("wiiu", Presenter.CurrentDraft.ImageKey);

        Presenter.ResetTimer();
        Presenter.StopPresence();
        Assert.False(Presenter.IsPresenceActive);
        Presenter.ResetTimer();
    }

    [Fact]
    public async Task QuickSwitchPresetsCustomGamesSettingsAndCancelledInitialize_WorkCleanly()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        FakeTimeProvider TimeProvider = new FakeTimeProvider();
        FakeDiscordRpcClientFactory DiscordFactory = new FakeDiscordRpcClientFactory();
        FakeProcessRunner ProcessRunner = new FakeProcessRunner();

        GamesLibModel InitialLib = new GamesLibModel
        {
            Consoles =
            {
                new ConsoleModel
                {
                    ConsoleName = "Nintendo Switch",
                    ConsoleIconKey = "switch",
                    Games = { new GameModel { GameName = "Splatoon 3", ImageIconKey = "s3" } }
                }
            }
        };
        File.WriteAllText(AppPaths.GetGameLibraryFilePath(TempDir.DirectoryPath), JsonSerializer.Serialize(InitialLib));

        FakeHttpMessageHandler Handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));

        using HttpClient Client = new HttpClient(Handler);
        SettingsService Settings = new SettingsService(TempDir.DirectoryPath, TimeProvider);
        Settings.Update(Config =>
        {
            Config.RememberLastStatus = false;
            Config.AutoSyncGameLibrary = false;
            Config.AutoCheckUpdates = false;
        });

        LibraryService Library = new LibraryService(TempDir.DirectoryPath, Client);
        PresetService Presets = new PresetService(TempDir.DirectoryPath);
        using DiscordService Discord = new DiscordService(DiscordFactory, TimeProvider);
        ImageCacheService ImageCache = new ImageCacheService(Client, TempDir.DirectoryPath, TempDir.DirectoryPath);
        UpdateService Updates = new UpdateService(ProcessRunner, Client, TempDir.DirectoryPath);

        using MainPresenter Presenter = new MainPresenter(
            Settings,
            Library,
            Presets,
            Discord,
            ImageCache,
            Updates);

        await Presenter.InitializeAsync();
        Assert.Equal(AppConstants.HomeGameName, Presenter.CurrentDraft.GameName);
        Assert.Equal(AppConstants.HomeIdleText, Presenter.CurrentDraft.Details);

        Assert.False(await Presenter.SyncLibraryAsync());
        Assert.Null(await Presenter.CheckForUpdatesAsync());

        int RecentEvents = 0;
        List<PresenceRequest> RestoredStates = new List<PresenceRequest>();
        Presenter.RecentGamesUpdated += (Sender, Args) => RecentEvents++;
        Presenter.StateRestored += (Sender, Req) => RestoredStates.Add(Req);

        Presenter.QuickSwitchGame("Nintendo Switch", "Splatoon 3");
        Assert.Equal(1, RecentEvents);
        Assert.Single(RestoredStates);
        Assert.Equal("Splatoon 3", RestoredStates[0].GameName);
        Assert.Single(Presenter.AppSettings.RecentGames);

        Presenter.CurrentDraft.Details = "Anarchy";
        Presenter.CurrentDraft.State = "S+0";
        Assert.True(Presenter.SaveCurrentAsPreset("Ranked Splat"));
        Assert.Single(Presets.Presets);

        Presenter.ApplyPreset(Presets.Presets[0]);
        Assert.Equal("Anarchy", Presenter.CurrentDraft.Details);
        Assert.Equal(2, RestoredStates.Count);

        int LibraryEvents = 0;
        Presenter.LibraryUpdated += (Sender, Args) => LibraryEvents++;
        Assert.False(Presenter.AddCustomGame("", "Bad", "bad.png"));
        Assert.True(Presenter.AddCustomGame("Nintendo Switch", "Celeste", "celeste.png"));
        Assert.True(Presenter.AddCustomGame("Steam Deck", "Hades", "hades.png"));
        Assert.Equal(2, LibraryEvents);

        Presenter.UpdateSettings(Config =>
        {
            Config.RememberLastStatus = true;
            Config.LastConsole = "";
            Config.LastGame = "";
            Config.ShowElapsedTimer = false;
        });

        Assert.False(Presenter.GetCurrentLivePreview().IsTimerVisible);

        using CancellationTokenSource PreCancelled = new CancellationTokenSource();
        PreCancelled.Cancel();
        Settings.Update(Config =>
        {
            Config.RememberLastStatus = true;
            Config.AutoSyncGameLibrary = true;
            Config.AutoCheckUpdates = true;
        });

        await Presenter.InitializeAsync(PreCancelled.Token);
        Assert.Equal(AppConstants.DefaultConsoleName, Presenter.CurrentDraft.ConsoleName);
        Assert.Equal(AppConstants.HomeGameName, Presenter.CurrentDraft.GameName);
    }

    [Fact]
    public async Task RapidImageSwitching_CancelsPreviousInFlightImageRequest()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        FakeDiscordRpcClientFactory DiscordFactory = new FakeDiscordRpcClientFactory();
        FakeProcessRunner ProcessRunner = new FakeProcessRunner();

        GamesLibModel Lib = new GamesLibModel
        {
            Consoles =
            {
                new ConsoleModel
                {
                    ConsoleName = "Nintendo Switch",
                    ConsoleIconKey = "switch",
                    Games =
                    {
                        new GameModel { GameName = "SlowGame", ImageIconKey = "slow" },
                        new GameModel { GameName = "FastGame", ImageIconKey = "fast" }
                    }
                }
            }
        };
        File.WriteAllText(AppPaths.GetGameLibraryFilePath(TempDir.DirectoryPath), JsonSerializer.Serialize(Lib));

        TaskCompletionSource<bool> SlowGate = new TaskCompletionSource<bool>();
        byte[] FastBytes = { 7, 7, 7 };

        FakeHttpMessageHandler Handler = new FakeHttpMessageHandler(async (Request, Token) =>
        {
            if (Request.RequestUri!.ToString().EndsWith("slow.png"))
            {
                using (Token.Register(() => SlowGate.TrySetCanceled()))
                {
                    await SlowGate.Task;
                }
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(FastBytes)
            };
        });

        using HttpClient Client = new HttpClient(Handler);
        SettingsService Settings = new SettingsService(TempDir.DirectoryPath);
        LibraryService Library = new LibraryService(TempDir.DirectoryPath, Client);
        PresetService Presets = new PresetService(TempDir.DirectoryPath);
        using DiscordService Discord = new DiscordService(DiscordFactory);
        ImageCacheService ImageCache = new ImageCacheService(Client, TempDir.DirectoryPath, TempDir.DirectoryPath);
        UpdateService Updates = new UpdateService(ProcessRunner, Client, TempDir.DirectoryPath);

        Library.Load();

        using MainPresenter Presenter = new MainPresenter(
            Settings,
            Library,
            Presets,
            Discord,
            ImageCache,
            Updates);

        List<byte[]?> ReceivedImages = new List<byte[]?>();
        Presenter.PreviewImageReady += (Sender, Bytes) => ReceivedImages.Add(Bytes);

        Presenter.UpdatePreviewDraft(new PresenceRequest { ConsoleName = "Nintendo Switch", GameName = "SlowGame" });
        Presenter.UpdatePreviewDraft(new PresenceRequest { ConsoleName = "Nintendo Switch", GameName = "FastGame" });

        await Task.Delay(50);

        Assert.Single(ReceivedImages);
        Assert.Equal(FastBytes, ReceivedImages[0]);
    }

    [Fact]
    public async Task Presenter_FunctionsSafelyWithoutEventSubscribersAndChecksUpdatesWhenActive()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        FakeDiscordRpcClientFactory DiscordFactory = new FakeDiscordRpcClientFactory();
        FakeProcessRunner ProcessRunner = new FakeProcessRunner();

        GamesLibModel Lib = new GamesLibModel
        {
            Consoles =
            {
                new ConsoleModel
                {
                    ConsoleName = "Nintendo Switch",
                    ConsoleIconKey = "switch",
                    Games = { new GameModel { GameName = "Metroid", ImageIconKey = "metroid" } }
                }
            }
        };
        File.WriteAllText(AppPaths.GetGameLibraryFilePath(TempDir.DirectoryPath), JsonSerializer.Serialize(Lib));

        FakeHttpMessageHandler Handler = new FakeHttpMessageHandler(Request =>
        {
            string Url = Request.RequestUri!.ToString();
            if (Url == AppConstants.GitHubRawGameLibUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(Lib))
                };
            }

            if (Url == AppConstants.GitHubApiLatestReleaseUrl)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(new GitHubReleaseModel
                    {
                        TagName = "v1.4.0",
                        Body = "Newer version without subscribers"
                    }))
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 4, 5, 6 })
            };
        });

        using HttpClient Client = new HttpClient(Handler);
        SettingsService Settings = new SettingsService(TempDir.DirectoryPath);
        LibraryService Library = new LibraryService(TempDir.DirectoryPath, Client);
        PresetService Presets = new PresetService(TempDir.DirectoryPath);
        using DiscordService Discord = new DiscordService(DiscordFactory);
        ImageCacheService ImageCache = new ImageCacheService(Client, TempDir.DirectoryPath, TempDir.DirectoryPath);
        UpdateService Updates = new UpdateService(ProcessRunner, Client, TempDir.DirectoryPath);

        using MainPresenter Presenter = new MainPresenter(
            Settings,
            Library,
            Presets,
            Discord,
            ImageCache,
            Updates);

        await Presenter.InitializeAsync();
        UpdateInfoModel? AvailableInfo = await Presenter.CheckForUpdatesAsync();
        Assert.NotNull(AvailableInfo);
        Assert.True(AvailableInfo!.IsUpdateAvailable);

        DiscordFactory.CreatedClients[0].SimulateError("Err without UI handler");
        Presenter.UpdatePreviewDraft(new PresenceRequest { ConsoleName = "Atari", GameName = "Pong" });
        Assert.Equal(string.Empty, Presenter.CurrentDraft.ConsoleIconKey);

        Presenter.QuickSwitchGame("Nintendo Switch", "Metroid");
        Presenter.ApplyPreset(new PresetModel { ConsoleName = "Nintendo Switch", GameName = "Metroid" });
        Presenter.ResetTimer();
        Presenter.UpdateSettings(Config => Config.DynamicConsoleTheme = false);
        Assert.True(Presenter.IsPresenceActive);

        Presenter.StopPresence();
        Presenter.UpdateSettings(Config => Config.DynamicConsoleTheme = true);
        Assert.False(Presenter.IsPresenceActive);

        Assert.True(Presenter.AddCustomGame("Nintendo Switch", "Custom Metroid", "cm.png"));
        Assert.True(await Presenter.SyncLibraryAsync());
    }
}
