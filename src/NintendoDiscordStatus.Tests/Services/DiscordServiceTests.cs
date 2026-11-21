using System;
using System.Collections.Generic;
using DiscordRPC;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;
using NintendoDiscordStatus.Core.Services;
using NintendoDiscordStatus.Tests.Fakes;
using Xunit;

namespace NintendoDiscordStatus.Tests.Services;

public class DiscordServiceTests
{
    [Fact]
    public void Connect_TracksConnectionStateTransitionsAndEvents()
    {
        FakeDiscordRpcClientFactory Factory = new FakeDiscordRpcClientFactory();
        FakeDiscordRpcClient Client = new FakeDiscordRpcClient();
        Factory.NextClient = Client;

        using DiscordService Service = new DiscordService(Factory);

        List<DiscordConnectionState> ObservedStates = new List<DiscordConnectionState>();
        List<string> ObservedErrors = new List<string>();
        Service.ConnectionStateChanged += (Sender, State) => ObservedStates.Add(State);
        Service.DiscordError += (Sender, Message) => ObservedErrors.Add(Message);

        Service.Connect();
        Assert.Equal(DiscordConnectionState.Connecting, Service.ConnectionState);
        Assert.Equal(1, Client.InitializeCallCount);

        Service.Connect();
        Assert.Single(Factory.CreatedClients);

        Client.SimulateError("Temporary pipe error");
        Assert.Equal("Temporary pipe error", Service.LastErrorMessage);
        Assert.Equal(new[] { "Temporary pipe error" }, ObservedErrors);

        Client.SimulateReady("Eternals");
        Assert.Equal(DiscordConnectionState.Connected, Service.ConnectionState);
        Assert.Equal("Eternals", Service.ConnectedUsername);
        Assert.Equal(string.Empty, Service.LastErrorMessage);

        Service.Connect();
        Assert.Single(Factory.CreatedClients);

        Client.SimulateConnectionFailed("Pipe disconnected");
        Assert.Equal(DiscordConnectionState.ConnectionFailed, Service.ConnectionState);
        Assert.Equal("Pipe disconnected", Service.LastErrorMessage);
        Assert.False(Service.IsPresenceActive);

        Client.SimulateClosed("Closed by Discord");
        Assert.Equal(DiscordConnectionState.Disconnected, Service.ConnectionState);
        Assert.Equal(string.Empty, Service.ConnectedUsername);

        Assert.Equal(
            new[]
            {
                DiscordConnectionState.Connecting,
                DiscordConnectionState.Connected,
                DiscordConnectionState.ConnectionFailed,
                DiscordConnectionState.Disconnected
            },
            ObservedStates);
    }

    [Fact]
    public void Connect_HandlesImmediateFailureAndSynchronousReadyAndReconnectDisposal()
    {
        FakeDiscordRpcClientFactory Factory = new FakeDiscordRpcClientFactory();

        FakeDiscordRpcClient FailingClient = new FakeDiscordRpcClient { InitializeResult = false };
        Factory.NextClient = FailingClient;

        using DiscordService Service = new DiscordService(Factory);
        Service.Connect();
        FailingClient.SimulateError("Error without subscriber");

        Assert.Equal(DiscordConnectionState.ConnectionFailed, Service.ConnectionState);

        FakeDiscordRpcClient InstantClient = new FakeDiscordRpcClient
        {
            InitializeResult = true,
            AutoRaiseReadyOnInitialize = true
        };
        Factory.NextClient = InstantClient;

        Service.Connect();
        Assert.Equal(DiscordConnectionState.Connected, Service.ConnectionState);
        Assert.Equal(1, FailingClient.DisposeCallCount);

        InstantClient.Dispose();
        Assert.Equal(DiscordConnectionState.Connected, Service.ConnectionState);

        Service.Disconnect();
        Assert.Equal(DiscordConnectionState.Disconnected, Service.ConnectionState);
    }

    [Fact]
    public void SetGameStatus_BuildsFullDiscordPayloadPreservesAndResetsTimerCleanly()
    {
        FakeDiscordRpcClientFactory Factory = new FakeDiscordRpcClientFactory();
        FakeDiscordRpcClient Client = new FakeDiscordRpcClient();
        Factory.NextClient = Client;
        FakeTimeProvider TimeProvider = new FakeTimeProvider(new DateTime(2026, 11, 11, 20, 0, 0, DateTimeKind.Utc));

        using DiscordService Service = new DiscordService(Factory, TimeProvider);

        Assert.False(Service.SetGameStatus(new PresenceRequest
        {
            ConsoleName = "Nintendo Switch",
            GameName = "Splatoon 3"
        }));
        Assert.NotNull(Service.SessionStartUtc);

        Service.ClearStatus();
        Service.Disconnect();

        Service.Connect();
        Client.SimulateReady("Eternals");

        PresenceRequest InitialRequest = new PresenceRequest
        {
            ConsoleName = "Nintendo Switch",
            ConsoleIconKey = "nintendo_switch_icon_v2",
            GameName = "Mario Kart 8 Deluxe",
            ImageKey = "mk8d",
            Details = "Worldwides",
            State = "Singleplayer",
            TimerOffsetMinutes = 5,
            ShowTimer = true
        };

        Assert.True(Service.SetGameStatus(InitialRequest));
        Assert.True(Service.IsPresenceActive);
        DateTime ExpectedStart = new DateTime(2026, 11, 11, 19, 55, 0, DateTimeKind.Utc);
        Assert.Equal(ExpectedStart, Service.SessionStartUtc);

        RichPresence Sent1 = Client.SentPresences[0];
        Assert.Equal("Mario Kart 8 Deluxe", Sent1.Details);
        Assert.Equal("Worldwides • Singleplayer", Sent1.State);
        Assert.Equal(ExpectedStart, Sent1.Timestamps.Start);
        Assert.Equal(ActivityType.Playing, Sent1.Type);
        Assert.Equal(StatusDisplayType.Details, Sent1.StatusDisplay);
        Assert.Equal(AppConstants.GitHubImagesBaseUrl + "mk8d.png", Sent1.Assets.LargeImageKey);
        Assert.Equal("Mario Kart 8 Deluxe", Sent1.Assets.LargeImageText);
        Assert.Equal(AppConstants.GitHubImagesBaseUrl + "nintendo_switch_icon_v2.png", Sent1.Assets.SmallImageKey);
        Assert.Equal("Nintendo Switch", Sent1.Assets.SmallImageText);

        TimeProvider.Advance(TimeSpan.FromMinutes(3));
        InitialRequest.State = "Rainbow Road";
        Assert.True(Service.SetGameStatus(InitialRequest));
        Assert.Equal(ExpectedStart, Service.SessionStartUtc);

        InitialRequest.TimerOffsetMinutes = 10;
        Assert.True(Service.SetGameStatus(InitialRequest));
        Assert.Equal(TimeProvider.UtcNow - TimeSpan.FromMinutes(10), Service.SessionStartUtc);

        Service.ResetTimer(0);
        Assert.Equal(TimeProvider.UtcNow, Service.SessionStartUtc);

        PresenceRequest HomeRequest = new PresenceRequest
        {
            ConsoleName = "",
            GameName = "Home",
            ImageKey = "",
            ConsoleIconKey = ""
        };
        Assert.True(Service.SetGameStatus(HomeRequest));
        RichPresence SentHome = Client.SentPresences[3];
        Assert.Equal(AppConstants.HomeGameName, SentHome.Details);
        Assert.Equal(AppConstants.HomeIdleText, SentHome.State);
        Assert.Null(SentHome.Timestamps);
        Assert.Null(SentHome.Assets);

        PresenceRequest EmptyGameHideTimerRequest = new PresenceRequest
        {
            ConsoleName = "Nintendo Wii",
            GameName = "",
            ImageKey = "wii.png",
            ConsoleIconKey = "",
            ShowTimer = false
        };
        Assert.True(Service.SetGameStatus(EmptyGameHideTimerRequest));
        RichPresence SentEmptyGame = Client.SentPresences[4];
        Assert.Equal(AppConstants.DefaultPlayingText, SentEmptyGame.Details);
        Assert.Null(SentEmptyGame.Timestamps);
        Assert.NotNull(SentEmptyGame.Assets);
        Assert.Null(SentEmptyGame.Assets.SmallImageKey);

        PresenceRequest HomeWithIconRequest = new PresenceRequest
        {
            ConsoleName = "Nintendo Wii",
            GameName = "Home",
            ImageKey = "wii",
            ConsoleIconKey = "wii"
        };
        Assert.True(Service.SetGameStatus(HomeWithIconRequest));
        Assert.Equal("Nintendo Wii", Client.SentPresences[5].Assets.LargeImageText);

        PresenceRequest SmallOnlyRequest = new PresenceRequest
        {
            ConsoleName = "Nintendo Wii",
            GameName = "Zelda",
            ImageKey = "",
            ConsoleIconKey = "wii"
        };
        Assert.True(Service.SetGameStatus(SmallOnlyRequest));
        Assert.Equal(AppConstants.GitHubImagesBaseUrl + "wii.png", Client.SentPresences[6].Assets.LargeImageKey);
        Assert.Equal(AppConstants.GitHubImagesBaseUrl + "wii.png", Client.SentPresences[6].Assets.SmallImageKey);

        Service.ClearStatus();
        Assert.False(Service.IsPresenceActive);
        Assert.Null(Service.SessionStartUtc);
        Assert.Equal(1, Client.ClearPresenceCallCount);
    }
}
