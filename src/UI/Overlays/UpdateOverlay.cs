using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;
using NintendoDiscordStatus.Core.Services;
using NintendoDiscordStatus.UI.Helpers;

namespace NintendoDiscordStatus.UI.Overlays;

public static class UpdateOverlay
{
    public static Control Build(
        UpdateInfoModel UpdateInfo,
        UpdateService Updates,
        string AccentHex,
        Action OnClose)
    {
        TextBlock SubtitleText = new TextBlock
        {
            Text = $"New version {UpdateInfo.TagName} available (current: v{AppConstants.CurrentVersionString}).",
            FontSize = 12.5,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.DiscordGreenHex)
        };

        ScrollViewer NotesScroller = new ScrollViewer
        {
            MaxHeight = 230,
            Background = UiStyleHelper.ParseBrush(ThemeConstants.InputBackgroundHex),
            Padding = new Thickness(12),
            Content = MarkdownRenderer.RenderReleaseNotes(UpdateInfo.ReleaseNotes)
        };

        Border NotesBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Child = NotesScroller
        };

        ProgressBar DownloadProgress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = 8,
            IsVisible = false,
            Foreground = UiStyleHelper.ParseBrush(AccentHex)
        };

        TextBlock StatusMessage = new TextBlock
        {
            FontSize = 11.5,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.WarningOrangeHex),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };

        Button BrowserButton = UiStyleHelper.CreateSecondaryButton("Open GitHub");
        BrowserButton.IsVisible = !string.IsNullOrWhiteSpace(UpdateInfo.HtmlUrl);
        BrowserButton.Click += (Sender, Args) => Updates.OpenReleasePage(UpdateInfo);

        Button SkipButton = UiStyleHelper.CreateSecondaryButton("Later");
        SkipButton.Margin = new Thickness(0, 0, 8, 0);
        SkipButton.Click += (Sender, Args) => OnClose();

        Button InstallButton = UiStyleHelper.CreatePrimaryButton("Update & Restart", AccentHex);
        InstallButton.Click += async (Sender, Args) =>
        {
            InstallButton.IsEnabled = false;
            SkipButton.IsEnabled = false;
            DownloadProgress.IsVisible = true;
            StatusMessage.IsVisible = true;
            StatusMessage.Text = "Downloading update...";

            Progress<double> ProgressReporter = new Progress<double>(Fraction =>
            {
                DownloadProgress.Value = Math.Clamp(Fraction * 100.0, 0, 100);
            });

            UpdateExecutionResult Result = await Updates.DownloadAndInstallAsync(UpdateInfo, ProgressReporter);

            if (Result.Status != UpdateExecutionStatus.SuccessLaunchedUpdater)
            {
                StatusMessage.Text = string.IsNullOrWhiteSpace(Result.ErrorMessage)
                    ? "Update installation could not complete."
                    : Result.ErrorMessage;
                InstallButton.IsEnabled = true;
                SkipButton.IsEnabled = true;
            }
        };

        Grid FooterGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto, Auto"),
            Margin = new Thickness(0, 4, 0, 0)
        };
        FooterGrid.Children.Add(BrowserButton);
        Grid.SetColumn(BrowserButton, 0);
        FooterGrid.Children.Add(SkipButton);
        Grid.SetColumn(SkipButton, 2);
        FooterGrid.Children.Add(InstallButton);
        Grid.SetColumn(InstallButton, 3);

        StackPanel CardContent = new StackPanel { Spacing = 10 };
        CardContent.Children.Add(UiStyleHelper.CreateLabel("UPDATE AVAILABLE", 14, true, ThemeConstants.TextWhiteHex));
        CardContent.Children.Add(SubtitleText);
        CardContent.Children.Add(NotesBorder);
        CardContent.Children.Add(DownloadProgress);
        CardContent.Children.Add(StatusMessage);
        CardContent.Children.Add(FooterGrid);

        return UiStyleHelper.WrapModalCard(CardContent, 440);
    }
}
