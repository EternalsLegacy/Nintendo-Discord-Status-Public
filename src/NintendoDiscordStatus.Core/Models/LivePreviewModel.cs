using System;

namespace NintendoDiscordStatus.Core.Models;

public class LivePreviewModel
{
    public string GameTitle { get; set; } = string.Empty;
    public string DetailsLine { get; set; } = string.Empty;
    public string StateLine { get; set; } = string.Empty;
    public string ImageKey { get; set; } = string.Empty;
    public string SmallBadgeText { get; set; } = string.Empty;
    public string ElapsedText { get; set; } = string.Empty;
    public bool IsTimerVisible { get; set; }
    public DateTime? SessionStartUtc { get; set; }
}
