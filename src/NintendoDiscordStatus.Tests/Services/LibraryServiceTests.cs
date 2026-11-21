using System.Collections.Generic;
using System.IO;
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

public class LibraryServiceTests
{
    [Fact]
    public void Load_MergesBuiltInAndCustomGamesWithoutDuplicates()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        GamesLibModel BaseLib = new GamesLibModel
        {
            Consoles =
            {
                new ConsoleModel
                {
                    ConsoleName = "Nintendo Switch",
                    ConsoleIconKey = "switch.png",
                    Games =
                    {
                        new GameModel { GameName = "Home", ImageIconKey = "switch.png" },
                        new GameModel { GameName = "Mario Kart 8", ImageIconKey = "mk8.png" }
                    }
                }
            }
        };

        GamesLibModel CustomLib = new GamesLibModel
        {
            Consoles =
            {
                new ConsoleModel
                {
                    ConsoleName = "Nintendo Switch",
                    ConsoleIconKey = "switch.png",
                    Games =
                    {
                        new GameModel { GameName = "Mario Kart 8", ImageIconKey = "custom_mk8.png" },
                        new GameModel { GameName = "Hollow Knight", ImageIconKey = "https://example.com/hk.png" }
                    }
                },
                new ConsoleModel
                {
                    ConsoleName = "Nintendo 3DS",
                    ConsoleIconKey = "3ds.png",
                    Games =
                    {
                        new GameModel { GameName = "Animal Crossing", ImageIconKey = "acnl.png" }
                    }
                }
            }
        };

        File.WriteAllText(AppPaths.GetGameLibraryFilePath(TempDir.DirectoryPath), JsonSerializer.Serialize(BaseLib));
        File.WriteAllText(AppPaths.GetCustomGamesFilePath(TempDir.DirectoryPath), JsonSerializer.Serialize(CustomLib));

        LibraryService Service = new LibraryService(TempDir.DirectoryPath);
        Service.Load();

        IReadOnlyList<string> Consoles = Service.GetConsoleNames();
        Assert.Equal(2, Consoles.Count);

        GameModel? OverriddenGame = Service.FindGame("Nintendo Switch", "mario kart 8");
        Assert.NotNull(OverriddenGame);
        Assert.Equal("custom_mk8.png", OverriddenGame!.ImageIconKey);
        Assert.True(OverriddenGame.IsCustom);

        GameModel? CustomConsoleGame = Service.FindGame("Nintendo 3DS", "Animal Crossing");
        Assert.NotNull(CustomConsoleGame);
        Assert.True(CustomConsoleGame!.IsCustom);

        Assert.Null(Service.FindConsole(null));
        Assert.Null(Service.FindGame("Nintendo Switch", null));
        Assert.Empty(Service.GetGamesForConsole("Missing Console"));
    }

    [Fact]
    public void Load_HandlesMissingNullAndCorruptJsonGracefully()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        LibraryService DefaultService = new LibraryService();
        Assert.NotNull(DefaultService.Library);

        LibraryService Service = new LibraryService(TempDir.DirectoryPath);
        Service.Load();
        Assert.Empty(Service.GetConsoleNames());

        File.WriteAllText(AppPaths.GetGameLibraryFilePath(TempDir.DirectoryPath), "null");
        File.WriteAllText(AppPaths.GetCustomGamesFilePath(TempDir.DirectoryPath), "{ broken");
        Service.Load();
        Assert.Empty(Service.GetConsoleNames());
    }

    [Fact]
    public async Task SyncFromGitHubAsync_UpdatesBaseLibraryWhilePreservingCustomGames()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        string NestedDir = Path.Combine(TempDir.DirectoryPath, "Data");

        int CallNumber = 0;
        FakeHttpMessageHandler Handler = new FakeHttpMessageHandler(_ =>
        {
            CallNumber++;
            if (CallNumber == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            if (CallNumber == 2)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"Consoles\":[]}")
                };
            }

            if (CallNumber == 3)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null")
                };
            }

            if (CallNumber == 4)
            {
                throw new HttpRequestException("Offline");
            }

            GamesLibModel Remote = new GamesLibModel
            {
                Consoles =
                {
                    new ConsoleModel
                    {
                        ConsoleName = "Nintendo Switch",
                        ConsoleIconKey = "switch.png",
                        Games = { new GameModel { GameName = "Synced Game", ImageIconKey = "synced.png" } }
                    }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(Remote))
            };
        });

        using HttpClient Client = new HttpClient(Handler);
        LibraryService Service = new LibraryService(NestedDir, Client);

        Assert.True(Service.AddCustomGame("Nintendo Switch", "My Custom Game", "my.png", "switch.png"));

        Assert.False(await Service.SyncFromGitHubAsync());
        Assert.False(await Service.SyncFromGitHubAsync());
        Assert.False(await Service.SyncFromGitHubAsync());
        Assert.False(await Service.SyncFromGitHubAsync());
        Assert.True(await Service.SyncFromGitHubAsync());

        Assert.NotNull(Service.FindGame("Nintendo Switch", "Synced Game"));
        GameModel? CustomGame = Service.FindGame("Nintendo Switch", "My Custom Game");
        Assert.NotNull(CustomGame);
        Assert.True(CustomGame!.IsCustom);
    }

    [Fact]
    public void AddAndDeleteCustomGame_HandleValidationUpdatingAndConsoleCleanup()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        LibraryService Service = new LibraryService(TempDir.DirectoryPath);

        Assert.False(Service.AddCustomGame("", "Game", "img.png"));
        Assert.False(Service.AddCustomGame("Switch", " ", "img.png"));
        Assert.False(Service.DeleteCustomGame("Switch", "Missing"));

        Assert.True(Service.AddCustomGame("Nintendo Wii", "Wii Sports", "wiisports.png", "wii.png"));
        Assert.True(Service.AddCustomGame("Nintendo Wii", "wii sports", "wiisports_v2.png"));
        Assert.Equal("wiisports_v2.png", Service.FindGame("Nintendo Wii", "Wii Sports")?.ImageIconKey);

        Assert.True(Service.AddCustomGame("Nintendo Wii", "Galaxy", "galaxy.png"));
        Assert.False(Service.DeleteCustomGame("Nintendo Wii", "Unknown Game"));
        Assert.True(Service.DeleteCustomGame("Nintendo Wii", "Wii Sports"));
        Assert.Single(Service.GetGamesForConsole("Nintendo Wii"));

        Assert.True(Service.DeleteCustomGame("Nintendo Wii", "Galaxy"));
        Assert.Empty(Service.GetConsoleNames());
    }
}
