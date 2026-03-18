#region Usings

using System.Text.Json.Serialization;

#endregion

namespace NintendoDiscordStatus.Models;

#region Public Classes

public class PresetModel
{
    #region Public Properties

    [JsonPropertyName("PresetName")]
    public string PresetName { get; set; } = string.Empty;

    [JsonPropertyName("Console")]
    public string Console { get; set; } = string.Empty;

    [JsonPropertyName("Game")]
    public string Game { get; set; } = string.Empty;

    [JsonPropertyName("Details")]
    public string Details { get; set; } = string.Empty;

    [JsonPropertyName("State")]
    public string State { get; set; } = string.Empty;

    #endregion

    #region Public Methods

    public override string ToString() => PresetName;

    #endregion
}

#endregion