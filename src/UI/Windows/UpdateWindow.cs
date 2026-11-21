using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;
using NintendoDiscordStatus.Core.Services;
using NintendoDiscordStatus.UI.Helpers;

namespace NintendoDiscordStatus.UI.Windows;

public sealed class UpdateWindow : Window
{
    private readonly UpdateInfoModel UpdateInfo;
    private readonly UpdateService Updates;
    private readonly string AccentHex;

    public UpdateWindow(
        UpdateInfoModel UpdateInfo,
        UpdateService Updates,
        string AccentHex)
    {
        this.UpdateInfo = UpdateInfo;
        this.Updates = Updates;
        this.AccentHex = AccentHex;

        ConfigureWindow();
        Content = BuildLayout();

        KeyDown += (Sender, Args) =>
        {
            if (Args.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    private void ConfigureWindow()
    {
        Title = $"Update Available ({UpdateInfo.TagName})";
        Width = 560;
        Height = 490;
        MinWidth = 480;
        MinHeight = 410;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = UiStyleHelper.ParseBrush("#1C1C20");

        try
        {
            using Stream IconStream = AssetLoader.Open(new Uri("avares://NintendoDiscordStatus/app_icon.ico"));
            Icon = new WindowIcon(IconStream);
        }
        catch
        {
            // Fallback cleanly when running without embedded resource icon.
        }
    }

    private Control BuildLayout()
    {
        Border VersionBadge = new Border
        {
            Background = UiStyleHelper.ParseBrush("#1D382B"),
            BorderBrush = UiStyleHelper.ParseBrush(ThemeConstants.DiscordGreenHex),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = $"v{AppConstants.CurrentVersionString}  →  {UpdateInfo.TagName}",
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                Foreground = UiStyleHelper.ParseBrush(ThemeConstants.DiscordGreenHex)
            }
        };

        Grid HeaderGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto")
        };
        HeaderGrid.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = "Update Available",
                    FontSize = 18,
                    FontWeight = FontWeight.Bold,
                    Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex)
                },
                new TextBlock
                {
                    Text = "A new release of Nintendo Discord Rich Presence is ready to install.",
                    FontSize = 12,
                    Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex)
                }
            }
        });
        HeaderGrid.Children.Add(VersionBadge);
        Grid.SetColumn(VersionBadge, 1);

        Border HeaderContainer = new Border
        {
            Background = UiStyleHelper.ParseBrush("#141417"),
            BorderBrush = UiStyleHelper.ParseBrush("#2A2A32"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(24, 18),
            Child = HeaderGrid
        };

        StackPanel NotesContent = MarkdownRenderer.RenderReleaseNotes(UpdateInfo.ReleaseNotes);
        NotesContent.Margin = new Thickness(18, 14, 22, 16);

        ScrollViewer NotesScroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = NotesContent
        };

        Border NotesCard = new Border
        {
            Background = UiStyleHelper.ParseBrush("#141417"),
            BorderBrush = UiStyleHelper.ParseBrush("#2A2A32"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Child = NotesScroller
        };

        ProgressBar DownloadProgress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = 7,
            IsVisible = false,
            Foreground = UiStyleHelper.ParseBrush(AccentHex)
        };

        TextBlock StatusMessage = new TextBlock
        {
            FontSize = 12,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.WarningOrangeHex),
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };

        Grid BodyGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *, Auto, Auto"),
            Margin = new Thickness(24, 18, 24, 16)
        };

        TextBlock NotesLabel = UiStyleHelper.CreateLabel("RELEASE NOTES", 11.5, true, ThemeConstants.TextGrayHex);
        NotesLabel.Margin = new Thickness(0, 0, 0, 8);
        BodyGrid.Children.Add(NotesLabel);
        Grid.SetRow(NotesLabel, 0);

        BodyGrid.Children.Add(NotesCard);
        Grid.SetRow(NotesCard, 1);

        DownloadProgress.Margin = new Thickness(0, 12, 0, 0);
        BodyGrid.Children.Add(DownloadProgress);
        Grid.SetRow(DownloadProgress, 2);

        StatusMessage.Margin = new Thickness(0, 8, 0, 0);
        BodyGrid.Children.Add(StatusMessage);
        Grid.SetRow(StatusMessage, 3);

        Button BrowserButton = UiStyleHelper.CreateSecondaryButton("Open GitHub");
        BrowserButton.IsVisible = !string.IsNullOrWhiteSpace(UpdateInfo.HtmlUrl);
        BrowserButton.Click += (Sender, Args) => Updates.OpenReleasePage(UpdateInfo);

        Button SkipButton = UiStyleHelper.CreateSecondaryButton("Later");
        SkipButton.Margin = new Thickness(0, 0, 8, 0);
        SkipButton.Click += (Sender, Args) => Close();

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
            ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto, Auto")
        };
        FooterGrid.Children.Add(BrowserButton);
        Grid.SetColumn(BrowserButton, 0);
        FooterGrid.Children.Add(SkipButton);
        Grid.SetColumn(SkipButton, 2);
        FooterGrid.Children.Add(InstallButton);
        Grid.SetColumn(InstallButton, 3);

        Border FooterContainer = new Border
        {
            Background = UiStyleHelper.ParseBrush("#19191D"),
            BorderBrush = UiStyleHelper.ParseBrush("#2A2A32"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 14),
            Child = FooterGrid
        };

        Grid RootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *, Auto")
        };
        RootGrid.Children.Add(HeaderContainer);
        Grid.SetRow(HeaderContainer, 0);
        RootGrid.Children.Add(BodyGrid);
        Grid.SetRow(BodyGrid, 1);
        RootGrid.Children.Add(FooterContainer);
        Grid.SetRow(FooterContainer, 2);

        return RootGrid;
    }
}
