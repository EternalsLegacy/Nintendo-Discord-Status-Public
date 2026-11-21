using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;

namespace NintendoDiscordStatus.Core.Services;

public class LibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    private readonly string GameLibraryPath;
    private readonly string CustomGamesPath;
    private readonly HttpClient HttpClientInstance;

    public LibraryService(string? BaseDirectory = null, HttpClient? HttpClientInstance = null)
    {
        string EffectiveBaseDirectory = string.IsNullOrWhiteSpace(BaseDirectory)
            ? AppPaths.GetDefaultBaseDirectory()
            : BaseDirectory;

        GameLibraryPath = AppPaths.GetGameLibraryFilePath(EffectiveBaseDirectory);
        CustomGamesPath = AppPaths.GetCustomGamesFilePath(EffectiveBaseDirectory);
        this.HttpClientInstance = HttpClientInstance ?? new HttpClient();
        Library = new GamesLibModel();
    }

    public GamesLibModel Library { get; private set; }

    public void Load()
    {
        GamesLibModel BaseLibrary = ReadLibraryFile(GameLibraryPath);
        GamesLibModel CustomLibrary = ReadLibraryFile(CustomGamesPath);

        MergeCustomLibrary(BaseLibrary, CustomLibrary);
        Library = BaseLibrary;
    }

    public async Task<bool> SyncFromGitHubAsync(CancellationToken Token = default)
    {
        try
        {
            using HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Get, AppConstants.GitHubRawGameLibUrl);
            Request.Headers.TryAddWithoutValidation("User-Agent", AppConstants.UpdaterUserAgent);

            using HttpResponseMessage Response = await HttpClientInstance.SendAsync(Request, Token);
            if (!Response.IsSuccessStatusCode)
            {
                return false;
            }

            string JsonText = await Response.Content.ReadAsStringAsync(Token);
            GamesLibModel? RemoteLibrary = JsonSerializer.Deserialize<GamesLibModel>(JsonText);
            if (RemoteLibrary == null || RemoteLibrary.Consoles.Count == 0)
            {
                return false;
            }

            WriteLibraryFile(GameLibraryPath, RemoteLibrary);
            Load();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool AddCustomGame(string ConsoleName, string GameName, string ImageKeyOrPath, string ConsoleIconKey = "")
    {
        if (string.IsNullOrWhiteSpace(ConsoleName) || string.IsNullOrWhiteSpace(GameName))
        {
            return false;
        }

        string TrimmedConsole = ConsoleName.Trim();
        string TrimmedGame = GameName.Trim();
        string TrimmedImage = ImageKeyOrPath.Trim();

        GamesLibModel CustomLibrary = ReadLibraryFile(CustomGamesPath);
        ConsoleModel TargetConsole = FindOrCreateConsole(CustomLibrary, TrimmedConsole, ConsoleIconKey);

        GameModel? ExistingGame = TargetConsole.Games.FirstOrDefault(Game =>
            string.Equals(Game.GameName, TrimmedGame, StringComparison.OrdinalIgnoreCase));

        if (ExistingGame != null)
        {
            ExistingGame.ImageIconKey = TrimmedImage;
            ExistingGame.IsCustom = true;
        }
        else
        {
            TargetConsole.Games.Add(new GameModel
            {
                GameName = TrimmedGame,
                ImageIconKey = TrimmedImage,
                IsCustom = true
            });
        }

        WriteLibraryFile(CustomGamesPath, CustomLibrary);
        Load();
        return true;
    }

    public bool DeleteCustomGame(string ConsoleName, string GameName)
    {
        GamesLibModel CustomLibrary = ReadLibraryFile(CustomGamesPath);
        ConsoleModel? ConsoleItem = CustomLibrary.Consoles.FirstOrDefault(Item =>
            string.Equals(Item.ConsoleName, ConsoleName, StringComparison.OrdinalIgnoreCase));

        if (ConsoleItem == null)
        {
            return false;
        }

        int RemovedCount = ConsoleItem.Games.RemoveAll(Game =>
            string.Equals(Game.GameName, GameName, StringComparison.OrdinalIgnoreCase));

        if (RemovedCount == 0)
        {
            return false;
        }

        if (ConsoleItem.Games.Count == 0)
        {
            CustomLibrary.Consoles.Remove(ConsoleItem);
        }

        WriteLibraryFile(CustomGamesPath, CustomLibrary);
        Load();
        return true;
    }

    public IReadOnlyList<string> GetConsoleNames()
    {
        return Library.Consoles.Select(ConsoleItem => ConsoleItem.ConsoleName).ToList();
    }

    public ConsoleModel? FindConsole(string? ConsoleName)
    {
        if (string.IsNullOrWhiteSpace(ConsoleName))
        {
            return null;
        }

        return Library.Consoles.FirstOrDefault(ConsoleItem =>
            string.Equals(ConsoleItem.ConsoleName, ConsoleName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<GameModel> GetGamesForConsole(string? ConsoleName)
    {
        ConsoleModel? ConsoleItem = FindConsole(ConsoleName);
        return ConsoleItem != null ? ConsoleItem.Games : Array.Empty<GameModel>();
    }

    public GameModel? FindGame(string? ConsoleName, string? GameName)
    {
        if (string.IsNullOrWhiteSpace(GameName))
        {
            return null;
        }

        IReadOnlyList<GameModel> Games = GetGamesForConsole(ConsoleName);
        return Games.FirstOrDefault(Game =>
            string.Equals(Game.GameName, GameName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static GamesLibModel ReadLibraryFile(string FilePath)
    {
        if (!File.Exists(FilePath))
        {
            return new GamesLibModel();
        }

        try
        {
            string JsonText = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<GamesLibModel>(JsonText) ?? new GamesLibModel();
        }
        catch
        {
            return new GamesLibModel();
        }
    }

    private static void WriteLibraryFile(string FilePath, GamesLibModel Model)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        string JsonText = JsonSerializer.Serialize(Model, JsonOptions);
        File.WriteAllText(FilePath, JsonText);
    }

    private static void MergeCustomLibrary(GamesLibModel BaseLibrary, GamesLibModel CustomLibrary)
    {
        foreach (ConsoleModel CustomConsole in CustomLibrary.Consoles)
        {
            ConsoleModel TargetConsole = FindOrCreateConsole(
                BaseLibrary,
                CustomConsole.ConsoleName,
                CustomConsole.ConsoleIconKey);

            foreach (GameModel CustomGame in CustomConsole.Games)
            {
                GameModel? ExistingGame = TargetConsole.Games.FirstOrDefault(Game =>
                    string.Equals(Game.GameName, CustomGame.GameName, StringComparison.OrdinalIgnoreCase));

                if (ExistingGame != null)
                {
                    ExistingGame.ImageIconKey = CustomGame.ImageIconKey;
                    ExistingGame.IsCustom = true;
                }
                else
                {
                    TargetConsole.Games.Add(new GameModel
                    {
                        GameName = CustomGame.GameName,
                        ImageIconKey = CustomGame.ImageIconKey,
                        IsCustom = true
                    });
                }
            }
        }
    }

    private static ConsoleModel FindOrCreateConsole(GamesLibModel TargetLibrary, string ConsoleName, string ConsoleIconKey)
    {
        ConsoleModel? ExistingConsole = TargetLibrary.Consoles.FirstOrDefault(ConsoleItem =>
            string.Equals(ConsoleItem.ConsoleName, ConsoleName, StringComparison.OrdinalIgnoreCase));

        if (ExistingConsole != null)
        {
            return ExistingConsole;
        }

        ConsoleModel NewConsole = new ConsoleModel
        {
            ConsoleName = ConsoleName,
            ConsoleIconKey = ConsoleIconKey
        };

        TargetLibrary.Consoles.Add(NewConsole);
        return NewConsole;
    }
}
