using System;
using DiscordRPC;

namespace NintendoDiscordStatus.Core.Abstractions
{
    public interface IDiscordRpcClient : IDisposable
    {
        bool IsInitialized { get; }
        bool IsDisposed { get; }
        string ConnectedUsername { get; }

        event EventHandler<string>? Ready;
        event EventHandler<string>? ConnectionFailed;
        event EventHandler<string>? Closed;
        event EventHandler<string>? Error;

        bool Initialize();
        void SetPresence(RichPresence Presence);
        void ClearPresence();
    }
}
