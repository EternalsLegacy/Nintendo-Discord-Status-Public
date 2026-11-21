using System;
using System.IO;
using System.Text.Json;
using NintendoDiscordStatus.Core.Abstractions;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Helpers;
using NintendoDiscordStatus.Core.Models;

namespace NintendoDiscordStatus.Core.Services;

public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    private static SettingsService? GlobalInstance;

    private readonly string SettingsFilePath;
    private readonly ITimeProvider TimeProvider;

    public SettingsService(string? BaseDirectory = null, ITimeProvider? TimeProvider = null)
    {
        string EffectiveBaseDirectory = string.IsNullOrWhiteSpace(BaseDirectory)
            ? AppPaths.GetDefaultBaseDirectory()
            : BaseDirectory;

        SettingsFilePath = AppPaths.GetSettingsFilePath(EffectiveBaseDirectory);
        this.TimeProvider = TimeProvider ?? new SystemTimeProvider();
        Current = new AppSettings();
    }

    public static SettingsService Global => GlobalInstance ??= new SettingsService();

    public AppSettings Current { get; private set; }

    public event EventHandler<AppSettings>? SettingsChanged;

    public void Load()
    {
        if (!File.Exists(SettingsFilePath))
        {
            Current = new AppSettings();
            return;
        }

        try
        {
            string JsonText = File.ReadAllText(SettingsFilePath);
            AppSettings? LoadedSettings = JsonSerializer.Deserialize<AppSettings>(JsonText);
            Current = LoadedSettings ?? new AppSettings();
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFilePath)!);

        string JsonText = JsonSerializer.Serialize(Current, JsonOptions);
        File.WriteAllText(SettingsFilePath, JsonText);
        SettingsChanged?.Invoke(this, Current);
    }

    public void Update(Action<AppSettings> Mutator)
    {
        Mutator(Current);
        Save();
    }

    public void ResetToDefaults()
    {
        Current = new AppSettings();
        Save();
    }

    public void AddRecentGame(string ConsoleName, string GameName)
    {
        if (string.IsNullOrWhiteSpace(ConsoleName)
            || string.IsNullOrWhiteSpace(GameName)
            || PresenceFormatter.IsHomeGame(GameName))
        {
            return;
        }

        string TrimmedConsole = ConsoleName.Trim();
        string TrimmedGame = GameName.Trim();

        Current.RecentGames.RemoveAll(Item =>
            string.Equals(Item.ConsoleName, TrimmedConsole, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Item.GameName, TrimmedGame, StringComparison.OrdinalIgnoreCase));

        Current.RecentGames.Insert(0, new RecentGameModel
        {
            ConsoleName = TrimmedConsole,
            GameName = TrimmedGame,
            LastPlayedUtc = new DateTimeOffset(TimeProvider.UtcNow, TimeSpan.Zero)
        });

        while (Current.RecentGames.Count > AppConstants.MaxRecentGames)
        {
            Current.RecentGames.RemoveAt(Current.RecentGames.Count - 1);
        }

        Save();
    }

    public void SaveLastStatus(PresenceRequest Request)
    {
        if (!Current.RememberLastStatus)
        {
            return;
        }

        Current.LastConsole = Request.ConsoleName;
        Current.LastGame = Request.GameName;
        Current.LastDetails = Request.Details;
        Current.LastState = Request.State;

        Save();
    }
}
