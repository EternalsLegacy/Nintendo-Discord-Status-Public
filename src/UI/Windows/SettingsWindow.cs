using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;
using NintendoDiscordStatus.UI.Helpers;

namespace NintendoDiscordStatus.UI.Windows;

public sealed class SettingsWindow : Window
{
    private enum SettingsSection
    {
        Info,
        General,
        Appearance,
        UpdatesAndSync
    }

    private readonly string AccentHex;
    private readonly Action<Action<AppSettings>> OnSaveSettings;
    private readonly Action OnResetDefaults;
    private readonly Func<Task<bool>> OnSyncLibraryNow;
    private readonly Func<Task<UpdateInfoModel?>> OnCheckUpdatesNow;
    private readonly Action<UpdateInfoModel> OnOpenUpdateOverlay;

    private readonly ModernToggleSwitch AutoUpdateToggle;
    private readonly ModernToggleSwitch AutoSyncToggle;
    private readonly ModernToggleSwitch RememberToggle;
    private readonly ModernToggleSwitch MinimizeTrayToggle;
    private readonly ModernToggleSwitch DynamicThemeToggle;
    private readonly ModernToggleSwitch ShowTimerToggle;

    private readonly Dictionary<SettingsSection, (Border Row, Border Indicator, TextBlock Label)> NavItems =
        new Dictionary<SettingsSection, (Border Row, Border Indicator, TextBlock Label)>();

    private readonly TextBlock SectionHeaderText;
    private readonly ContentControl SectionBodyHost;
    private readonly TextBlock ActionStatusText;

    private readonly Control InfoSectionView;
    private readonly Control GeneralSectionView;
    private readonly Control AppearanceSectionView;
    private readonly Control UpdatesSectionView;

    public SettingsWindow(
        AppSettings CurrentSettings,
        string AccentHex,
        Action<Action<AppSettings>> OnSaveSettings,
        Action OnResetDefaults,
        Func<Task<bool>> OnSyncLibraryNow,
        Func<Task<UpdateInfoModel?>> OnCheckUpdatesNow,
        Action<UpdateInfoModel> OnOpenUpdateOverlay)
    {
        this.AccentHex = AccentHex;
        this.OnSaveSettings = OnSaveSettings;
        this.OnResetDefaults = OnResetDefaults;
        this.OnSyncLibraryNow = OnSyncLibraryNow;
        this.OnCheckUpdatesNow = OnCheckUpdatesNow;
        this.OnOpenUpdateOverlay = OnOpenUpdateOverlay;

        ConfigureWindow();

        AutoUpdateToggle = new ModernToggleSwitch(CurrentSettings.AutoCheckUpdates, AccentHex);
        AutoSyncToggle = new ModernToggleSwitch(CurrentSettings.AutoSyncGameLibrary, AccentHex);
        RememberToggle = new ModernToggleSwitch(CurrentSettings.RememberLastStatus, AccentHex);
        MinimizeTrayToggle = new ModernToggleSwitch(CurrentSettings.MinimizeToTrayOnClose, AccentHex);
        DynamicThemeToggle = new ModernToggleSwitch(CurrentSettings.DynamicConsoleTheme, AccentHex);
        ShowTimerToggle = new ModernToggleSwitch(CurrentSettings.ShowElapsedTimer, AccentHex);

        ActionStatusText = new TextBlock
        {
            Text = $"Version {AppConstants.CurrentVersionString}",
            FontSize = 12,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
            TextWrapping = TextWrapping.Wrap
        };

        SectionHeaderText = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex),
            Margin = new Thickness(0, 0, 0, 22)
        };

        SectionBodyHost = new ContentControl();

        InfoSectionView = BuildInfoSection();
        GeneralSectionView = BuildGeneralSection();
        AppearanceSectionView = BuildAppearanceSection();
        UpdatesSectionView = BuildUpdatesSection();

        Content = BuildMainLayout();
        SelectSection(SettingsSection.General);

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
        Title = "Settings";
        Width = 680;
        Height = 470;
        MinWidth = 600;
        MinHeight = 420;
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

    private Control BuildMainLayout()
    {
        Border Sidebar = new Border
        {
            Background = UiStyleHelper.ParseBrush("#141417"),
            BorderBrush = UiStyleHelper.ParseBrush("#2A2A32"),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Padding = new Thickness(0, 22, 0, 20),
            Child = BuildSidebarStack()
        };

        SectionBodyHost.HorizontalAlignment = HorizontalAlignment.Stretch;

        StackPanel ContentStack = new StackPanel
        {
            Margin = new Thickness(32, 26, 32, 20),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children =
            {
                SectionHeaderText,
                SectionBodyHost
            }
        };

        ScrollViewer ContentScroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = ContentStack
        };

        Grid RightPaneGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*, Auto")
        };
        RightPaneGrid.Children.Add(ContentScroller);
        Grid.SetRow(ContentScroller, 0);

        Control FooterBar = BuildFooterBar();
        RightPaneGrid.Children.Add(FooterBar);
        Grid.SetRow(FooterBar, 1);

        Grid RootGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("190, *")
        };
        RootGrid.Children.Add(Sidebar);
        Grid.SetColumn(Sidebar, 0);
        RootGrid.Children.Add(RightPaneGrid);
        Grid.SetColumn(RightPaneGrid, 1);

        return RootGrid;
    }

    private StackPanel BuildSidebarStack()
    {
        StackPanel NavStack = new StackPanel { Spacing = 4 };
        NavStack.Children.Add(CreateNavItem(SettingsSection.Info, "Info"));
        NavStack.Children.Add(CreateNavItem(SettingsSection.General, "General"));
        NavStack.Children.Add(CreateNavItem(SettingsSection.Appearance, "Appearance"));
        NavStack.Children.Add(CreateNavItem(SettingsSection.UpdatesAndSync, "Updates & Sync"));
        return NavStack;
    }

    private Border CreateNavItem(SettingsSection Section, string TitleText)
    {
        Border Indicator = new Border
        {
            Width = 3,
            Height = 20,
            CornerRadius = new CornerRadius(1.5),
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center
        };

        TextBlock Label = new TextBlock
        {
            Text = TitleText,
            FontSize = 13.5,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0)
        };

        Grid ItemGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *")
        };
        ItemGrid.Children.Add(Indicator);
        Grid.SetColumn(Indicator, 0);
        ItemGrid.Children.Add(Label);
        Grid.SetColumn(Label, 1);

        Border ItemRow = new Border
        {
            Padding = new Thickness(14, 11, 16, 11),
            Margin = new Thickness(8, 0),
            CornerRadius = new CornerRadius(6),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = ItemGrid
        };

        ItemRow.PointerPressed += (Sender, Args) =>
        {
            SelectSection(Section);
            Args.Handled = true;
        };

        NavItems[Section] = (ItemRow, Indicator, Label);
        return ItemRow;
    }

    private void SelectSection(SettingsSection SelectedSection)
    {
        foreach ((SettingsSection Key, (Border Row, Border Indicator, TextBlock Label) Item) in NavItems)
        {
            bool IsSelected = Key == SelectedSection;
            Item.Row.Background = IsSelected
                ? UiStyleHelper.ParseBrush("#222228")
                : Brushes.Transparent;
            Item.Indicator.Background = IsSelected
                ? UiStyleHelper.ParseBrush(AccentHex)
                : Brushes.Transparent;
            Item.Label.Foreground = UiStyleHelper.ParseBrush(
                IsSelected ? ThemeConstants.TextWhiteHex : ThemeConstants.TextGrayHex);
            Item.Label.FontWeight = IsSelected ? FontWeight.SemiBold : FontWeight.Normal;
        }

        switch (SelectedSection)
        {
            case SettingsSection.Info:
                SectionHeaderText.Text = "App Information:";
                SectionBodyHost.Content = InfoSectionView;
                break;

            case SettingsSection.General:
                SectionHeaderText.Text = "General Settings:";
                SectionBodyHost.Content = GeneralSectionView;
                break;

            case SettingsSection.Appearance:
                SectionHeaderText.Text = "Appearance:";
                SectionBodyHost.Content = AppearanceSectionView;
                break;

            case SettingsSection.UpdatesAndSync:
                SectionHeaderText.Text = "Updates & Library Sync:";
                SectionBodyHost.Content = UpdatesSectionView;
                break;
        }
    }

    private Control BuildInfoSection()
    {
        Border InfoCard = new Border
        {
            Background = UiStyleHelper.ParseBrush(ThemeConstants.CardBackgroundHex),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = AppConstants.WindowTitle,
                        FontSize = 15,
                        FontWeight = FontWeight.Bold,
                        Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex)
                    },
                    new TextBlock
                    {
                        Text = $"Version {AppConstants.CurrentVersionString}",
                        FontSize = 13,
                        Foreground = UiStyleHelper.ParseBrush(AccentHex),
                        FontWeight = FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = $"GitHub: {AppConstants.GitHubOwner}/{AppConstants.GitHubRepository}",
                        FontSize = 12.5,
                        Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };

        return InfoCard;
    }

    private Control BuildGeneralSection()
    {
        return new StackPanel
        {
            Spacing = 22,
            Children =
            {
                CreateToggleRow(
                    "Remember last console, game & details",
                    "Restore your previous fields automatically when launching the app",
                    RememberToggle),
                CreateToggleRow(
                    "Minimize to System Tray on close",
                    "Keep Rich Presence active in the background when closing this window",
                    MinimizeTrayToggle)
            }
        };
    }

    private Control BuildAppearanceSection()
    {
        return new StackPanel
        {
            Spacing = 22,
            Children =
            {
                CreateToggleRow(
                    "Dynamic console accent themes",
                    "Adapt buttons, badges, and highlights to the selected console",
                    DynamicThemeToggle),
                CreateToggleRow(
                    "Show elapsed play timer",
                    "Display elapsed session time in Discord and the Live Preview",
                    ShowTimerToggle)
            }
        };
    }

    private Control BuildUpdatesSection()
    {
        Button SyncNowButton = UiStyleHelper.CreateSecondaryButton("Sync Library Now");
        SyncNowButton.Padding = new Thickness(14, 9);
        SyncNowButton.Click += async (Sender, Args) =>
        {
            SyncNowButton.IsEnabled = false;
            ActionStatusText.Text = "Syncing game_lib.json from GitHub...";
            bool Synced = await OnSyncLibraryNow();
            ActionStatusText.Text = Synced
                ? "Game library synced cleanly!"
                : "Could not reach GitHub. Using local library.";
            SyncNowButton.IsEnabled = true;
        };

        Button CheckUpdateButton = UiStyleHelper.CreateSecondaryButton("Check Updates");
        CheckUpdateButton.Padding = new Thickness(14, 9);
        CheckUpdateButton.Click += async (Sender, Args) =>
        {
            CheckUpdateButton.IsEnabled = false;
            ActionStatusText.Text = "Checking GitHub releases...";
            UpdateInfoModel? Info = await OnCheckUpdatesNow();
            CheckUpdateButton.IsEnabled = true;

            if (Info != null && Info.IsUpdateAvailable)
            {
                Close();
                OnOpenUpdateOverlay(Info);
            }
            else if (Info != null)
            {
                ActionStatusText.Text = $"Up to date (v{AppConstants.CurrentVersionString}).";
            }
            else
            {
                ActionStatusText.Text = "Update check failed. Check connection.";
            }
        };

        StackPanel ActionsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 6, 0, 0),
            Children = { SyncNowButton, CheckUpdateButton }
        };

        return new StackPanel
        {
            Spacing = 22,
            Children =
            {
                CreateToggleRow(
                    "Check for GitHub updates on startup",
                    "Automatically check for newer releases when starting the app",
                    AutoUpdateToggle),
                CreateToggleRow(
                    "Auto-sync game library from GitHub",
                    "Fetch updated consoles, titles, and cover keys automatically",
                    AutoSyncToggle),
                ActionsRow,
                ActionStatusText
            }
        };
    }

    private Control BuildFooterBar()
    {
        Button ResetButton = UiStyleHelper.CreateSecondaryButton("Reset Defaults");
        ResetButton.Click += (Sender, Args) =>
        {
            OnResetDefaults();
            AutoUpdateToggle.IsChecked = true;
            AutoSyncToggle.IsChecked = true;
            RememberToggle.IsChecked = true;
            MinimizeTrayToggle.IsChecked = true;
            DynamicThemeToggle.IsChecked = true;
            ShowTimerToggle.IsChecked = true;
            ActionStatusText.Text = "Settings reset to defaults.";
        };

        Button CancelButton = UiStyleHelper.CreateSecondaryButton("Cancel");
        CancelButton.Margin = new Thickness(0, 0, 8, 0);
        CancelButton.Click += (Sender, Args) => Close();

        Button SaveButton = UiStyleHelper.CreatePrimaryButton("Save Settings", AccentHex);
        SaveButton.Click += (Sender, Args) =>
        {
            OnSaveSettings(Config =>
            {
                Config.AutoCheckUpdates = AutoUpdateToggle.IsChecked;
                Config.AutoSyncGameLibrary = AutoSyncToggle.IsChecked;
                Config.RememberLastStatus = RememberToggle.IsChecked;
                Config.MinimizeToTrayOnClose = MinimizeTrayToggle.IsChecked;
                Config.DynamicConsoleTheme = DynamicThemeToggle.IsChecked;
                Config.ShowElapsedTimer = ShowTimerToggle.IsChecked;
            });
            Close();
        };

        Grid FooterGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto, Auto")
        };
        FooterGrid.Children.Add(ResetButton);
        Grid.SetColumn(ResetButton, 0);
        FooterGrid.Children.Add(CancelButton);
        Grid.SetColumn(CancelButton, 2);
        FooterGrid.Children.Add(SaveButton);
        Grid.SetColumn(SaveButton, 3);

        return new Border
        {
            Background = UiStyleHelper.ParseBrush("#19191D"),
            BorderBrush = UiStyleHelper.ParseBrush("#2A2A32"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(28, 14),
            Child = FooterGrid
        };
    }

    private static Control CreateToggleRow(string TitleText, string DescriptionText, ModernToggleSwitch Toggle)
    {
        StackPanel TextStack = new StackPanel
        {
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 16, 0),
            Children =
            {
                new TextBlock
                {
                    Text = TitleText,
                    FontSize = 13.5,
                    FontWeight = FontWeight.Medium,
                    Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex),
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = DescriptionText,
                    FontSize = 11.5,
                    Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        Toggle.HorizontalAlignment = HorizontalAlignment.Right;

        Grid RowGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 36
        };
        RowGrid.Children.Add(TextStack);
        Grid.SetColumn(TextStack, 0);
        RowGrid.Children.Add(Toggle);
        Grid.SetColumn(Toggle, 1);

        return RowGrid;
    }
}
