using System;
using DiscordRPC;
using DiscordRPC.Message;
using NintendoDiscordStatus.Core.Abstractions;

namespace NintendoDiscordStatus.Services;

public class DiscordRpcClientFactory : IDiscordRpcClientFactory
{
    public IDiscordRpcClient CreateClient(string ApplicationId)
    {
        return new DiscordRpcClientAdapter(ApplicationId);
    }
}

public class DiscordRpcClientAdapter : IDiscordRpcClient
{
    private readonly DiscordRpcClient InnerClient;

    public DiscordRpcClientAdapter(string ApplicationId)
    {
        InnerClient = new DiscordRpcClient(ApplicationId);
        InnerClient.OnReady += OnInnerReady;
        InnerClient.OnConnectionFailed += OnInnerConnectionFailed;
        InnerClient.OnClose += OnInnerClose;
        InnerClient.OnError += OnInnerError;
    }

    public bool IsInitialized => InnerClient.IsInitialized;
    public bool IsDisposed => InnerClient.IsDisposed;
    public string ConnectedUsername => InnerClient.CurrentUser?.Username ?? string.Empty;

    public event EventHandler<string>? Ready;
    public event EventHandler<string>? ConnectionFailed;
    public event EventHandler<string>? Closed;
    public event EventHandler<string>? Error;

    public bool Initialize()
    {
        return InnerClient.Initialize();
    }

    public void SetPresence(RichPresence Presence)
    {
        InnerClient.SetPresence(Presence);
    }

    public void ClearPresence()
    {
        InnerClient.ClearPresence();
    }

    public void Dispose()
    {
        InnerClient.OnReady -= OnInnerReady;
        InnerClient.OnConnectionFailed -= OnInnerConnectionFailed;
        InnerClient.OnClose -= OnInnerClose;
        InnerClient.OnError -= OnInnerError;

        if (!InnerClient.IsDisposed)
        {
            InnerClient.Dispose();
        }
    }

    private void OnInnerReady(object Sender, ReadyMessage Args)
    {
        Ready?.Invoke(this, Args.User?.Username ?? string.Empty);
    }

    private void OnInnerConnectionFailed(object Sender, ConnectionFailedMessage Args)
    {
        ConnectionFailed?.Invoke(this, $"Connection failed on pipe {Args.FailedPipe}.");
    }

    private void OnInnerClose(object Sender, CloseMessage Args)
    {
        Closed?.Invoke(this, Args.Reason ?? string.Empty);
    }

    private void OnInnerError(object Sender, ErrorMessage Args)
    {
        Error?.Invoke(this, Args.Message ?? string.Empty);
    }
}
