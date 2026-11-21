using System.Collections.Generic;
using System.Text.Json.Serialization;
using NintendoDiscordStatus.Core.Constants;

namespace NintendoDiscordStatus.Core.Models;

public class AppSettings
{
    [JsonPropertyName("AutoCheckUpdates")]
    public bool AutoCheckUpdates { get; set; } = true;

    [JsonPropertyName("AutoSyncGameLibrary")]
    public bool AutoSyncGameLibrary { get; set; } = true;

    [JsonPropertyName("RememberLastStatus")]
    public bool RememberLastStatus { get; set; } = true;

    [JsonPropertyName("MinimizeToTrayOnClose")]
    public bool MinimizeToTrayOnClose { get; set; } = true;

    [JsonPropertyName("DynamicConsoleTheme")]
    public bool DynamicConsoleTheme { get; set; } = true;

    [JsonPropertyName("ShowElapsedTimer")]
    public bool ShowElapsedTimer { get; set; } = true;

    [JsonPropertyName("LastConsole")]
    public string LastConsole { get; set; } = AppConstants.DefaultConsoleName;

    [JsonPropertyName("LastGame")]
    public string LastGame { get; set; } = AppConstants.HomeGameName;

    [JsonPropertyName("LastDetails")]
    public string LastDetails { get; set; } = AppConstants.HomeIdleText;

    [JsonPropertyName("LastState")]
    public string LastState { get; set; } = string.Empty;

    [JsonPropertyName("RecentGames")]
    public List<RecentGameModel> RecentGames { get; set; } = new List<RecentGameModel>();
}
