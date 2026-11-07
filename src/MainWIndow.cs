#region Usings

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using NintendoDiscordStatus.Services;
using NintendoDiscordStatus.Models;

#endregion

namespace NintendoDiscordStatus;

#region Public Classes

public class MainWindow : Window
{
    #region Variables

    private const string GithubImagesUrl = "https://raw.githubusercontent.com/EternalsLegacy/Nintendo-Discord-Status-Public/main/images/";
    private readonly DiscordService AppDiscordService;
    private GamesLibModel Library = new GamesLibModel();
    private List<PresetModel> Presets = new List<PresetModel>();
    private readonly string PresetsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "presets.json");

    private AutoCompleteBox ConsoleCombo = null!;
    private AutoCompleteBox GameCombo = null!;
    private TextBox DetailsBox = null!;
    private TextBox StateBox = null!;

    private Image PreviewMainImage = null!;
    private TextBlock PreviewGameName = null!;
    private TextBlock PreviewDetails = null!;
    private TextBlock PreviewState = null!;
    private TextBlock PreviewConsoleIconText = null!;

    private TextBlock StatusIndicatorText = null!;
    private Avalonia.Controls.Shapes.Ellipse StatusIndicatorDot = null!;
    private Button UpdateButton = null!;
    private Button StopButton = null!;
    private Button PresetButton = null!;
    private Button SavePresetButton = null!;

    private Panel PresetOverlay = null!;
    private ListBox PresetListBox = null!;

    private readonly UpdateService AppUpdateService = new UpdateService();
    private UpdateModel? PendingUpdate;

    private Panel UpdateOverlay = null!;
    private TextBlock UpdateTitleText = null!;
    private TextBlock UpdateVersionSubtext = null!;
    private TextBlock UpdateChangelogText = null!;
    private TextBlock UpdateStatusText = null!;
    private Button UpdateInstallButton = null!;
    private Button UpdateCloseButton = null!;
    private Button CheckUpdatesFooterButton = null!;

    private readonly ISolidColorBrush WindowBackgroundBrush = SolidColorBrush.Parse("#18181A");
    private readonly ISolidColorBrush CardBackgroundBrush = SolidColorBrush.Parse("#252529");
    private readonly ISolidColorBrush InputBackgroundBrush = SolidColorBrush.Parse("#1E1E21");
    private readonly ISolidColorBrush AccentRedBrush = SolidColorBrush.Parse("#E60012");
    private readonly ISolidColorBrush TextWhiteBrush = SolidColorBrush.Parse("#F2F2F2");
    private readonly ISolidColorBrush TextGrayBrush = SolidColorBrush.Parse("#9A9A9E");
    private readonly ISolidColorBrush DiscordGreenBrush = SolidColorBrush.Parse("#43B581");

    #endregion

    #region Public Constructors

    public MainWindow()
    {
        this.Title("Nintendo Discord Rich Presence")
            .Width(450).Height(700)
            .CanResize(false)
            .Background(WindowBackgroundBrush)
            .WindowStartupLocation(WindowStartupLocation.CenterScreen);

        string IconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
        if (File.Exists(IconPath))
        {
            this.Icon = new WindowIcon(IconPath);
        }

        try
        {
            using (var Stream = Avalonia.Platform.AssetLoader.Open(new Uri("avares://NintendoDiscordStatus/app_icon.ico")))
            {
                this.Icon = new WindowIcon(Stream);
            }
        }
        catch (Exception Ex)
        {
            Console.WriteLine($"[Build] Error loading the app icon from resource: {Ex.Message}");
        }

        AppDiscordService = new DiscordService();
        AppDiscordService.InitializeDiscordClient("1482829273164284106");

        InitializeUIComponents();

        Content = new Panel()
            .Children(
                new ScrollViewer()
                    .Content(
                        new StackPanel()
                            .Margin(20)
                            .Spacing(20)
                            .Children(
                                CreateTopStatusCard(),
                                CreateInputForm(),
                                CreateLivePreviewCard(),
                                CreateFooter()
                            )
                    ),
                CreatePresetOverlay(),
                CreateUpdateOverlay()
            );

        LoadLibraryData();
        LoadPresetsData();
        BindEvents();
        TriggerDefaultState();
        CheckForUpdatesOnStartup();
    }

    #endregion

    #region Protected Methods

    protected override void OnClosed(EventArgs e)
    {
        AppDiscordService.Dispose();
        base.OnClosed(e);
    }

    #endregion

    #region Private Core Logic Methods

    private void InitializeUIComponents()
    {
        ConsoleCombo = CreateStyledAutoCompleteBox();
        GameCombo = CreateStyledAutoCompleteBox();

        DetailsBox = CreateStyledTextBox("Exploring Hyrule", false);
        StateBox = CreateStyledTextBox("Singleplayer", false);

        PreviewMainImage = new Image() { Stretch = Stretch.UniformToFill };

        PreviewGameName = new TextBlock().Text("The Legend of Zelda").Foreground(TextWhiteBrush).FontWeight(FontWeight.Bold);
        PreviewDetails = new TextBlock().Text("Exploring Hyrule").Foreground(TextGrayBrush).FontSize(13);
        PreviewState = new TextBlock().Text("Singleplayer").Foreground(TextGrayBrush).FontSize(13);

        PreviewConsoleIconText = new TextBlock().Text("NS").Foreground(TextWhiteBrush).FontSize(9).FontWeight(FontWeight.Bold).HorizontalAlignment(HorizontalAlignment.Center).VerticalAlignment(VerticalAlignment.Center);

        StatusIndicatorText = new TextBlock().Text("RPC is Inactive").Foreground(TextWhiteBrush).FontWeight(FontWeight.Bold).VerticalAlignment(VerticalAlignment.Center).Margin(new Thickness(10, 0, 0, 0));
        StatusIndicatorDot = (Avalonia.Controls.Shapes.Ellipse)new Avalonia.Controls.Shapes.Ellipse().Width(12).Height(12).Fill(SolidColorBrush.Parse("#555555")).VerticalAlignment(VerticalAlignment.Center);
    }

    private void LoadLibraryData()
    {
        try
        {
            string LibPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game_lib.json");
            if (File.Exists(LibPath))
            {
                string JsonContent = File.ReadAllText(LibPath);
                var ParsedLibrary = JsonSerializer.Deserialize<GamesLibModel>(JsonContent);
                if (ParsedLibrary != null) Library = ParsedLibrary;
            }

            ConsoleCombo.ItemsSource = Library.Consoles.Select(C => C.ConsoleName).ToList();
        }
        catch (Exception Ex)
        {
            Console.WriteLine("Error loading game_lib.json: " + Ex.Message);
        }
    }

    private void LoadPresetsData()
    {
        try
        {
            if (File.Exists(PresetsFilePath))
            {
                string JsonContent = File.ReadAllText(PresetsFilePath);
                var LoadedPresets = JsonSerializer.Deserialize<List<PresetModel>>(JsonContent);
                if (LoadedPresets != null)
                {
                    Presets = LoadedPresets;
                    PresetListBox.ItemsSource = Presets;
                }
            }
        }
        catch (Exception Ex)
        {
            Console.WriteLine("Error loading presets: " + Ex.Message);
        }
    }

    private void BindEvents()
    {
        ConsoleCombo.SelectionChanged += OnConsoleSelectionChanged;
        ConsoleCombo.KeyUp += OnComboTextChanged;

        GameCombo.SelectionChanged += OnGameSelectionChanged;
        GameCombo.KeyUp += OnComboTextChanged;

        DetailsBox.TextChanged += OnInputTextChanged;
        StateBox.TextChanged += OnInputTextChanged;

        UpdateButton.Click += OnUpdateClicked;
        StopButton.Click += OnStopClicked;
        PresetButton.Click += OnOpenPresetsClicked;
        SavePresetButton.Click += OnSaveCurrentClicked;
        CheckUpdatesFooterButton.Click += OnCheckUpdatesClicked;
    }

    private void TriggerDefaultState()
    {
        if (ConsoleCombo.ItemsSource is List<string> Consoles && Consoles.Contains("Nintendo Switch"))
        {
            ConsoleCombo.Text = "Nintendo Switch";
        }

        GameCombo.Text = "Home";
        StateBox.Text = string.Empty;

        HandleGameHomeLogic();
    }

    private void SavePresetsToFile()
    {
        File.WriteAllText(PresetsFilePath, JsonSerializer.Serialize(Presets));

        PresetListBox.ItemsSource = null;
        PresetListBox.ItemsSource = Presets;
    }

    #endregion

    #region Event Handlers

    private void OnConsoleSelectionChanged(object? Sender, SelectionChangedEventArgs E)
    {
        HandleConsoleChange();
    }

    private void OnComboTextChanged(object? Sender, KeyEventArgs E)
    {
        HandleConsoleChange();
        HandleGameHomeLogic();
    }

    private void HandleConsoleChange()
    {
        string SelectedConsole = ConsoleCombo.Text ?? string.Empty;
        var ConsoleData = Library.Consoles.FirstOrDefault(C => C.ConsoleName == SelectedConsole);

        if (ConsoleData != null)
        {
            GameCombo.ItemsSource = ConsoleData.Games.Select(G => G.GameName).ToList();
            UpdatePreviewConsoleIcon(SelectedConsole);
        }

        UpdateLivePreview();
    }

    private void OnGameSelectionChanged(object? Sender, SelectionChangedEventArgs E)
    {
        HandleGameHomeLogic();
    }

    private void HandleGameHomeLogic()
    {
        string CurrentGame = GameCombo.Text ?? string.Empty;

        if (CurrentGame.Equals("Home", StringComparison.OrdinalIgnoreCase))
        {
            DetailsBox.Text = "Idling";
            DetailsBox.IsReadOnly = true;
            DetailsBox.Foreground = TextGrayBrush;
        }
        else
        {
            if (DetailsBox.Text == "Idling") DetailsBox.Text = string.Empty;
            DetailsBox.IsReadOnly = false;
            DetailsBox.Foreground = TextWhiteBrush;
        }

        UpdateLivePreview();
    }

    private void OnInputTextChanged(object? Sender, TextChangedEventArgs E)
    {
        UpdateLivePreview();
    }

    private void UpdateLivePreview()
    {
        string CurrentConsole = ConsoleCombo.Text ?? string.Empty;
        string CurrentGame = GameCombo.Text ?? string.Empty;

        PreviewGameName.Text = string.IsNullOrWhiteSpace(CurrentGame) ? "Unknown Game" : CurrentGame;
        PreviewDetails.Text = DetailsBox.Text;
        PreviewState.Text = StateBox.Text;

        var ConsoleData = Library.Consoles.FirstOrDefault(C => C.ConsoleName == CurrentConsole);
        var GameData = ConsoleData?.Games.FirstOrDefault(G => G.GameName == CurrentGame);

        string ImageKey = GameData?.ImageIconKey ?? string.Empty;
        UpdatePreviewImageAsync(ImageKey);
    }

    private async void UpdatePreviewImageAsync(string ImageKey)
    {
        if (string.IsNullOrWhiteSpace(ImageKey))
        {
            PreviewMainImage.Source = null;
            return;
        }

        string FinalKey = ImageKey;
        if (!FinalKey.Contains('.')) FinalKey += ".png";

        // Check local image directory first for offline support and immediate loading
        string[] searchPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images", FinalKey),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "images", FinalKey),
            Path.Combine(AppContext.BaseDirectory, "images", FinalKey),
            Path.Combine(Environment.CurrentDirectory, "images", FinalKey)
        };

        foreach (var localPath in searchPaths)
        {
            if (File.Exists(localPath))
            {
                try
                {
                    var localBitmap = new Avalonia.Media.Imaging.Bitmap(localPath);
                    PreviewMainImage.Source = localBitmap;
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Preview] Error loading local image {localPath}: {ex.Message}");
                }
            }
        }

        string CacheBuster = $"?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        string Url = FinalKey.StartsWith("http")
            ? FinalKey
            : $"{GithubImagesUrl}{FinalKey}{CacheBuster}";

        try
        {
            using var Client = new HttpClient();
            var Bytes = await Client.GetByteArrayAsync(Url);
            using var Ms = new MemoryStream(Bytes);
            var Bitmap = new Avalonia.Media.Imaging.Bitmap(Ms);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                PreviewMainImage.Source = Bitmap;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Preview] Error loading the image: {ex.Message}");
            Avalonia.Threading.Dispatcher.UIThread.Post(() => PreviewMainImage.Source = null);
        }
    }

    private void UpdatePreviewConsoleIcon(string ConsoleName)
    {
        if (ConsoleName.Contains("Switch")) PreviewConsoleIconText.Text = "NS";
        else if (ConsoleName.Contains("Wii U")) PreviewConsoleIconText.Text = "WIIU";
        else if (ConsoleName.Contains("Wii")) PreviewConsoleIconText.Text = "WII";
        else if (ConsoleName.Contains("3DS")) PreviewConsoleIconText.Text = "3DS";
        else if (ConsoleName.Contains("DS")) PreviewConsoleIconText.Text = "DS";
        else PreviewConsoleIconText.Text = "??";
    }

    private void OnUpdateClicked(object? Sender, RoutedEventArgs E)
    {
        string SelectedConsole = ConsoleCombo.Text ?? string.Empty;
        string SelectedGame = GameCombo.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(SelectedConsole) || string.IsNullOrWhiteSpace(SelectedGame)) return;

        var ConsoleData = Library.Consoles.FirstOrDefault(C => C.ConsoleName == SelectedConsole);
        var GameData = ConsoleData?.Games.FirstOrDefault(G => G.GameName == SelectedGame);

        string LargeKey = GameData?.ImageIconKey ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(LargeKey) && !LargeKey.Contains('.')) LargeKey += ".png";

        string SmallKey = ConsoleData?.ConsoleIconKey ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(SmallKey) && !SmallKey.Contains('.')) SmallKey += ".png";

        string LargeImageUrl = string.IsNullOrWhiteSpace(LargeKey) ? string.Empty :
            (LargeKey.StartsWith("http") ? LargeKey : $"{GithubImagesUrl}{LargeKey}");

        string SmallImageUrl = string.IsNullOrWhiteSpace(SmallKey) ? string.Empty :
            (SmallKey.StartsWith("http") ? SmallKey : $"{GithubImagesUrl}{SmallKey}");

        AppDiscordService.SetGameStatus(
            SelectedConsole, 
            SelectedGame, 
            DetailsBox.Text ?? string.Empty, 
            StateBox.Text ?? string.Empty, 
            LargeImageUrl, 
            SmallImageUrl
        );

        StatusIndicatorText.Text = "RPC is Active";
        StatusIndicatorText.Foreground = DiscordGreenBrush;
        StatusIndicatorDot.Fill = DiscordGreenBrush;

        StopButton.IsEnabled = true;
        StopButton.Foreground = TextWhiteBrush;

        UpdateButton.Content = "Updated!";
        System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ => 
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => UpdateButton.Content = "Update")
        );
    }

    private void OnStopClicked(object? Sender, RoutedEventArgs E)
    {
        AppDiscordService.ClearStatus();

        StatusIndicatorText.Text = "RPC is Inactive";
        StatusIndicatorText.Foreground = TextWhiteBrush;
        StatusIndicatorDot.Fill = SolidColorBrush.Parse("#555555");

        StopButton.IsEnabled = false;
        StopButton.Foreground = TextGrayBrush;
    }

    private void OnSaveCurrentClicked(object? Sender, RoutedEventArgs E)
    {
        string CurrentConsole = ConsoleCombo.Text ?? string.Empty;
        string CurrentGame = GameCombo.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(CurrentConsole) || string.IsNullOrWhiteSpace(CurrentGame)) return;

        string PresetName = $"{CurrentConsole} - {CurrentGame}";

        var ExistingPreset = Presets.FirstOrDefault(P => P.PresetName == PresetName);
        if (ExistingPreset != null)
        {
            ExistingPreset.Details = DetailsBox.Text ?? string.Empty;
            ExistingPreset.State = StateBox.Text ?? string.Empty;
        }
        else
        {
            Presets.Add(new PresetModel
            {
                PresetName = PresetName,
                Console = CurrentConsole,
                Game = CurrentGame,
                Details = DetailsBox.Text ?? string.Empty,
                State = StateBox.Text ?? string.Empty
            });
        }

        SavePresetsToFile();

        SavePresetButton.Content = "Saved!";
        System.Threading.Tasks.Task.Delay(2000).ContinueWith(T => Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => SavePresetButton.Content = "Save Current"));
    }

    private void OnOpenPresetsClicked(object? Sender, RoutedEventArgs E)
    {
        PresetOverlay.IsVisible = true;
    }

    private void OnLoadPresetConfirmed(object? Sender, RoutedEventArgs E)
    {
        if (PresetListBox.SelectedItem is PresetModel SelectedPreset)
        {
            ConsoleCombo.Text = SelectedPreset.Console;
            GameCombo.Text = SelectedPreset.Game;
            DetailsBox.Text = SelectedPreset.Details;
            StateBox.Text = SelectedPreset.State;

            HandleConsoleChange();
            HandleGameHomeLogic();

            PresetOverlay.IsVisible = false;
        }
    }

    private void OnDeletePresetConfirmed(object? Sender, RoutedEventArgs E)
    {
        if (PresetListBox.SelectedItem is PresetModel SelectedPreset)
        {
            Presets.Remove(SelectedPreset);
            SavePresetsToFile();
        }
    }

    #endregion

    #region UI Build Methods

    private async void OnCheckUpdatesClicked(object? Sender, RoutedEventArgs E)
    {
        CheckUpdatesFooterButton.Content = "Checking...";
        var update = await AppUpdateService.CheckForUpdateAsync();
        if (update != null)
        {
            CheckUpdatesFooterButton.Content = $"v{UpdateService.CurrentVersionString}";
            ShowUpdateOverlay(update);
        }
        else
        {
            CheckUpdatesFooterButton.Content = "Up to date!";
            _ = System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ =>
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    CheckUpdatesFooterButton.Content = $"v{UpdateService.CurrentVersionString}"));
        }
    }

    private async void CheckForUpdatesOnStartup()
    {
        await System.Threading.Tasks.Task.Delay(1500);
        var update = await AppUpdateService.CheckForUpdateAsync();
        if (update != null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ShowUpdateOverlay(update));
        }
    }

    private void ShowUpdateOverlay(UpdateModel update)
    {
        PendingUpdate = update;
        UpdateVersionSubtext.Text = $"{update.TagName} - {update.ReleaseName}";
        UpdateChangelogText.Text = update.Changelog;
        UpdateStatusText.IsVisible = false;
        UpdateInstallButton.IsEnabled = true;
        UpdateCloseButton.IsEnabled = true;
        UpdateOverlay.IsVisible = true;
    }

    private Panel CreateUpdateOverlay()
    {
        UpdateTitleText = new TextBlock()
            .Text("UPDATE AVAILABLE")
            .Foreground(TextWhiteBrush)
            .FontWeight(FontWeight.Bold)
            .FontSize(16);

        UpdateVersionSubtext = new TextBlock()
            .Text(string.Empty)
            .Foreground(DiscordGreenBrush)
            .FontSize(13)
            .FontWeight(FontWeight.SemiBold);

        UpdateChangelogText = new TextBlock()
            .Text(string.Empty)
            .Foreground(TextWhiteBrush)
            .FontSize(13)
            .TextWrapping(TextWrapping.Wrap);

        UpdateStatusText = new TextBlock()
            .Text(string.Empty)
            .Foreground(TextGrayBrush)
            .FontSize(12)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .IsVisible(false);

        UpdateInstallButton = new Button()
            .Content("Update Now")
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .HorizontalContentAlignment(HorizontalAlignment.Center)
            .Background(DiscordGreenBrush)
            .Foreground(TextWhiteBrush)
            .FontWeight(FontWeight.Bold)
            .Col(0);

        UpdateInstallButton.Click += async (s, e) =>
        {
            if (PendingUpdate == null) return;

            UpdateInstallButton.IsEnabled = false;
            UpdateCloseButton.IsEnabled = false;
            UpdateStatusText.IsVisible = true;
            UpdateStatusText.Text = "Downloading update...";

            try
            {
                await AppUpdateService.ApplyUpdateAsync(PendingUpdate, status =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateStatusText.Text = status);
                });
            }
            catch (Exception ex)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UpdateStatusText.Text = $"Fehler: {ex.Message}";
                    UpdateInstallButton.IsEnabled = true;
                    UpdateCloseButton.IsEnabled = true;
                });
            }
        };

        UpdateCloseButton = new Button()
            .Content("Later")
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .HorizontalContentAlignment(HorizontalAlignment.Center)
            .Background(InputBackgroundBrush)
            .Foreground(TextGrayBrush)
            .Col(2);

        UpdateCloseButton.Click += (s, e) => UpdateOverlay.IsVisible = false;

        UpdateOverlay = new Panel()
            .Background(new SolidColorBrush(Colors.Black) { Opacity = 0.85 })
            .IsVisible(false)
            .ZIndex(110)
            .Children(
                new Border()
                    .Background(CardBackgroundBrush)
                    .CornerRadius(8)
                    .Padding(20)
                    .Width(380)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Child(
                        new StackPanel()
                            .Spacing(14)
                            .Children(
                                UpdateTitleText,
                                UpdateVersionSubtext,
                                new TextBlock().Text("CHANGELOG").Foreground(TextGrayBrush).FontSize(11).FontWeight(FontWeight.Bold),
                                new Border()
                                    .Background(InputBackgroundBrush)
                                    .CornerRadius(6)
                                    .Padding(12)
                                    .MaxHeight(200)
                                    .Child(
                                        new ScrollViewer()
                                            .Content(UpdateChangelogText)
                                    ),
                                UpdateStatusText,
                                new Grid()
                                    .Cols("*, 10, *")
                                    .Children(
                                        UpdateInstallButton,
                                        UpdateCloseButton
                                    )
                            )
                    )
            );

        return UpdateOverlay;
    }

    private Panel CreatePresetOverlay()
    {
        Button LoadBtn = new Button().Content("Load").HorizontalAlignment(HorizontalAlignment.Stretch).HorizontalContentAlignment(HorizontalAlignment.Center).Background(DiscordGreenBrush).Foreground(TextWhiteBrush).Col(0);
        LoadBtn.Click += OnLoadPresetConfirmed;

        Button DeleteBtn = new Button().Content("Delete").HorizontalAlignment(HorizontalAlignment.Stretch).HorizontalContentAlignment(HorizontalAlignment.Center).Background(AccentRedBrush).Foreground(TextWhiteBrush).Col(2);
        DeleteBtn.Click += OnDeletePresetConfirmed;

        Button CloseBtn = new Button().Content("Close").HorizontalAlignment(HorizontalAlignment.Stretch).HorizontalContentAlignment(HorizontalAlignment.Center).Background(InputBackgroundBrush).Foreground(TextWhiteBrush);
        CloseBtn.Click += (s, e) => PresetOverlay.IsVisible = false;

        PresetOverlay = new Panel()
            .Background(new SolidColorBrush(Colors.Black) { Opacity = 0.85 })
            .IsVisible(false)
            .ZIndex(100)
            .Children(
                new Border()
                    .Background(CardBackgroundBrush)
                    .CornerRadius(8)
                    .Padding(20)
                    .Width(350)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Child(
                        new StackPanel()
                            .Spacing(15)
                            .Children(
                                new TextBlock().Text("SAVED PRESETS").Foreground(TextWhiteBrush).FontWeight(FontWeight.Bold).FontSize(16),

                                PresetListBox = new ListBox()
                                    .Height(200)
                                    .Background(InputBackgroundBrush)
                                    .Foreground(TextWhiteBrush)
                                    .CornerRadius(6),

                                new Grid()
                                    .Cols("*, 10, *")
                                    .Children(
                                        LoadBtn,
                                        DeleteBtn
                                    ),

                                CloseBtn
                            )
                    )
            );

        return PresetOverlay;
    }

    private Border CreateTopStatusCard()
    {
        StopButton = new Button()
            .Content("Stop")
            .Background(InputBackgroundBrush)
            .Foreground(TextGrayBrush)
            .FontWeight(FontWeight.Bold)
            .CornerRadius(6)
            .Padding(new Thickness(12, 8))
            .IsEnabled(false);

        UpdateButton = new Button()
            .Content("Update")
            .Background(AccentRedBrush)
            .Foreground(TextWhiteBrush)
            .FontWeight(FontWeight.Bold)
            .CornerRadius(6)
            .Padding(new Thickness(15, 8));

        return (Border)new Border()
            .Background(CardBackgroundBrush)
            .CornerRadius(8)
            .Padding(15)
            .Child(
                new Grid()
                    .Cols("Auto, *, Auto")
                    .Children(
                        StatusIndicatorDot.Col(0),
                        StatusIndicatorText.Col(1),
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(8)
                            .Col(2)
                            .Children(
                                StopButton,
                                UpdateButton
                            )
                    )
            );
    }

    private StackPanel CreateInputForm()
    {
        return new StackPanel()
            .Spacing(15)
            .Children(
                CreateStyledLabel("CONSOLE"),
                ConsoleCombo,

                CreateStyledLabel("GAME NAME"),
                GameCombo,

                CreateStyledLabel("DETAILS (TOP LINE)"),
                DetailsBox,

                CreateStyledLabel("STATE (BOTTOM LINE)"),
                StateBox
            );
    }

    private Border CreateLivePreviewCard()
    {
        return (Border)new Border()
            .Background(CardBackgroundBrush)
            .CornerRadius(8)
            .Padding(20)
            .Child(
                new StackPanel()
                    .Spacing(15)
                    .Children(
                        new TextBlock()
                            .Text("LIVE PREVIEW")
                            .Foreground(TextGrayBrush)
                            .FontSize(12)
                            .FontWeight(FontWeight.Bold),

                        new StackPanel()
                            .Spacing(7)
                            .Children(
                                new TextBlock()
                                    .Text("PLAYING A GAME")
                                    .Foreground(TextWhiteBrush)
                                    .FontSize(12)
                                    .FontWeight(FontWeight.Bold),

                                new Grid()
                                    .Cols("80, *")
                                    .Margin(new Thickness(0, 10, 0, 0))
                                    .Children(

                                        new Panel()
                                            .Width(70).Height(70)
                                            .HorizontalAlignment(HorizontalAlignment.Left)
                                            .Col(0)
                                            .Children(
                                                new Border()
                                                    .Background(InputBackgroundBrush)
                                                    .CornerRadius(8)
                                                    .Width(70).Height(70)
                                                    .ClipToBounds(true)
                                                    .Child(PreviewMainImage),

                                                new Border()
                                                    .Background(AccentRedBrush)
                                                    .CornerRadius(12)
                                                    .Width(24).Height(24)
                                                    .HorizontalAlignment(HorizontalAlignment.Right)
                                                    .VerticalAlignment(VerticalAlignment.Bottom)
                                                    .Margin(new Thickness(0, 0, -4, -4))
                                                    .Child(
                                                        PreviewConsoleIconText
                                                    )
                                            ),

                                        new StackPanel()
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Spacing(4)
                                            .Col(1)
                                            .Children(
                                                PreviewGameName,
                                                PreviewDetails,
                                                PreviewState,
                                                new TextBlock().Text("00:00").Foreground(TextGrayBrush).FontSize(13)
                                            )
                                    )
                            )
                    )
            );
    }

    private Grid CreateFooter()
    {
        PresetButton = new Button()
            .Content("Presets")
            .Background(Brushes.Transparent)
            .Foreground(TextGrayBrush)
            .Col(0);

        SavePresetButton = new Button()
            .Content("Save Current")
            .Background(Brushes.Transparent)
            .Foreground(AccentRedBrush)
            .Col(2);

        return new Grid()
            .Cols("Auto, *, Auto")
            .Margin(new Thickness(0, 10, 0, 0))
            .Children(
                PresetButton,
                SavePresetButton
            );
    }

    #endregion

    #region UI Component Helpers

    private TextBlock CreateStyledLabel(string LabelText)
    {
        return new TextBlock()
            .Text(LabelText)
            .Foreground(TextGrayBrush)
            .FontSize(12)
            .FontWeight(FontWeight.Bold)
            .Margin(new Thickness(0, 5, 0, -10));
    }

    private TextBox CreateStyledTextBox(string WatermarkText, bool IsActiveBorder)
    {
        ISolidColorBrush CurrentBorderBrush = IsActiveBorder ? AccentRedBrush : CardBackgroundBrush;

        TextBox Box = new TextBox()
            .Watermark(WatermarkText)
            .Background(InputBackgroundBrush)
            .Foreground(TextWhiteBrush)
            .BorderBrush(CurrentBorderBrush)
            .BorderThickness(1)
            .CornerRadius(6)
            .Padding(new Thickness(12, 10));

        Box.Resources.Add("TextControlBorderBrushFocused", CurrentBorderBrush);
        Box.Resources.Add("TextControlBorderBrushPointerOver", CurrentBorderBrush);
        Box.Resources.Add("TextControlBackgroundFocused", InputBackgroundBrush);
        Box.Resources.Add("TextControlBackgroundPointerOver", InputBackgroundBrush);

        return Box;
    }

    private AutoCompleteBox CreateStyledAutoCompleteBox()
    {
        AutoCompleteBox Box = new AutoCompleteBox()
            .Background(InputBackgroundBrush)
            .Foreground(TextWhiteBrush)
            .BorderBrush(CardBackgroundBrush)
            .BorderThickness(1)
            .CornerRadius(6)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .Padding(new Thickness(12, 10));

        Box.FilterMode = AutoCompleteFilterMode.Contains;

        Box.Resources.Add("TextControlBorderBrushFocused", CardBackgroundBrush);
        Box.Resources.Add("TextControlBorderBrushPointerOver", CardBackgroundBrush);
        Box.Resources.Add("TextControlBackgroundFocused", InputBackgroundBrush);
        Box.Resources.Add("TextControlBackgroundPointerOver", InputBackgroundBrush);

        return Box;
    }

    #endregion
}

#endregion