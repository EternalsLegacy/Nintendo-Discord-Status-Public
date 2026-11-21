using System;
using DiscordRPC;
using NintendoDiscordStatus.Core.Abstractions;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Helpers;
using NintendoDiscordStatus.Core.Models;

namespace NintendoDiscordStatus.Core.Services;

public class DiscordService : IDisposable
{
    private readonly IDiscordRpcClientFactory ClientFactory;
    private readonly ITimeProvider TimeProvider;

    private IDiscordRpcClient? Client;
    private int LastAppliedOffsetMinutes;

    public DiscordService(IDiscordRpcClientFactory ClientFactory, ITimeProvider? TimeProvider = null)
    {
        this.ClientFactory = ClientFactory;
        this.TimeProvider = TimeProvider ?? new SystemTimeProvider();
    }

    public DiscordConnectionState ConnectionState { get; private set; } = DiscordConnectionState.Disconnected;
    public string ConnectedUsername { get; private set; } = string.Empty;
    public string LastErrorMessage { get; private set; } = string.Empty;
    public bool IsPresenceActive { get; private set; }
    public DateTime? SessionStartUtc { get; private set; }
    public string CurrentGameName { get; private set; } = string.Empty;

    public event EventHandler<DiscordConnectionState>? ConnectionStateChanged;
    public event EventHandler<string>? DiscordError;

    public void Connect()
    {
        if (Client != null && !Client.IsDisposed
            && (ConnectionState == DiscordConnectionState.Connected || ConnectionState == DiscordConnectionState.Connecting))
        {
            return;
        }

        DetachAndDisposeClient();

        Client = ClientFactory.CreateClient(AppConstants.DiscordAppId);
        Client.Ready += OnClientReady;
        Client.ConnectionFailed += OnClientConnectionFailed;
        Client.Closed += OnClientClosed;
        Client.Error += OnClientError;

        SetConnectionState(DiscordConnectionState.Connecting);

        if (!Client.Initialize() && ConnectionState == DiscordConnectionState.Connecting)
        {
            LastErrorMessage = "Failed to initialize Discord RPC client.";
            SetConnectionState(DiscordConnectionState.ConnectionFailed);
        }
    }

    public bool SetGameStatus(PresenceRequest Request)
    {
        UpdateSessionTimer(Request);

        if (Client == null || Client.IsDisposed
            || ConnectionState == DiscordConnectionState.Disconnected
            || ConnectionState == DiscordConnectionState.ConnectionFailed)
        {
            IsPresenceActive = false;
            return false;
        }

        bool IsHome = PresenceFormatter.IsHomeGame(Request.GameName);
        string ConsoleName = string.IsNullOrWhiteSpace(Request.ConsoleName)
            ? AppConstants.DefaultConsoleName
            : Request.ConsoleName.Trim();

        string DetailsText = IsHome
            ? AppConstants.HomeGameName
            : (string.IsNullOrWhiteSpace(Request.GameName)
                ? AppConstants.DefaultPlayingText
                : PresenceFormatter.Truncate(Request.GameName, AppConstants.MaxDetailsLength));

        string? StateText = PresenceFormatter.BuildDiscordState(Request);

        RichPresence Presence = new RichPresence
        {
            Details = DetailsText,
            State = StateText,
            Timestamps = (!IsHome && Request.ShowTimer && SessionStartUtc.HasValue)
                ? new Timestamps(SessionStartUtc.Value)
                : null,
            Type = ActivityType.Playing,
            StatusDisplay = StatusDisplayType.Details
        };

        string LargeImageKey = PresenceFormatter.ResolveDiscordImageKey(Request.ImageKey, Request.ConsoleIconKey);
        string SmallImageKey = PresenceFormatter.ResolveDiscordImageKey(Request.ConsoleIconKey);

        if (!string.IsNullOrEmpty(LargeImageKey) || !string.IsNullOrEmpty(SmallImageKey))
        {
            Assets PresenceAssets = new Assets();

            if (!string.IsNullOrEmpty(LargeImageKey))
            {
                PresenceAssets.LargeImageKey = LargeImageKey;
                PresenceAssets.LargeImageText = IsHome ? ConsoleName : DetailsText;
            }

            if (!string.IsNullOrEmpty(SmallImageKey))
            {
                PresenceAssets.SmallImageKey = SmallImageKey;
                PresenceAssets.SmallImageText = ConsoleName;
            }

            Presence.Assets = PresenceAssets;
        }

        Client.SetPresence(Presence);
        IsPresenceActive = true;
        return true;
    }

    public void ResetTimer(int OffsetMinutes = 0)
    {
        TimeSpan Offset = OffsetMinutes > 0 ? TimeSpan.FromMinutes(OffsetMinutes) : TimeSpan.Zero;
        SessionStartUtc = TimeProvider.UtcNow - Offset;
        LastAppliedOffsetMinutes = OffsetMinutes;
    }

    public void ClearStatus()
    {
        if (Client != null && !Client.IsDisposed)
        {
            Client.ClearPresence();
        }

        IsPresenceActive = false;
        SessionStartUtc = null;
        CurrentGameName = string.Empty;
    }

    public void Disconnect()
    {
        if (Client != null && !Client.IsDisposed)
        {
            Client.ClearPresence();
        }

        DetachAndDisposeClient();
        IsPresenceActive = false;
        SessionStartUtc = null;
        CurrentGameName = string.Empty;
        ConnectedUsername = string.Empty;
        SetConnectionState(DiscordConnectionState.Disconnected);
    }

    public void Dispose()
    {
        Disconnect();
    }

    private void UpdateSessionTimer(PresenceRequest Request)
    {
        if (PresenceFormatter.IsHomeGame(Request.GameName))
        {
            SessionStartUtc = null;
            CurrentGameName = AppConstants.HomeGameName;
            LastAppliedOffsetMinutes = 0;
            return;
        }

        string NormalizedGameName = Request.GameName.Trim();
        bool GameChanged = !string.Equals(CurrentGameName, NormalizedGameName, StringComparison.OrdinalIgnoreCase);
        bool OffsetChanged = LastAppliedOffsetMinutes != Request.TimerOffsetMinutes;

        if (!SessionStartUtc.HasValue || GameChanged || OffsetChanged)
        {
            ResetTimer(Request.TimerOffsetMinutes);
            CurrentGameName = NormalizedGameName;
        }
    }

    private void DetachAndDisposeClient()
    {
        if (Client == null)
        {
            return;
        }

        Client.Ready -= OnClientReady;
        Client.ConnectionFailed -= OnClientConnectionFailed;
        Client.Closed -= OnClientClosed;
        Client.Error -= OnClientError;

        if (!Client.IsDisposed)
        {
            Client.Dispose();
        }

        Client = null;
    }

    private void SetConnectionState(DiscordConnectionState NewState)
    {
        ConnectionState = NewState;
        ConnectionStateChanged?.Invoke(this, NewState);
    }

    private void OnClientReady(object? Sender, string Username)
    {
        ConnectedUsername = Username;
        LastErrorMessage = string.Empty;
        SetConnectionState(DiscordConnectionState.Connected);
    }

    private void OnClientConnectionFailed(object? Sender, string Reason)
    {
        LastErrorMessage = Reason;
        IsPresenceActive = false;
        SetConnectionState(DiscordConnectionState.ConnectionFailed);
    }

    private void OnClientClosed(object? Sender, string Reason)
    {
        LastErrorMessage = Reason;
        ConnectedUsername = string.Empty;
        IsPresenceActive = false;
        SetConnectionState(DiscordConnectionState.Disconnected);
    }

    private void OnClientError(object? Sender, string Message)
    {
        LastErrorMessage = Message;
        DiscordError?.Invoke(this, Message);
    }
}
