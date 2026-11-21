using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NintendoDiscordStatus.Core.Models;

public class GamesLibModel
{
    [JsonPropertyName("Consoles")]
    public List<ConsoleModel> Consoles { get; set; } = new List<ConsoleModel>();
}

public class ConsoleModel
{
    [JsonPropertyName("ConsoleName")]
    public string ConsoleName { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleIconKey")]
    public string ConsoleIconKey { get; set; } = string.Empty;

    [JsonPropertyName("Games")]
    public List<GameModel> Games { get; set; } = new List<GameModel>();
}

public class GameModel
{
    [JsonPropertyName("GameName")]
    public string GameName { get; set; } = string.Empty;

    [JsonPropertyName("ImageIconKey")]
    public string ImageIconKey { get; set; } = string.Empty;

    [JsonPropertyName("IsCustom")]
    public bool IsCustom { get; set; }
}
