using System;
using System.IO;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;

namespace NintendoDiscordStatus.Core.Helpers;

public static class PresenceFormatter
{
    public static bool IsHomeGame(string? GameName)
    {
        return string.Equals(GameName?.Trim(), AppConstants.HomeGameName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsHttpUrl(string? Value)
    {
        if (string.IsNullOrWhiteSpace(Value))
        {
            return false;
        }

        return Uri.TryCreate(Value.Trim(), UriKind.Absolute, out Uri? ParsedUri)
            && (ParsedUri.Scheme == Uri.UriSchemeHttp || ParsedUri.Scheme == Uri.UriSchemeHttps);
    }

    public static string NormalizeAssetFileName(string? ImageKey)
    {
        if (string.IsNullOrWhiteSpace(ImageKey))
        {
            return string.Empty;
        }

        string Trimmed = ImageKey.Trim();
        if (IsHttpUrl(Trimmed) || Path.IsPathRooted(Trimmed) || Path.HasExtension(Trimmed))
        {
            return Trimmed;
        }

        return Trimmed + AppConstants.DefaultImageExtension;
    }

    public static string Truncate(string? Value, int MaxLength)
    {
        if (string.IsNullOrWhiteSpace(Value) || MaxLength <= 0)
        {
            return string.Empty;
        }

        string Trimmed = Value.Trim();
        return Trimmed.Length <= MaxLength ? Trimmed : Trimmed.Substring(0, MaxLength);
    }

    public static string ResolveDiscordImageKey(string? ImageIconKey, string? FallbackIconKey = null)
    {
        string Normalized = NormalizeAssetFileName(ImageIconKey);
        if (string.IsNullOrEmpty(Normalized))
        {
            return string.IsNullOrWhiteSpace(FallbackIconKey)
                ? string.Empty
                : ResolveDiscordImageKey(FallbackIconKey, null);
        }

        if (IsHttpUrl(Normalized))
        {
            return Normalized;
        }

        if (Path.IsPathRooted(Normalized))
        {
            return string.IsNullOrWhiteSpace(FallbackIconKey)
                ? string.Empty
                : ResolveDiscordImageKey(FallbackIconKey, null);
        }

        return AppConstants.GitHubImagesBaseUrl + Normalized;
    }

    public static string FormatElapsedTime(TimeSpan Elapsed)
    {
        TimeSpan Clamped = Elapsed < TimeSpan.Zero ? TimeSpan.Zero : Elapsed;
        int TotalHours = (int)Clamped.TotalHours;

        return TotalHours >= 1
            ? $"{TotalHours:D2}:{Clamped.Minutes:D2}:{Clamped.Seconds:D2}"
            : $"{Clamped.Minutes:D2}:{Clamped.Seconds:D2}";
    }

    public static string FormatDetailsAndState(string? DetailsText, string? StateText)
    {
        string TrimmedDetails = Truncate(DetailsText, AppConstants.MaxDetailsLength);
        string TrimmedState = Truncate(StateText, AppConstants.MaxDetailsLength);

        bool HasDetails = !string.IsNullOrEmpty(TrimmedDetails);
        bool HasState = !string.IsNullOrEmpty(TrimmedState);

        if (HasDetails && HasState)
        {
            return Truncate($"{TrimmedDetails} • {TrimmedState}", AppConstants.MaxDetailsLength);
        }

        if (HasDetails)
        {
            return TrimmedDetails;
        }

        return TrimmedState;
    }

    public static string? BuildDiscordState(PresenceRequest Request)
    {
        if (IsHomeGame(Request.GameName))
        {
            return AppConstants.HomeIdleText;
        }

        string Combined = FormatDetailsAndState(Request.Details, Request.State);
        return string.IsNullOrEmpty(Combined) ? null : Combined;
    }

    public static LivePreviewModel BuildLivePreview(PresenceRequest Request, DateTime? SessionStartUtc, DateTime CurrentUtc)
    {
        bool IsHome = IsHomeGame(Request.GameName);
        string ConsoleName = string.IsNullOrWhiteSpace(Request.ConsoleName)
            ? AppConstants.DefaultConsoleName
            : Request.ConsoleName.Trim();

        string GameTitle = IsHome
            ? AppConstants.HomeGameName
            : (string.IsNullOrWhiteSpace(Request.GameName) ? AppConstants.UnknownGameText : Request.GameName.Trim());

        string DetailsLine = IsHome
            ? AppConstants.HomeIdleText
            : Request.Details.Trim();

        string StateLine = Request.State.Trim();

        string ElapsedText = string.Empty;
        if (Request.ShowTimer)
        {
            ElapsedText = (!IsHome && SessionStartUtc.HasValue)
                ? FormatElapsedTime(CurrentUtc - SessionStartUtc.Value)
                : AppConstants.DefaultIdleElapsedText;
        }

        return new LivePreviewModel
        {
            GameTitle = GameTitle,
            DetailsLine = DetailsLine,
            StateLine = StateLine,
            ImageKey = Request.ImageKey,
            SmallBadgeText = ThemeHelper.GetConsoleBadge(ConsoleName),
            IsTimerVisible = Request.ShowTimer,
            ElapsedText = ElapsedText,
            SessionStartUtc = (!IsHome && Request.ShowTimer) ? SessionStartUtc : null
        };
    }
}
