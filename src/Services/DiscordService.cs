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
    private string? CurrentGameName;

    #endregion

    #region Public Properties

    public bool IsConnected { get; private set; }
    public bool IsPresenceActive { get; private set; }

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

    public void SetGameStatus(string ConsoleName, string GameName, string DetailsText, string StateText, string LargeImageKey, string SmallImageKey)
    {
        if (!IsConnected || DiscordClient == null)
        {
            return;
        }

        string FinalDetails = string.IsNullOrWhiteSpace(GameName) ? "Playing" : GameName;
        string FinalState = string.Empty;

        if (GameName == "Home")
        {
            FinalState = "Idling";
            GameStartTime = null;
            CurrentGameName = GameName;
        }
        else
        {
            if (GameStartTime == null || CurrentGameName != GameName)
            {
                GameStartTime = Timestamps.Now;
                CurrentGameName = GameName;
            }

            bool hasDetails = !string.IsNullOrWhiteSpace(DetailsText);
            bool hasState = !string.IsNullOrWhiteSpace(StateText);

            if (hasDetails && hasState)
            {
                FinalState = $"{DetailsText.Trim()} • {StateText.Trim()}";
            }
            else if (hasDetails)
            {
                FinalState = DetailsText.Trim();
            }
            else if (hasState)
            {
                FinalState = StateText.Trim();
            }
        }

        RichPresence CurrentPresence = new RichPresence()
        {
            Details = FinalDetails,
            State = string.IsNullOrWhiteSpace(FinalState) ? null : FinalState,
            Timestamps = GameStartTime,
            StatusDisplay = StatusDisplayType.Details
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
        IsPresenceActive = true;
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
            CurrentGameName = null;
            IsPresenceActive = false;
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