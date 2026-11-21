using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Helpers;
using NintendoDiscordStatus.Core.Models;
using NintendoDiscordStatus.Core.Presenters;
using NintendoDiscordStatus.Core.Services;
using NintendoDiscordStatus.UI.Helpers;
using NintendoDiscordStatus.UI.Overlays;
using NintendoDiscordStatus.UI.Windows;

namespace NintendoDiscordStatus;

public class MainWindow : Window
{
    private readonly MainPresenter StatusPresenter;
    private readonly SettingsService Settings;
    private readonly LibraryService Library;
    private readonly PresetService Presets;
    private readonly DiscordService Discord;
    private readonly UpdateService Updates;
    private readonly DispatcherTimer LivePreviewTimer;
    private readonly Dictionary<string, Bitmap> UiBitmapCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

    private int SyncGuardDepth;
    private bool IsForceClosing;
    private string CurrentAccentHex = ThemeConstants.DefaultAccentHex;
    private string ActivePreviewImageKey = string.Empty;
    private UpdateInfoModel? PendingUpdateInfo;

    private Grid RootOverlayHost = null!;
    private Ellipse StatusDot = null!;
    private TextBlock ConnectionStatusLabel = null!;
    private Button UpdateButton = null!;
    private Button StopButton = null!;

    private Image PreviewGameImage = null!;
    private Border PreviewBadgeBorder = null!;
    private TextBlock PreviewBadgeText = null!;
    private TextBlock PreviewGameTitle = null!;
    private TextBlock PreviewDetailsLine = null!;
    private TextBlock PreviewStateLine = null!;
    private TextBlock PreviewElapsedLine = null!;

    private StackPanel RecentGamesSection = null!;
    private StackPanel RecentGamesRow = null!;

    private AutoCompleteBox ConsoleCombo = null!;
    private AutoCompleteBox GameCombo = null!;
    private TextBox DetailsBox = null!;
    private TextBox StateBox = null!;
    private NumericUpDown TimerOffsetInput = null!;

    private Button FooterSettingsButton = null!;
    private Button FooterUpdateBadgeButton = null!;
    private Button FooterSavePresetButton = null!;
    private Avalonia.Controls.Shapes.Path SaveCurrentIconPath = null!;
    private SettingsWindow? ActiveSettingsWindow;
    private Flyout? ActiveSelectorFlyout;

    public MainWindow(
        MainPresenter Presenter,
        SettingsService Settings,
        LibraryService Library,
        PresetService Presets,
        DiscordService Discord,
        UpdateService Updates)
    {
        this.StatusPresenter = Presenter;
        this.Settings = Settings;
        this.Library = Library;
        this.Presets = Presets;
        this.Discord = Discord;
        this.Updates = Updates;

        LivePreviewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        LivePreviewTimer.Tick += (Sender, Args) => RefreshLivePreviewOnly();

        ConfigureWindowMetadata();
        BuildUserInterface();
        WirePresenterEvents();

        Opened += async (Sender, Args) => await StatusPresenter.InitializeAsync();
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
    }

    public event EventHandler? RecentGamesUpdatedForTray;

    public IReadOnlyList<RecentGameModel> GetRecentGamesSnapshot()
    {
        return Settings.Current.RecentGames.ToList();
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void QuickSwitchFromTray(string ConsoleName, string GameName)
    {
        StatusPresenter.QuickSwitchGame(ConsoleName, GameName);
    }

    public void StopPresenceFromTray()
    {
        StatusPresenter.StopPresence();
        UpdateConnectionBadge(StatusPresenter.ConnectionState);
    }

    public void ForceExit()
    {
        IsForceClosing = true;
        Close();
    }

    private void ConfigureWindowMetadata()
    {
        Title = AppConstants.WindowTitle;
        Width = 450;
        Height = 760;
        MinWidth = 420;
        MinHeight = 640;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = UiStyleHelper.ParseBrush(ThemeConstants.WindowBackgroundHex);

        try
        {
            using Stream IconStream = AssetLoader.Open(new Uri("avares://NintendoDiscordStatus/app_icon.ico"));
            Icon = new WindowIcon(IconStream);
        }
        catch
        {
            // Fallback gracefully if running without embedded resource icon.
        }
    }

    private void BuildUserInterface()
    {
        StackPanel MainStack = new StackPanel
        {
            Spacing = 14,
            Margin = new Thickness(20)
        };

        MainStack.Children.Add(BuildTopStatusCard());
        MainStack.Children.Add(BuildRecentGamesSection());
        MainStack.Children.Add(BuildInputFormSection());
        MainStack.Children.Add(BuildLivePreviewCard());
        MainStack.Children.Add(BuildFooterSection());

        ScrollViewer MainScroller = new ScrollViewer
        {
            Content = MainStack
        };

        RootOverlayHost = new Grid();
        RootOverlayHost.Children.Add(MainScroller);

        Content = RootOverlayHost;
        LivePreviewTimer.Start();
    }

    private Control BuildTopStatusCard()
    {
        StatusDot = new Ellipse
        {
            Width = 12,
            Height = 12,
            Fill = UiStyleHelper.ParseBrush(ThemeConstants.InactiveDotHex),
            VerticalAlignment = VerticalAlignment.Center
        };

        ConnectionStatusLabel = new TextBlock
        {
            Text = "RPC is Inactive",
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };

        StopButton = new Button
        {
            Content = "Stop",
            Background = UiStyleHelper.ParseBrush(ThemeConstants.InputBackgroundHex),
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
            FontWeight = FontWeight.Bold,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 8),
            IsEnabled = false
        };
        StopButton.Click += (Sender, Args) =>
        {
            StatusPresenter.StopPresence();
            UpdateConnectionBadge(StatusPresenter.ConnectionState);
        };

        UpdateButton = new Button
        {
            Content = "Update",
            Background = UiStyleHelper.ParseBrush(CurrentAccentHex),
            Foreground = Brushes.White,
            FontWeight = FontWeight.Bold,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 8)
        };
        UpdateButton.Click += (Sender, Args) =>
        {
            if (StatusPresenter.ConnectionState == DiscordConnectionState.Disconnected
                || StatusPresenter.ConnectionState == DiscordConnectionState.ConnectionFailed)
            {
                Discord.Connect();
            }

            StatusPresenter.ApplyStatus(BuildRequestFromUi(), RecordAsRecent: true);
            UpdateConnectionBadge(StatusPresenter.ConnectionState);
        };

        StackPanel ActionStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { StopButton, UpdateButton }
        };

        Grid TopGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto")
        };
        TopGrid.Children.Add(StatusDot);
        Grid.SetColumn(StatusDot, 0);
        TopGrid.Children.Add(ConnectionStatusLabel);
        Grid.SetColumn(ConnectionStatusLabel, 1);
        TopGrid.Children.Add(ActionStack);
        Grid.SetColumn(ActionStack, 2);

        return new Border
        {
            Background = UiStyleHelper.ParseBrush(ThemeConstants.CardBackgroundHex),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15),
            Child = TopGrid
        };
    }

    private Control BuildRecentGamesSection()
    {
        RecentGamesRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6
        };

        RecentGamesSection = new StackPanel
        {
            Spacing = 6,
            IsVisible = false,
            Children =
            {
                CreateSectionLabel("RECENT"),
                RecentGamesRow
            }
        };

        return RecentGamesSection;
    }

    private Control BuildInputFormSection()
    {
        ConsoleCombo = new AutoCompleteBox
        {
            Watermark = AppConstants.DefaultConsoleName,
            MinimumPrefixLength = 0,
            MaxDropDownHeight = 260,
            FilterMode = AutoCompleteFilterMode.Custom,
            TextFilter = MatchesAutoCompleteFilter,
            Background = UiStyleHelper.ParseBrush(ThemeConstants.InputBackgroundHex),
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex),
            CornerRadius = new CornerRadius(6)
        };
        ConsoleCombo.TextChanged += OnConsoleTextChanged;
        ConsoleCombo.SelectionChanged += OnConsoleSelectionChanged;

        Button ConsoleDropButton = CreateDropdownArrowButton(
            ConsoleCombo,
            () => Library.GetConsoleNames(),
            SelectedConsole =>
            {
                RunWithoutUiEvents(() => ConsoleCombo.Text = SelectedConsole);
                HandleConsoleChanged();
            });

        Grid ConsoleRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto")
        };
        ConsoleRow.Children.Add(ConsoleCombo);
        Grid.SetColumn(ConsoleCombo, 0);
        ConsoleRow.Children.Add(ConsoleDropButton);
        Grid.SetColumn(ConsoleDropButton, 1);

        GameCombo = new AutoCompleteBox
        {
            Watermark = AppConstants.HomeGameName,
            MinimumPrefixLength = 0,
            MaxDropDownHeight = 260,
            FilterMode = AutoCompleteFilterMode.Custom,
            TextFilter = MatchesAutoCompleteFilter,
            Background = UiStyleHelper.ParseBrush(ThemeConstants.InputBackgroundHex),
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex),
            CornerRadius = new CornerRadius(6)
        };
        GameCombo.TextChanged += OnGameTextChanged;
        GameCombo.SelectionChanged += OnGameSelectionChanged;

        Button GameDropButton = CreateDropdownArrowButton(
            GameCombo,
            () => Library.GetGamesForConsole(ConsoleCombo.Text).Select(Game => Game.GameName).ToList(),
            SelectedGame =>
            {
                RunWithoutUiEvents(() => GameCombo.Text = SelectedGame);
                HandleGameChanged();
            });
        Button AddCustomGameButton = UiStyleHelper.CreateSecondaryButton("+ Add");
        AddCustomGameButton.Margin = new Thickness(6, 0, 0, 0);
        AddCustomGameButton.Click += (Sender, Args) => OpenAddGameOverlay();

        Grid GameRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto, Auto")
        };
        GameRow.Children.Add(GameCombo);
        Grid.SetColumn(GameCombo, 0);
        GameRow.Children.Add(GameDropButton);
        Grid.SetColumn(GameDropButton, 1);
        GameRow.Children.Add(AddCustomGameButton);
        Grid.SetColumn(AddCustomGameButton, 2);

        DetailsBox = UiStyleHelper.CreateStyledTextBox(AppConstants.HomeIdleText);
        DetailsBox.Padding = new Thickness(12, 10);
        DetailsBox.TextChanged += OnDetailsOrStateChanged;

        StateBox = UiStyleHelper.CreateStyledTextBox(AppConstants.DefaultStateExample);
        StateBox.Padding = new Thickness(12, 10);
        StateBox.TextChanged += OnDetailsOrStateChanged;

        TimerOffsetInput = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 1440,
            Value = 0,
            Increment = 1,
            FormatString = "0",
            Background = UiStyleHelper.ParseBrush(ThemeConstants.InputBackgroundHex),
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex),
            CornerRadius = new CornerRadius(6)
        };
        TimerOffsetInput.ValueChanged += OnTimerOffsetChanged;

        Button ResetTimerButton = UiStyleHelper.CreateSecondaryButton("Reset Timer");
        ResetTimerButton.Margin = new Thickness(8, 0, 0, 0);
        ResetTimerButton.Click += (Sender, Args) =>
        {
            StatusPresenter.UpdatePreviewDraft(BuildRequestFromUi());
            StatusPresenter.ResetTimer();
        };

        Grid TimerRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*, Auto")
        };
        TimerRow.Children.Add(TimerOffsetInput);
        Grid.SetColumn(TimerOffsetInput, 0);
        TimerRow.Children.Add(ResetTimerButton);
        Grid.SetColumn(ResetTimerButton, 1);

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                CreateSectionLabel("CONSOLE"),
                ConsoleRow,
                CreateSectionLabel("GAME NAME"),
                GameRow,
                CreateSectionLabel("DETAILS (TOP LINE)"),
                DetailsBox,
                CreateSectionLabel("STATE (BOTTOM LINE)"),
                StateBox,
                CreateSectionLabel("STARTED X MINUTES AGO"),
                TimerRow
            }
        };
    }

    private Control BuildLivePreviewCard()
    {
        PreviewGameImage = new Image
        {
            Width = 70,
            Height = 70,
            Stretch = Stretch.UniformToFill
        };

        Border ImageContainer = new Border
        {
            Width = 70,
            Height = 70,
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Background = UiStyleHelper.ParseBrush(ThemeConstants.InputBackgroundHex),
            Child = PreviewGameImage
        };

        PreviewBadgeText = new TextBlock
        {
            Text = ThemeConstants.BadgeSwitch,
            FontSize = 9,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        PreviewBadgeBorder = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = UiStyleHelper.ParseBrush(CurrentAccentHex),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -4, -4),
            Child = PreviewBadgeText
        };

        Panel ArtworkPanel = new Panel
        {
            Width = 70,
            Height = 70,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { ImageContainer, PreviewBadgeBorder }
        };

        PreviewGameTitle = new TextBlock
        {
            Text = AppConstants.HomeGameName,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        PreviewDetailsLine = new TextBlock
        {
            Text = AppConstants.HomeIdleText,
            FontSize = 13,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        PreviewStateLine = new TextBlock
        {
            Text = string.Empty,
            FontSize = 13,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsVisible = false
        };

        PreviewElapsedLine = new TextBlock
        {
            Text = AppConstants.DefaultIdleElapsedText,
            FontSize = 13,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex)
        };

        StackPanel TextColumn = new StackPanel
        {
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            Children = { PreviewGameTitle, PreviewDetailsLine, PreviewStateLine, PreviewElapsedLine }
        };

        Grid PreviewGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("78, *"),
            Margin = new Thickness(0, 6, 0, 0)
        };
        PreviewGrid.Children.Add(ArtworkPanel);
        Grid.SetColumn(ArtworkPanel, 0);
        PreviewGrid.Children.Add(TextColumn);
        Grid.SetColumn(TextColumn, 1);

        StackPanel PreviewStack = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = "LIVE PREVIEW",
                    FontSize = 12,
                    FontWeight = FontWeight.Bold,
                    Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex)
                },
                new TextBlock
                {
                    Text = "PLAYING A GAME",
                    FontSize = 12,
                    FontWeight = FontWeight.Bold,
                    Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex)
                },
                PreviewGrid
            }
        };

        return new Border
        {
            Background = UiStyleHelper.ParseBrush(ThemeConstants.CardBackgroundHex),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18),
            Child = PreviewStack
        };
    }

    private Control BuildFooterSection()
    {
        Button PresetsFooterButton = CreateFooterIconButton(
            UiStyleHelper.CreateVectorIcon(UiStyleHelper.PresetsIconData, ThemeConstants.TextGrayHex, 18),
            "Presets");
        PresetsFooterButton.Click += (Sender, Args) => OpenPresetsOverlay();

        FooterSettingsButton = CreateFooterIconButton(
            UiStyleHelper.CreateVectorIcon(UiStyleHelper.SettingsIconData, ThemeConstants.TextGrayHex, 18),
            "Settings");
        FooterSettingsButton.HorizontalAlignment = HorizontalAlignment.Center;
        FooterSettingsButton.Click += async (Sender, Args) => await OpenSettingsWindowAsync();

        FooterUpdateBadgeButton = CreateFooterIconButton(
            UiStyleHelper.CreateVectorIcon(UiStyleHelper.UpdateIconData, ThemeConstants.DiscordGreenHex, 18),
            "Update available! Click to install");
        FooterUpdateBadgeButton.IsVisible = false;
        FooterUpdateBadgeButton.Click += (Sender, Args) =>
        {
            if (PendingUpdateInfo != null)
            {
                ShowOverlay(UpdateOverlay.Build(PendingUpdateInfo, Updates, CurrentAccentHex, CloseTopOverlay));
            }
        };

        StackPanel CenterPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { FooterSettingsButton, FooterUpdateBadgeButton }
        };

        SaveCurrentIconPath = UiStyleHelper.CreateVectorIcon(UiStyleHelper.SaveIconData, CurrentAccentHex, 18);
        FooterSavePresetButton = CreateFooterIconButton(SaveCurrentIconPath, "Save Current as Preset");
        FooterSavePresetButton.Click += (Sender, Args) => OpenPresetsOverlay();

        Grid FooterGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto"),
            Margin = new Thickness(0, 4, 0, 0)
        };
        FooterGrid.Children.Add(PresetsFooterButton);
        Grid.SetColumn(PresetsFooterButton, 0);
        FooterGrid.Children.Add(CenterPanel);
        Grid.SetColumn(CenterPanel, 1);
        FooterGrid.Children.Add(FooterSavePresetButton);
        Grid.SetColumn(FooterSavePresetButton, 2);

        return FooterGrid;
    }

    private static Button CreateFooterIconButton(Avalonia.Controls.Shapes.Path IconShape, string TooltipText)
    {
        Button IconButton = new Button
        {
            Content = IconShape,
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(8),
            Width = 38,
            Height = 36,
            Padding = new Thickness(8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        ToolTip.SetTip(IconButton, TooltipText);
        return IconButton;
    }

    private static bool MatchesAutoCompleteFilter(string? SearchText, string? ItemText)
    {
        return string.IsNullOrWhiteSpace(SearchText)
            || (ItemText?.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static TextBlock CreateSectionLabel(string LabelText)
    {
        return new TextBlock
        {
            Text = LabelText,
            FontSize = 12,
            FontWeight = FontWeight.Bold,
            Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
            Margin = new Thickness(0, 2, 0, -6)
        };
    }

    private Button CreateDropdownArrowButton(
        AutoCompleteBox TargetBox,
        Func<IReadOnlyList<string>> GetAllItems,
        Action<string> OnItemSelected)
    {
        Button DropButton = UiStyleHelper.CreateSecondaryButton("▾");
        DropButton.Margin = new Thickness(6, 0, 0, 0);
        DropButton.Padding = new Thickness(11, 7);
        DropButton.Click += (Sender, Args) => ShowFullSelectorFlyout(TargetBox, GetAllItems(), OnItemSelected);
        return DropButton;
    }

    private void ShowFullSelectorFlyout(
        AutoCompleteBox AnchorBox,
        IReadOnlyList<string> Items,
        Action<string> OnItemSelected)
    {
        AnchorBox.IsDropDownOpen = false;

        if (ActiveSelectorFlyout != null)
        {
            ActiveSelectorFlyout.Hide();
            ActiveSelectorFlyout = null;
            return;
        }

        double PopupWidth = AnchorBox.Bounds.Width > 120 ? AnchorBox.Bounds.Width : 340;
        string CurrentText = AnchorBox.Text?.Trim() ?? string.Empty;

        StackPanel ItemsPanel = new StackPanel
        {
            Spacing = 2,
            Width = PopupWidth
        };

        Flyout SelectorFlyout = new Flyout
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft
        };
        SelectorFlyout.Closed += (Sender, Args) =>
        {
            if (ReferenceEquals(ActiveSelectorFlyout, SelectorFlyout))
            {
                ActiveSelectorFlyout = null;
            }
        };

        if (Items.Count == 0)
        {
            ItemsPanel.Children.Add(new TextBlock
            {
                Text = "No entries available for the selected console",
                FontSize = 12,
                Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex),
                Padding = new Thickness(10, 8)
            });
        }
        else
        {
            foreach (string ItemName in Items)
            {
                bool IsSelected = string.Equals(ItemName, CurrentText, StringComparison.OrdinalIgnoreCase);

                Button OptionButton = new Button
                {
                    Content = new TextBlock
                    {
                        Text = ItemName,
                        FontSize = 12.5,
                        FontWeight = IsSelected ? FontWeight.SemiBold : FontWeight.Normal,
                        Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    },
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Background = IsSelected
                        ? UiStyleHelper.ParseBrush("#33343B")
                        : Brushes.Transparent,
                    BorderBrush = IsSelected
                        ? UiStyleHelper.ParseBrush(CurrentAccentHex)
                        : Brushes.Transparent,
                    BorderThickness = IsSelected ? new Thickness(2, 0, 0, 0) : new Thickness(0),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(10, 7),
                    Cursor = new Cursor(StandardCursorType.Hand)
                };

                OptionButton.Click += (Sender, Args) =>
                {
                    SelectorFlyout.Hide();
                    OnItemSelected(ItemName);
                };

                ItemsPanel.Children.Add(OptionButton);
            }
        }

        ScrollViewer ListScroller = new ScrollViewer
        {
            MaxHeight = 260,
            Content = ItemsPanel
        };

        Border PopupContainer = new Border
        {
            Background = UiStyleHelper.ParseBrush(ThemeConstants.CardBackgroundHex),
            BorderBrush = UiStyleHelper.ParseBrush("#3A3A44"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4),
            Child = ListScroller
        };

        SelectorFlyout.Content = PopupContainer;
        ActiveSelectorFlyout = SelectorFlyout;
        SelectorFlyout.ShowAt(AnchorBox);
    }

    private void WirePresenterEvents()
    {
        StatusPresenter.StateRestored += (Sender, Request) => Dispatcher.UIThread.Post(() => ApplyRequestToUi(Request));
        StatusPresenter.PreviewUpdated += (Sender, Preview) => Dispatcher.UIThread.Post(() => RenderLivePreview(Preview));
        StatusPresenter.ThemeUpdated += (Sender, AccentHex) => Dispatcher.UIThread.Post(() => ApplyAccentTheme(AccentHex));
        StatusPresenter.PreviewImageReady += (Sender, Bytes) => Dispatcher.UIThread.Post(() => RenderPreviewImage(Bytes));
        StatusPresenter.ConnectionStateChanged += (Sender, State) => Dispatcher.UIThread.Post(() => UpdateConnectionBadge(State));
        StatusPresenter.DiscordError += (Sender, Error) => Dispatcher.UIThread.Post(() => UpdateConnectionBadge(StatusPresenter.ConnectionState));
        StatusPresenter.LibraryUpdated += (Sender, Args) => Dispatcher.UIThread.Post(RefreshConsoleAndGameLists);
        StatusPresenter.RecentGamesUpdated += (Sender, Args) => Dispatcher.UIThread.Post(RefreshRecentGameChips);
        StatusPresenter.UpdateAvailable += (Sender, UpdateInfo) => Dispatcher.UIThread.Post(() => HandleUpdateAvailable(UpdateInfo));
    }

    private void RunWithoutUiEvents(Action ActionBlock)
    {
        SyncGuardDepth++;
        try
        {
            ActionBlock();
        }
        finally
        {
            SyncGuardDepth--;
        }
    }

    private void ApplyRequestToUi(PresenceRequest Request)
    {
        RunWithoutUiEvents(() =>
        {
            RefreshConsoleAndGameLists();

            ConsoleCombo.Text = Request.ConsoleName;
            RefreshGamesSourceForConsole(Request.ConsoleName);
            GameCombo.Text = Request.GameName;
            DetailsBox.Text = Request.Details;
            StateBox.Text = Request.State;
            TimerOffsetInput.Value = Request.TimerOffsetMinutes;

            ApplyHomeFieldLockState(Request.GameName);
            RefreshRecentGameChips();
            UpdateConnectionBadge(StatusPresenter.ConnectionState);
        });
    }

    private PresenceRequest BuildRequestFromUi()
    {
        return new PresenceRequest
        {
            ConsoleName = ConsoleCombo.Text?.Trim() ?? string.Empty,
            GameName = GameCombo.Text?.Trim() ?? string.Empty,
            Details = DetailsBox.Text?.Trim() ?? string.Empty,
            State = StateBox.Text?.Trim() ?? string.Empty,
            TimerOffsetMinutes = (int)(TimerOffsetInput.Value ?? 0),
            ShowTimer = Settings.Current.ShowElapsedTimer
        };
    }

    private void RefreshConsoleAndGameLists()
    {
        RunWithoutUiEvents(() =>
        {
            string SavedConsole = ConsoleCombo.Text ?? string.Empty;
            string SavedGame = GameCombo.Text ?? string.Empty;

            ConsoleCombo.ItemsSource = Library.GetConsoleNames();
            ConsoleCombo.Text = SavedConsole;

            RefreshGamesSourceForConsole(SavedConsole);
            GameCombo.Text = SavedGame;
        });
    }

    private void RefreshGamesSourceForConsole(string? ConsoleName)
    {
        RunWithoutUiEvents(() =>
        {
            string SavedGame = GameCombo.Text ?? string.Empty;
            List<string> GameTitles = Library.GetGamesForConsole(ConsoleName)
                .Select(Game => Game.GameName)
                .ToList();

            GameCombo.ItemsSource = GameTitles;
            GameCombo.Text = SavedGame;
        });
    }

    private void RefreshRecentGameChips()
    {
        RecentGamesRow.Children.Clear();
        IReadOnlyList<RecentGameModel> Recent = Settings.Current.RecentGames;

        RecentGamesSection.IsVisible = Recent.Count > 0;
        foreach (RecentGameModel Item in Recent)
        {
            Button Chip = UiStyleHelper.CreateSecondaryButton(Item.GameName);
            Chip.Padding = new Thickness(10, 5);
            Chip.FontSize = 11;
            ToolTip.SetTip(Chip, Item.ConsoleName);
            Chip.Click += (Sender, Args) => StatusPresenter.QuickSwitchGame(Item.ConsoleName, Item.GameName);
            RecentGamesRow.Children.Add(Chip);
        }

        RecentGamesUpdatedForTray?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyHomeFieldLockState(string? GameName)
    {
        if (PresenceFormatter.IsHomeGame(GameName))
        {
            DetailsBox.Text = AppConstants.HomeIdleText;
            DetailsBox.IsReadOnly = true;
            DetailsBox.Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex);
        }
        else
        {
            if (DetailsBox.Text == AppConstants.HomeIdleText)
            {
                DetailsBox.Text = string.Empty;
            }

            DetailsBox.IsReadOnly = false;
            DetailsBox.Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex);
        }
    }

    private void OnConsoleSelectionChanged(object? Sender, SelectionChangedEventArgs Args)
    {
        if (SyncGuardDepth > 0 || Args.AddedItems.Count == 0)
        {
            return;
        }

        HandleConsoleChanged();
    }

    private void OnConsoleTextChanged(object? Sender, EventArgs Args)
    {
        if (SyncGuardDepth > 0)
        {
            return;
        }

        HandleConsoleChanged();
    }

    private void HandleConsoleChanged()
    {
        RefreshGamesSourceForConsole(ConsoleCombo.Text);
        StatusPresenter.UpdatePreviewDraft(BuildRequestFromUi());
    }

    private void OnGameSelectionChanged(object? Sender, SelectionChangedEventArgs Args)
    {
        if (SyncGuardDepth > 0 || Args.AddedItems.Count == 0)
        {
            return;
        }

        HandleGameChanged();
    }

    private void OnGameTextChanged(object? Sender, EventArgs Args)
    {
        if (SyncGuardDepth > 0)
        {
            return;
        }

        HandleGameChanged();
    }

    private void HandleGameChanged()
    {
        RunWithoutUiEvents(() => ApplyHomeFieldLockState(GameCombo.Text));
        StatusPresenter.UpdatePreviewDraft(BuildRequestFromUi());
    }

    private void OnDetailsOrStateChanged(object? Sender, TextChangedEventArgs Args)
    {
        if (SyncGuardDepth > 0)
        {
            return;
        }

        StatusPresenter.UpdatePreviewDraft(BuildRequestFromUi());
    }

    private void OnTimerOffsetChanged(object? Sender, NumericUpDownValueChangedEventArgs Args)
    {
        if (SyncGuardDepth > 0)
        {
            return;
        }

        StatusPresenter.UpdatePreviewDraft(BuildRequestFromUi());
    }

    private void RefreshLivePreviewOnly()
    {
        RenderLivePreview(StatusPresenter.GetCurrentLivePreview());
    }

    private void RenderLivePreview(LivePreviewModel Preview)
    {
        ActivePreviewImageKey = Preview.ImageKey;
        PreviewGameTitle.Text = Preview.GameTitle;

        PreviewDetailsLine.Text = Preview.DetailsLine;
        PreviewDetailsLine.IsVisible = !string.IsNullOrWhiteSpace(Preview.DetailsLine);

        PreviewStateLine.Text = Preview.StateLine;
        PreviewStateLine.IsVisible = !string.IsNullOrWhiteSpace(Preview.StateLine);

        PreviewBadgeText.Text = Preview.SmallBadgeText;
        PreviewElapsedLine.Text = Preview.ElapsedText;
        PreviewElapsedLine.IsVisible = Preview.IsTimerVisible;
    }

    private void RenderPreviewImage(byte[]? ImageBytes)
    {
        if (ImageBytes == null || ImageBytes.Length == 0)
        {
            PreviewGameImage.Source = null;
            return;
        }

        string CacheKey = PresenceFormatter.NormalizeAssetFileName(ActivePreviewImageKey);
        if (!string.IsNullOrEmpty(CacheKey) && UiBitmapCache.TryGetValue(CacheKey, out Bitmap? CachedBitmap))
        {
            PreviewGameImage.Source = CachedBitmap;
            return;
        }

        try
        {
            using MemoryStream Stream = new MemoryStream(ImageBytes);
            Bitmap Decoded = new Bitmap(Stream);
            if (!string.IsNullOrEmpty(CacheKey))
            {
                UiBitmapCache[CacheKey] = Decoded;
            }

            PreviewGameImage.Source = Decoded;
        }
        catch
        {
            PreviewGameImage.Source = null;
        }
    }

    private void ApplyAccentTheme(string AccentHex)
    {
        CurrentAccentHex = AccentHex;
        IBrush AccentBrush = UiStyleHelper.ParseBrush(AccentHex);

        if (UpdateButton != null)
        {
            UpdateButton.Background = AccentBrush;
        }

        if (PreviewBadgeBorder != null)
        {
            PreviewBadgeBorder.Background = AccentBrush;
        }

        if (SaveCurrentIconPath != null)
        {
            SaveCurrentIconPath.Fill = AccentBrush;
        }
    }

    private void UpdateConnectionBadge(DiscordConnectionState State)
    {
        if (StatusPresenter.IsPresenceActive)
        {
            StatusDot.Fill = UiStyleHelper.ParseBrush(ThemeConstants.DiscordGreenHex);
            ConnectionStatusLabel.Text = "RPC is Active";
            ConnectionStatusLabel.Foreground = UiStyleHelper.ParseBrush(ThemeConstants.DiscordGreenHex);
            StopButton.IsEnabled = true;
            StopButton.Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex);
            return;
        }

        StopButton.IsEnabled = false;
        StopButton.Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextGrayHex);

        if (State == DiscordConnectionState.ConnectionFailed)
        {
            StatusDot.Fill = UiStyleHelper.ParseBrush(ThemeConstants.WarningOrangeHex);
            ConnectionStatusLabel.Text = "Discord Not Running";
            ConnectionStatusLabel.Foreground = UiStyleHelper.ParseBrush(ThemeConstants.WarningOrangeHex);
            return;
        }

        StatusDot.Fill = UiStyleHelper.ParseBrush(ThemeConstants.InactiveDotHex);
        ConnectionStatusLabel.Text = "RPC is Inactive";
        ConnectionStatusLabel.Foreground = UiStyleHelper.ParseBrush(ThemeConstants.TextWhiteHex);
    }

    private void HandleUpdateAvailable(UpdateInfoModel UpdateInfo)
    {
        PendingUpdateInfo = UpdateInfo;
        FooterUpdateBadgeButton.IsVisible = true;
        ShowOverlay(UpdateOverlay.Build(UpdateInfo, Updates, CurrentAccentHex, CloseTopOverlay));
    }

    private async Task OpenSettingsWindowAsync()
    {
        if (ActiveSettingsWindow != null)
        {
            ActiveSettingsWindow.Activate();
            return;
        }

        SettingsWindow Dialog = new SettingsWindow(
            Settings.Current,
            CurrentAccentHex,
            StatusPresenter.UpdateSettings,
            Settings.ResetToDefaults,
            () => StatusPresenter.SyncLibraryAsync(),
            () => StatusPresenter.CheckForUpdatesAsync(),
            UpdateInfo =>
            {
                PendingUpdateInfo = UpdateInfo;
                FooterUpdateBadgeButton.IsVisible = true;
                ShowOverlay(UpdateOverlay.Build(UpdateInfo, Updates, CurrentAccentHex, CloseTopOverlay));
            });

        ActiveSettingsWindow = Dialog;
        try
        {
            await Dialog.ShowDialog(this);
        }
        finally
        {
            ActiveSettingsWindow = null;
        }
    }

    private void OpenPresetsOverlay()
    {
        string ConsoleName = ConsoleCombo.Text?.Trim() ?? string.Empty;
        string GameName = GameCombo.Text?.Trim() ?? string.Empty;
        string Suggested = $"{ConsoleName} - {GameName}".Trim(' ', '-');

        ShowOverlay(PresetOverlay.Build(
            Presets.Presets,
            Suggested,
            CurrentAccentHex,
            Preset => StatusPresenter.ApplyPreset(Preset),
            PresetName =>
            {
                StatusPresenter.UpdatePreviewDraft(BuildRequestFromUi());
                return StatusPresenter.SaveCurrentAsPreset(PresetName);
            },
            PresetName => Presets.DeletePreset(PresetName),
            CloseTopOverlay));
    }

    private void OpenAddGameOverlay()
    {
        ShowOverlay(AddGameOverlay.Build(
            this,
            Library.GetConsoleNames(),
            ConsoleCombo.Text ?? AppConstants.DefaultConsoleName,
            CurrentAccentHex,
            (ConsoleName, GameName, ImageKey) => StatusPresenter.AddCustomGame(ConsoleName, GameName, ImageKey),
            (ConsoleName, GameName) =>
            {
                RunWithoutUiEvents(() =>
                {
                    RefreshConsoleAndGameLists();
                    ConsoleCombo.Text = ConsoleName;
                    RefreshGamesSourceForConsole(ConsoleName);
                    GameCombo.Text = GameName;
                    ApplyHomeFieldLockState(GameName);
                });

                StatusPresenter.UpdatePreviewDraft(BuildRequestFromUi());
            },
            CloseTopOverlay));
    }

    private void ShowOverlay(Control OverlayControl)
    {
        RootOverlayHost.Children.Add(OverlayControl);
    }

    private void CloseTopOverlay()
    {
        if (RootOverlayHost.Children.Count > 1)
        {
            RootOverlayHost.Children.RemoveAt(RootOverlayHost.Children.Count - 1);
        }
    }

    private void OnWindowClosing(object? Sender, WindowClosingEventArgs Args)
    {
        if (!IsForceClosing && Settings.Current.MinimizeToTrayOnClose)
        {
            Args.Cancel = true;
            Hide();
        }
    }

    private void OnWindowClosed(object? Sender, EventArgs Args)
    {
        LivePreviewTimer.Stop();
        PreviewGameImage.Source = null;

        foreach (Bitmap Item in UiBitmapCache.Values)
        {
            Item.Dispose();
        }

        UiBitmapCache.Clear();
    }
}
