using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Helpers;
using Xunit;

namespace NintendoDiscordStatus.Tests.Helpers;

public class ThemeHelperTests
{
    [Theory]
    [InlineData("Nintendo Switch", false, ThemeConstants.DefaultAccentHex)]
    [InlineData("Nintendo Wii U", false, ThemeConstants.DefaultAccentHex)]
    [InlineData(null, true, ThemeConstants.DefaultAccentHex)]
    [InlineData("", true, ThemeConstants.DefaultAccentHex)]
    [InlineData("Super Nintendo", true, ThemeConstants.DefaultAccentHex)]
    [InlineData("Nintendo Switch", true, ThemeConstants.SwitchAccentHex)]
    [InlineData("Nintendo Wii U", true, ThemeConstants.WiiUAccentHex)]
    [InlineData("WiiU", true, ThemeConstants.WiiUAccentHex)]
    [InlineData("Nintendo Wii", true, ThemeConstants.WiiAccentHex)]
    [InlineData("New Nintendo 3DS XL", true, ThemeConstants.ThreeDsAccentHex)]
    [InlineData("Nintendo DS Lite", true, ThemeConstants.DsAccentHex)]
    public void GetAccentHexForConsole_ReturnsExpectedAccentHex(
        string? ConsoleName,
        bool DynamicThemeEnabled,
        string ExpectedHex)
    {
        string ActualHex = ThemeHelper.GetAccentHexForConsole(ConsoleName, DynamicThemeEnabled);

        Assert.Equal(ExpectedHex, ActualHex);
    }

    [Theory]
    [InlineData(null, ThemeConstants.BadgeUnknown)]
    [InlineData("   ", ThemeConstants.BadgeUnknown)]
    [InlineData("Nintendo Switch", ThemeConstants.BadgeSwitch)]
    [InlineData("Nintendo Wii U", ThemeConstants.BadgeWiiU)]
    [InlineData("Nintendo Wii", ThemeConstants.BadgeWii)]
    [InlineData("Nintendo 3DS", ThemeConstants.BadgeThreeDs)]
    [InlineData("Nintendo DS", ThemeConstants.BadgeDs)]
    [InlineData("nes", "NES")]
    [InlineData("GameCube", "GAME")]
    public void GetConsoleBadge_ReturnsExpectedBadge(string? ConsoleName, string ExpectedBadge)
    {
        string ActualBadge = ThemeHelper.GetConsoleBadge(ConsoleName);

        Assert.Equal(ExpectedBadge, ActualBadge);
    }
}
