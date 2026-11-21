namespace NintendoDiscordStatus.Core.Models;

public class PresenceRequest
{
    public string ConsoleName { get; set; } = string.Empty;
    public string ConsoleIconKey { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string ImageKey { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int TimerOffsetMinutes { get; set; }
    public bool ShowTimer { get; set; } = true;
}
