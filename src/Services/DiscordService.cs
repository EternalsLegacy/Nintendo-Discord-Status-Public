#region Usings

using System;
using DiscordRPC;

#endregion

namespace NintendoDiscordStatus.Services;

#region Public Classes

public class DiscordService : IDisposable
{
    #region Variables

    private DiscordRpcClient? DiscordClient;
    private Timestamps? GameStartTime;

    #endregion

    #region Public Properties

    public bool IsConnected { get; private set; }

    #endregion

    #region Public Methods

    public void InitializeDiscordClient(string AppId)
    {
        if (IsConnected || DiscordClient != null)
        {
            return;
        }

        DiscordClient = new DiscordRpcClient(AppId);
        
        DiscordClient.OnError += (Sender, E) => Console.WriteLine($"Discord Error: {E.Message}");
        
        DiscordClient.Initialize();

        IsConnected = true;
    }

    public void SetGameStatus(string ConsoleName, string GameName, string CustomText, string LargeImageKey, string SmallImageKey)
    {
        if (!IsConnected || DiscordClient == null)
        {
            return;
        }

        string FinalState = CustomText;

        if (GameName == "Home")
        {
            FinalState = "Idling";
            GameStartTime = null;
        }
        else
        {
            if (GameStartTime == null)
            {
                GameStartTime = Timestamps.Now;
            }
        }

        RichPresence CurrentPresence = new RichPresence()
        {
            Details = string.IsNullOrWhiteSpace(GameName) ? "Playing" : GameName,
            State = string.IsNullOrWhiteSpace(FinalState) ? null : FinalState,
            Timestamps = GameStartTime
        };

        bool HasLargeImage = !string.IsNullOrWhiteSpace(LargeImageKey);
        bool HasSmallImage = !string.IsNullOrWhiteSpace(SmallImageKey);

        if (HasLargeImage || HasSmallImage)
        {
            CurrentPresence.Assets = new Assets();

            if (HasLargeImage)
            {
                CurrentPresence.Assets.LargeImageKey = LargeImageKey;
                CurrentPresence.Assets.LargeImageText = GameName == "Home" ? ConsoleName : GameName;
            }

            if (HasSmallImage)
            {
                CurrentPresence.Assets.SmallImageKey = SmallImageKey;
                CurrentPresence.Assets.SmallImageText = ConsoleName;
            }
        }

        DiscordClient?.SetPresence(CurrentPresence);
    }

    public void ResetTimer()
    {
        GameStartTime = Timestamps.Now;
    }

    public void ClearStatus()
    {
        if (IsConnected && DiscordClient != null)
        {
            DiscordClient?.ClearPresence();
            GameStartTime = null;
        }
    }

    public void Dispose()
    {
        ClearStatus();

        if (DiscordClient != null)
        {
            DiscordClient?.Dispose();
        }

        IsConnected = false;
    }

    #endregion
}

#endregion