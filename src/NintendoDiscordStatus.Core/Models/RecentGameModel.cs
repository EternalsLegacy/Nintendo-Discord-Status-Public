using System;
using System.Text.Json.Serialization;

namespace NintendoDiscordStatus.Core.Models;

public class RecentGameModel
{
    [JsonPropertyName("ConsoleName")]
    public string ConsoleName { get; set; } = string.Empty;

    [JsonPropertyName("GameName")]
    public string GameName { get; set; } = string.Empty;

    [JsonPropertyName("LastPlayedUtc")]
    public DateTimeOffset LastPlayedUtc { get; set; }
}
