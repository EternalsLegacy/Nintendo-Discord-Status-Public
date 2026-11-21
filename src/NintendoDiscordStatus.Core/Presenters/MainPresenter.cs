using System;
using System.Threading;
using System.Threading.Tasks;
using NintendoDiscordStatus.Core.Abstractions;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Helpers;
using NintendoDiscordStatus.Core.Models;
using NintendoDiscordStatus.Core.Services;

namespace NintendoDiscordStatus.Core.Presenters;

public class MainPresenter : IDisposable
{
    private readonly SettingsService Settings;
    private readonly LibraryService Library;
    private readonly PresetService Presets;
    private readonly DiscordService Discord;
    private readonly ImageCacheService ImageCache;
    private readonly UpdateService Updates;
    private readonly ITimeProvider TimeProvider;

    private CancellationTokenSource? ImageLoadCts;
    private string LastRequestedImageKey = "\0";

    public MainPresenter(
        SettingsService Settings,
        LibraryService Library,
        PresetService Presets,
        DiscordService Discord,
        ImageCacheService ImageCache,
        UpdateService Updates,
        ITimeProvider? TimeProvider = null)
    {
        this.Settings = Settings;
        this.Library = Library;
        this.Presets = Presets;
        this.Discord = Discord;
        this.ImageCache = ImageCache;
        this.Updates = Updates;
        this.TimeProvider = TimeProvider ?? new SystemTimeProvider();

        CurrentDraft = new PresenceRequest();
        this.Discord.ConnectionStateChanged += OnDiscordConnectionStateChanged;
        this.Discord.DiscordError += OnDiscordServiceError;
    }

    public PresenceRequest CurrentDraft { get; private set; }
    public DiscordConnectionState ConnectionState => Discord.ConnectionState;
    public bool IsPresenceActive => Discord.IsPresenceActive;
    public AppSettings AppSettings => Settings.Current;

    public event EventHandler<PresenceRequest>? StateRestored;
    public event EventHandler<LivePreviewModel>? PreviewUpdated;
    public event EventHandler<string>? ThemeUpdated;
    public event EventHandler<byte[]?>? PreviewImageReady;
    public event EventHandler<DiscordConnectionState>? ConnectionStateChanged;
    public event EventHandler<string>? DiscordError;
    public event EventHandler? LibraryUpdated;
    public event EventHandler? RecentGamesUpdated;
    public event EventHandler<UpdateInfoModel>? UpdateAvailable;

    public async Task InitializeAsync(CancellationToken Token = default)
    {
        Settings.Load();
        Library.Load();
        Presets.Load();

        CurrentDraft = BuildInitialDraft();
        ResolveRequestAssets(CurrentDraft);

        Discord.Connect();

        StateRestored?.Invoke(this, CurrentDraft);
        EmitThemeAndPreview();
        await LoadPreviewImageIfChangedAsync(CurrentDraft.ImageKey, Token);

        if (Settings.Current.AutoSyncGameLibrary && !Token.IsCancellationRequested)
        {
            await SyncLibraryAsync(Token);
        }

        if (Settings.Current.AutoCheckUpdates && !Token.IsCancellationRequested)
        {
            await CheckForUpdatesAsync(Token);
        }
    }

    public void UpdatePreviewDraft(PresenceRequest Request)
    {
        CurrentDraft = Request;
        ResolveRequestAssets(CurrentDraft);
        EmitThemeAndPreview();
        _ = LoadPreviewImageIfChangedAsync(CurrentDraft.ImageKey, CancellationToken.None);
    }

    public bool ApplyStatus(PresenceRequest Request, bool RecordAsRecent = true)
    {
        CurrentDraft = Request;
        ResolveRequestAssets(CurrentDraft);

        bool SentToDiscord = Discord.SetGameStatus(CurrentDraft);
        Settings.SaveLastStatus(CurrentDraft);

        if (RecordAsRecent)
        {
            Settings.AddRecentGame(CurrentDraft.ConsoleName, CurrentDraft.GameName);
            RecentGamesUpdated?.Invoke(this, EventArgs.Empty);
        }

        EmitThemeAndPreview();
        _ = LoadPreviewImageIfChangedAsync(CurrentDraft.ImageKey, CancellationToken.None);
        return SentToDiscord;
    }

    public void ResetTimer()
    {
        Discord.ResetTimer(CurrentDraft.TimerOffsetMinutes);
        if (Discord.IsPresenceActive)
        {
            Discord.SetGameStatus(CurrentDraft);
        }

        PreviewUpdated?.Invoke(this, GetCurrentLivePreview());
    }

    public void StopPresence()
    {
        Discord.ClearStatus();
        PreviewUpdated?.Invoke(this, GetCurrentLivePreview());
    }

    public void QuickSwitchGame(string ConsoleName, string GameName)
    {
        PresenceRequest Request = new PresenceRequest
        {
            ConsoleName = ConsoleName,
            GameName = GameName,
            ShowTimer = Settings.Current.ShowElapsedTimer
        };

        ApplyStatus(Request, RecordAsRecent: true);
        StateRestored?.Invoke(this, CurrentDraft);
    }

    public void ApplyPreset(PresetModel Preset)
    {
        PresenceRequest Request = new PresenceRequest
        {
            ConsoleName = Preset.ConsoleName,
            GameName = Preset.GameName,
            Details = Preset.Details,
            State = Preset.State,
            ShowTimer = Settings.Current.ShowElapsedTimer
        };

        ApplyStatus(Request, RecordAsRecent: true);
        StateRestored?.Invoke(this, CurrentDraft);
    }

    public bool SaveCurrentAsPreset(string PresetName)
    {
        PresetModel Preset = new PresetModel
        {
            PresetName = PresetName,
            ConsoleName = CurrentDraft.ConsoleName,
            GameName = CurrentDraft.GameName,
            Details = CurrentDraft.Details,
            State = CurrentDraft.State
        };

        return Presets.SaveOrUpdatePreset(Preset);
    }

    public bool AddCustomGame(string ConsoleName, string GameName, string ImageKeyOrPath)
    {
        ConsoleModel? ExistingConsole = Library.FindConsole(ConsoleName);
        string ConsoleIconKey = ExistingConsole?.ConsoleIconKey ?? string.Empty;

        bool Added = Library.AddCustomGame(ConsoleName, GameName, ImageKeyOrPath, ConsoleIconKey);
        if (Added)
        {
            LibraryUpdated?.Invoke(this, EventArgs.Empty);
        }

        return Added;
    }

    public async Task<bool> SyncLibraryAsync(CancellationToken Token = default)
    {
        bool Synced = await Library.SyncFromGitHubAsync(Token);
        if (!Synced)
        {
            return false;
        }

        LibraryUpdated?.Invoke(this, EventArgs.Empty);
        ResolveRequestAssets(CurrentDraft);
        EmitThemeAndPreview();
        await LoadPreviewImageIfChangedAsync(CurrentDraft.ImageKey, Token);
        return true;
    }

    public async Task<UpdateInfoModel?> CheckForUpdatesAsync(CancellationToken Token = default)
    {
        UpdateInfoModel? UpdateInfo = await Updates.CheckForUpdatesAsync(Token);
        if (UpdateInfo != null && UpdateInfo.IsUpdateAvailable)
        {
            UpdateAvailable?.Invoke(this, UpdateInfo);
        }

        return UpdateInfo;
    }

    public void UpdateSettings(Action<AppSettings> Mutator)
    {
        Settings.Update(Mutator);
        CurrentDraft.ShowTimer = Settings.Current.ShowElapsedTimer;
        EmitThemeAndPreview();

        if (Discord.IsPresenceActive)
        {
            Discord.SetGameStatus(CurrentDraft);
        }
    }

    public LivePreviewModel GetCurrentLivePreview()
    {
        CurrentDraft.ShowTimer = Settings.Current.ShowElapsedTimer;
        return PresenceFormatter.BuildLivePreview(CurrentDraft, Discord.SessionStartUtc, TimeProvider.UtcNow);
    }

    public void Dispose()
    {
        Discord.ConnectionStateChanged -= OnDiscordConnectionStateChanged;
        Discord.DiscordError -= OnDiscordServiceError;
        CancelPendingImageLoad();
    }

    private PresenceRequest BuildInitialDraft()
    {
        AppSettings Config = Settings.Current;
        if (!Config.RememberLastStatus)
        {
            return new PresenceRequest
            {
                ConsoleName = AppConstants.DefaultConsoleName,
                GameName = AppConstants.HomeGameName,
                Details = AppConstants.HomeIdleText,
                ShowTimer = Config.ShowElapsedTimer
            };
        }

        return new PresenceRequest
        {
            ConsoleName = string.IsNullOrWhiteSpace(Config.LastConsole) ? AppConstants.DefaultConsoleName : Config.LastConsole,
            GameName = string.IsNullOrWhiteSpace(Config.LastGame) ? AppConstants.HomeGameName : Config.LastGame,
            Details = Config.LastDetails,
            State = Config.LastState,
            ShowTimer = Config.ShowElapsedTimer
        };
    }

    private void ResolveRequestAssets(PresenceRequest Request)
    {
        ConsoleModel? ConsoleItem = Library.FindConsole(Request.ConsoleName);
        Request.ConsoleIconKey = ConsoleItem?.ConsoleIconKey ?? string.Empty;
        Request.ShowTimer = Settings.Current.ShowElapsedTimer;

        GameModel? GameItem = Library.FindGame(Request.ConsoleName, Request.GameName);
        if (GameItem != null)
        {
            Request.ImageKey = GameItem.ImageIconKey;
            return;
        }

        Request.ImageKey = PresenceFormatter.IsHomeGame(Request.GameName)
            ? Request.ConsoleIconKey
            : string.Empty;
    }

    private async Task LoadPreviewImageIfChangedAsync(string ImageKey, CancellationToken ExternalToken)
    {
        if (string.Equals(LastRequestedImageKey, ImageKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        LastRequestedImageKey = ImageKey;
        CancelPendingImageLoad();

        ImageLoadCts = ExternalToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(ExternalToken)
            : new CancellationTokenSource();

        CancellationToken ActiveToken = ImageLoadCts.Token;
        byte[]? ImageBytes = await ImageCache.GetImageBytesAsync(ImageKey, ActiveToken);

        if (!ActiveToken.IsCancellationRequested)
        {
            PreviewImageReady?.Invoke(this, ImageBytes);
        }
    }

    private void CancelPendingImageLoad()
    {
        if (ImageLoadCts == null)
        {
            return;
        }

        ImageLoadCts.Cancel();
        ImageLoadCts.Dispose();
        ImageLoadCts = null;
    }

    private void EmitThemeAndPreview()
    {
        string AccentHex = ThemeHelper.GetAccentHexForConsole(
            CurrentDraft.ConsoleName,
            Settings.Current.DynamicConsoleTheme);

        ThemeUpdated?.Invoke(this, AccentHex);
        PreviewUpdated?.Invoke(this, GetCurrentLivePreview());
    }

    private void OnDiscordConnectionStateChanged(object? Sender, DiscordConnectionState NewState)
    {
        ConnectionStateChanged?.Invoke(this, NewState);
    }

    private void OnDiscordServiceError(object? Sender, string Message)
    {
        DiscordError?.Invoke(this, Message);
    }
}
