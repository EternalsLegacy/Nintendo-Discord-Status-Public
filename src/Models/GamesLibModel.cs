#region Usings

using System.Collections.Generic;
using System.Text.Json.Serialization;

#endregion

namespace NintendoDiscordStatus.Models;

#region Public Classes 

public class GamesLibModel
{
    #region Public Properties

    [JsonPropertyName("Consoles")]
    public List<ConsoleModel> Consoles { get; set; } = new List<ConsoleModel>();

    #endregion
}

public class ConsoleModel
{
    #region Public Properties

    [JsonPropertyName("ConsoleName")]
    public string ConsoleName { get; set; } = string.Empty;

    [JsonPropertyName("ConsoleIconKey")]
    public string ConsoleIconKey { get; set; } = string.Empty;

    [JsonPropertyName("Games")]
    public List<GameModel> Games { get; set; } = new List<GameModel>();

    #endregion
}

public class GameModel
{
    #region Public Properties

    [JsonPropertyName("GameName")]
    public string GameName { get; set; } = string.Empty;

    [JsonPropertyName("ImageIconKey")]
    public string ImageIconKey { get; set; } = string.Empty;

    #endregion
}

#endregion