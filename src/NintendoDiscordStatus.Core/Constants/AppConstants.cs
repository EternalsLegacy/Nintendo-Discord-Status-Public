using System;

namespace NintendoDiscordStatus.Core.Constants;

public static class AppConstants
{
    public static readonly Version CurrentVersion = new Version(1, 2, 0);
    public const string CurrentVersionString = "1.2.0";

    public const string WindowTitle = "Nintendo Discord Rich Presence";
    public const string DiscordAppId = "1482829273164284106";
    public const string GitHubOwner = "EternalsLegacy";
    public const string GitHubRepository = "Nintendo-Discord-Status-Public";
    public const string GitHubApiLatestReleaseUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepository}/releases/latest";
    public const string GitHubImagesBaseUrl = $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubRepository}/main/images/";
    public const string GitHubRawGameLibUrl = $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubRepository}/main/src/game_lib.json";
    public const string UpdaterUserAgent = "NintendoDiscordStatus-Updater";

    public const string DefaultConsoleName = "Nintendo Switch";
    public const string HomeGameName = "Home";
    public const string HomeIdleText = "Idling";
    public const string DefaultStateExample = "Singleplayer";
    public const string DefaultPlayingText = "Playing";
    public const string UnknownGameText = "Unknown Game";
    public const string DefaultIdleElapsedText = "00:00";
    public const string DefaultImageExtension = ".png";
    public const string ExecutableFileName = "NintendoDiscordStatus.exe";
    public const string DefaultSevenZipPath = @"C:\Program Files\7-Zip\7z.exe";

    public const string SettingsFileName = "settings.json";
    public const string GameLibraryFileName = "game_lib.json";
    public const string CustomGamesFileName = "custom_games.json";
    public const string PresetsFileName = "presets.json";
    public const string CrashLogFileName = "crash.log";
    public const string ImagesFolderName = "images";

    public const int MaxRecentGames = 3;
    public const int MaxDetailsLength = 128;
    public const int ImageRequestTimeoutSeconds = 15;
}
