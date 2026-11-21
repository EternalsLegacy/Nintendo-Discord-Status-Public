using System;
using System.Collections.Generic;
using DiscordRPC;
using NintendoDiscordStatus.Core.Abstractions;

namespace NintendoDiscordStatus.Tests.Fakes;

public sealed class FakeDiscordRpcClient : IDiscordRpcClient
{
    public bool IsInitialized { get; private set; }
    public bool IsDisposed { get; private set; }
    public string ConnectedUsername { get; private set; } = string.Empty;

    public bool InitializeResult { get; set; } = true;
    public bool AutoRaiseReadyOnInitialize { get; set; }
    public int InitializeCallCount { get; private set; }
    public int ClearPresenceCallCount { get; private set; }
    public int DisposeCallCount { get; private set; }
    public List<RichPresence> SentPresences { get; } = new List<RichPresence>();

    public event EventHandler<string>? Ready;
    public event EventHandler<string>? ConnectionFailed;
    public event EventHandler<string>? Closed;
    public event EventHandler<string>? Error;

    public bool Initialize()
    {
        InitializeCallCount++;
        IsInitialized = InitializeResult;

        if (InitializeResult && AutoRaiseReadyOnInitialize)
        {
            SimulateReady("InstantUser");
        }

        return InitializeResult;
    }

    public void SetPresence(RichPresence Presence)
    {
        SentPresences.Add(Presence);
    }

    public void ClearPresence()
    {
        ClearPresenceCallCount++;
    }

    public void Dispose()
    {
        DisposeCallCount++;
        IsDisposed = true;
    }

    public void SimulateReady(string Username)
    {
        ConnectedUsername = Username;
        Ready?.Invoke(this, Username);
    }

    public void SimulateConnectionFailed(string Reason)
    {
        ConnectionFailed?.Invoke(this, Reason);
    }

    public void SimulateClosed(string Reason)
    {
        Closed?.Invoke(this, Reason);
    }

    public void SimulateError(string Message)
    {
        Error?.Invoke(this, Message);
    }
}

public sealed class FakeDiscordRpcClientFactory : IDiscordRpcClientFactory
{
    public List<string> RequestedApplicationIds { get; } = new List<string>();
    public List<FakeDiscordRpcClient> CreatedClients { get; } = new List<FakeDiscordRpcClient>();
    public FakeDiscordRpcClient NextClient { get; set; } = new FakeDiscordRpcClient();

    public IDiscordRpcClient CreateClient(string ApplicationId)
    {
        RequestedApplicationIds.Add(ApplicationId);
        FakeDiscordRpcClient Client = NextClient;
        CreatedClients.Add(Client);
        NextClient = new FakeDiscordRpcClient();
        return Client;
    }
}
