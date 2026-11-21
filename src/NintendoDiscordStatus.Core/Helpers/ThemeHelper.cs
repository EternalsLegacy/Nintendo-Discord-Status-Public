using System;
using NintendoDiscordStatus.Core.Constants;

namespace NintendoDiscordStatus.Core.Helpers;

public static class ThemeHelper
{
    private static readonly (string Keyword, string AccentHex, string BadgeText)[] ConsoleThemeRules =
    {
        ("wii u", ThemeConstants.WiiUAccentHex, ThemeConstants.BadgeWiiU),
        ("wiiu", ThemeConstants.WiiUAccentHex, ThemeConstants.BadgeWiiU),
        ("wii", ThemeConstants.WiiAccentHex, ThemeConstants.BadgeWii),
        ("3ds", ThemeConstants.ThreeDsAccentHex, ThemeConstants.BadgeThreeDs),
        ("ds", ThemeConstants.DsAccentHex, ThemeConstants.BadgeDs),
        ("switch", ThemeConstants.SwitchAccentHex, ThemeConstants.BadgeSwitch)
    };

    public static string GetAccentHexForConsole(string? ConsoleName, bool DynamicThemeEnabled)
    {
        if (!DynamicThemeEnabled || string.IsNullOrWhiteSpace(ConsoleName))
        {
            return ThemeConstants.DefaultAccentHex;
        }

        foreach ((string Keyword, string AccentHex, string _) in ConsoleThemeRules)
        {
            if (ConsoleName.Contains(Keyword, StringComparison.OrdinalIgnoreCase))
            {
                return AccentHex;
            }
        }

        return ThemeConstants.DefaultAccentHex;
    }

    public static string GetConsoleBadge(string? ConsoleName)
    {
        if (string.IsNullOrWhiteSpace(ConsoleName))
        {
            return ThemeConstants.BadgeUnknown;
        }

        foreach ((string Keyword, string _, string BadgeText) in ConsoleThemeRules)
        {
            if (ConsoleName.Contains(Keyword, StringComparison.OrdinalIgnoreCase))
            {
                return BadgeText;
            }
        }

        string TrimmedName = ConsoleName.Trim().ToUpperInvariant();
        return TrimmedName.Length <= 4 ? TrimmedName : TrimmedName.Substring(0, 4);
    }
}
