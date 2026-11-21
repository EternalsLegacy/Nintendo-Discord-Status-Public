using System;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Helpers;
using NintendoDiscordStatus.Core.Models;
using Xunit;

namespace NintendoDiscordStatus.Tests.Helpers;

public class PresenceFormatterTests
{
    [Theory]
    [InlineData("Home", true)]
    [InlineData("  home ", true)]
    [InlineData("Mario Kart 8", false)]
    [InlineData(null, false)]
    public void IsHomeGame_MatchesCaseInsensitively(string? GameName, bool Expected)
    {
        Assert.Equal(Expected, PresenceFormatter.IsHomeGame(GameName));
    }

    [Theory]
    [InlineData("https://example.com/icon.png", true)]
    [InlineData("http://localhost:8080", true)]
    [InlineData("ftp://example.com/file", false)]
    [InlineData(@"C:\Games\cover.png", false)]
    [InlineData("botw.png", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    public void IsHttpUrl_ValidatesHttpAndHttpsOnly(string? Url, bool Expected)
    {
        Assert.Equal(Expected, PresenceFormatter.IsHttpUrl(Url));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("nintendo_switch_icon_v2", "nintendo_switch_icon_v2.png")]
    [InlineData("ssbb.jpg", "ssbb.jpg")]
    [InlineData("https://example.com/custom", "https://example.com/custom")]
    [InlineData(@"C:\Images\cover", @"C:\Images\cover")]
    public void NormalizeAssetFileName_AppendsPngOnlyWhenExtensionMissingAndLocalAssetKey(
        string? InputKey,
        string ExpectedKey)
    {
        Assert.Equal(ExpectedKey, PresenceFormatter.NormalizeAssetFileName(InputKey));
    }

    [Fact]
    public void Truncate_HandlesNullNonPositiveAndLongText()
    {
        Assert.Equal(string.Empty, PresenceFormatter.Truncate(null, 10));
        Assert.Equal(string.Empty, PresenceFormatter.Truncate("abc", 0));
        Assert.Equal("abc", PresenceFormatter.Truncate("  abc  ", 10));
        Assert.Equal("abcde", PresenceFormatter.Truncate("abcdefgh", 5));
    }

    [Fact]
    public void ResolveDiscordImageKey_HandlesUrlsLocalPathsExtensionlessKeysAndFallbacks()
    {
        Assert.Equal(string.Empty, PresenceFormatter.ResolveDiscordImageKey(null, null));
        Assert.Equal(
            AppConstants.GitHubImagesBaseUrl + "switch.png",
            PresenceFormatter.ResolveDiscordImageKey("", "switch"));
        Assert.Equal(
            "https://cdn.example.org/custom.png",
            PresenceFormatter.ResolveDiscordImageKey("  https://cdn.example.org/custom.png ", "switch"));
        Assert.Equal(
            AppConstants.GitHubImagesBaseUrl + "switch.png",
            PresenceFormatter.ResolveDiscordImageKey(@"C:\CustomImages\game.png", "switch"));
        Assert.Equal(
            string.Empty,
            PresenceFormatter.ResolveDiscordImageKey(@"C:\CustomImages\game.png", null));
        Assert.Equal(
            AppConstants.GitHubImagesBaseUrl + "botw.png",
            PresenceFormatter.ResolveDiscordImageKey("botw", "switch"));
        Assert.Equal(
            AppConstants.GitHubImagesBaseUrl + "ssbb.jpg",
            PresenceFormatter.ResolveDiscordImageKey("ssbb.jpg", "wii"));
    }

    [Fact]
    public void FormatElapsedTime_ClampsNegativeAndFormatsCompactMinutesAndHours()
    {
        Assert.Equal("00:00", PresenceFormatter.FormatElapsedTime(TimeSpan.FromSeconds(-12)));
        Assert.Equal("05:09", PresenceFormatter.FormatElapsedTime(new TimeSpan(0, 5, 9)));
        Assert.Equal("02:14:03", PresenceFormatter.FormatElapsedTime(new TimeSpan(2, 14, 3)));
    }

    [Fact]
    public void FormatDetailsAndState_AndBuildDiscordState_HandleAllCombinations()
    {
        Assert.Equal("Exploring • Singleplayer", PresenceFormatter.FormatDetailsAndState("Exploring", "Singleplayer"));
        Assert.Equal("Exploring", PresenceFormatter.FormatDetailsAndState("Exploring", ""));
        Assert.Equal("Singleplayer", PresenceFormatter.FormatDetailsAndState("", "Singleplayer"));
        Assert.Equal(string.Empty, PresenceFormatter.FormatDetailsAndState("  ", null));

        PresenceRequest HomeReq = new PresenceRequest { GameName = "Home", Details = "Ignored" };
        Assert.Equal(AppConstants.HomeIdleText, PresenceFormatter.BuildDiscordState(HomeReq));

        PresenceRequest DetailedReq = new PresenceRequest { GameName = "Zelda", Details = "Dungeon", State = "Master" };
        Assert.Equal("Dungeon • Master", PresenceFormatter.BuildDiscordState(DetailedReq));

        PresenceRequest EmptyGameReq = new PresenceRequest { GameName = "Splatoon 3" };
        Assert.Null(PresenceFormatter.BuildDiscordState(EmptyGameReq));
    }

    [Fact]
    public void BuildLivePreview_ProducesClassicDetailsStateAndTimerLines()
    {
        DateTime Start = new DateTime(2026, 11, 11, 18, 0, 0, DateTimeKind.Utc);
        DateTime Now = Start.AddMinutes(7).AddSeconds(42);

        LivePreviewModel HomePreview = PresenceFormatter.BuildLivePreview(
            new PresenceRequest
            {
                ConsoleName = "Nintendo Wii U",
                GameName = "Home",
                State = "Menu",
                ImageKey = "wiiu",
                ShowTimer = true
            },
            Start,
            Now);

        Assert.Equal(AppConstants.HomeGameName, HomePreview.GameTitle);
        Assert.Equal(AppConstants.HomeIdleText, HomePreview.DetailsLine);
        Assert.Equal("Menu", HomePreview.StateLine);
        Assert.Equal(ThemeConstants.BadgeWiiU, HomePreview.SmallBadgeText);
        Assert.True(HomePreview.IsTimerVisible);
        Assert.Equal("00:00", HomePreview.ElapsedText);
        Assert.Null(HomePreview.SessionStartUtc);

        LivePreviewModel FallbackPreview = PresenceFormatter.BuildLivePreview(
            new PresenceRequest
            {
                ConsoleName = "",
                GameName = "",
                ShowTimer = true
            },
            null,
            Now);

        Assert.Equal(AppConstants.UnknownGameText, FallbackPreview.GameTitle);
        Assert.Equal(string.Empty, FallbackPreview.DetailsLine);
        Assert.Equal(string.Empty, FallbackPreview.StateLine);
        Assert.Equal("00:00", FallbackPreview.ElapsedText);

        LivePreviewModel ActivePreview = PresenceFormatter.BuildLivePreview(
            new PresenceRequest
            {
                ConsoleName = "Nintendo Switch",
                GameName = "Mario Kart 8 Deluxe",
                Details = "Online VS",
                State = "Singleplayer",
                ImageKey = "mk8d",
                ShowTimer = true
            },
            Start,
            Now);

        Assert.Equal("Mario Kart 8 Deluxe", ActivePreview.GameTitle);
        Assert.Equal("Online VS", ActivePreview.DetailsLine);
        Assert.Equal("Singleplayer", ActivePreview.StateLine);
        Assert.Equal("mk8d", ActivePreview.ImageKey);
        Assert.True(ActivePreview.IsTimerVisible);
        Assert.Equal("07:42", ActivePreview.ElapsedText);
        Assert.Equal(Start, ActivePreview.SessionStartUtc);

        LivePreviewModel HiddenTimerPreview = PresenceFormatter.BuildLivePreview(
            new PresenceRequest
            {
                ConsoleName = "Nintendo Switch",
                GameName = "Splatoon 3",
                Details = "Turf War",
                ShowTimer = false
            },
            Start,
            Now);

        Assert.False(HiddenTimerPreview.IsTimerVisible);
        Assert.Equal(string.Empty, HiddenTimerPreview.ElapsedText);
    }
}
