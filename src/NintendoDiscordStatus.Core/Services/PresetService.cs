using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;

namespace NintendoDiscordStatus.Core.Services;

public class PresetService
{
    private sealed class PresetJsonEntry
    {
        public string? PresetName { get; set; }
        public string? ConsoleName { get; set; }
        public string? GameName { get; set; }
        public string? Details { get; set; }
        public string? State { get; set; }

        [JsonPropertyName("Console")]
        public string? LegacyConsole { get; set; }

        [JsonPropertyName("Game")]
        public string? LegacyGame { get; set; }
    }
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    private readonly string PresetsFilePath;

    public PresetService(string? BaseDirectory = null)
    {
        string EffectiveBaseDirectory = string.IsNullOrWhiteSpace(BaseDirectory)
            ? AppPaths.GetDefaultBaseDirectory()
            : BaseDirectory;

        PresetsFilePath = AppPaths.GetPresetsFilePath(EffectiveBaseDirectory);
    }

    public ObservableCollection<PresetModel> Presets { get; } = new ObservableCollection<PresetModel>();

    public void Load()
    {
        Presets.Clear();

        if (!File.Exists(PresetsFilePath))
        {
            return;
        }

        try
        {
            string JsonText = File.ReadAllText(PresetsFilePath);
            List<PresetJsonEntry>? Loaded = JsonSerializer.Deserialize<List<PresetJsonEntry>>(JsonText);
            if (Loaded == null)
            {
                return;
            }

            foreach (PresetJsonEntry Entry in Loaded)
            {
                Presets.Add(NormalizeLoadedPreset(Entry));
            }
        }
        catch
        {
            Presets.Clear();
        }
    }

    private static PresetModel NormalizeLoadedPreset(PresetJsonEntry Entry)
    {
        string PresetName = Entry.PresetName?.Trim() ?? string.Empty;
        string ConsoleName = PickFirstNonEmpty(Entry.ConsoleName, Entry.LegacyConsole);
        string GameName = PickFirstNonEmpty(Entry.GameName, Entry.LegacyGame);

        if (string.IsNullOrEmpty(ConsoleName) && string.IsNullOrEmpty(GameName))
        {
            int SeparatorIndex = PresetName.IndexOf(" - ", StringComparison.Ordinal);
            if (SeparatorIndex > 0)
            {
                ConsoleName = PresetName.Substring(0, SeparatorIndex).Trim();
                GameName = PresetName.Substring(SeparatorIndex + 3).Trim();
            }
        }

        if (string.IsNullOrEmpty(PresetName))
        {
            PresetName = $"{ConsoleName} - {GameName}".Trim(' ', '-');
        }

        return new PresetModel
        {
            PresetName = PresetName,
            ConsoleName = ConsoleName,
            GameName = GameName,
            Details = Entry.Details?.Trim() ?? string.Empty,
            State = Entry.State?.Trim() ?? string.Empty
        };
    }

    private static string PickFirstNonEmpty(string? Primary, string? Fallback)
    {
        if (!string.IsNullOrWhiteSpace(Primary))
        {
            return Primary.Trim();
        }

        return string.IsNullOrWhiteSpace(Fallback) ? string.Empty : Fallback.Trim();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PresetsFilePath)!);

        string JsonText = JsonSerializer.Serialize(Presets, JsonOptions);
        File.WriteAllText(PresetsFilePath, JsonText);
    }

    public bool SaveOrUpdatePreset(PresetModel Preset)
    {
        if (string.IsNullOrWhiteSpace(Preset.PresetName))
        {
            return false;
        }

        string NormalizedName = Preset.PresetName.Trim();
        Preset.PresetName = NormalizedName;

        for (int Index = 0; Index < Presets.Count; Index++)
        {
            if (string.Equals(Presets[Index].PresetName, NormalizedName, StringComparison.OrdinalIgnoreCase))
            {
                Presets[Index] = Preset;
                Save();
                return true;
            }
        }

        Presets.Add(Preset);
        Save();
        return true;
    }

    public bool DeletePreset(string PresetName)
    {
        for (int Index = 0; Index < Presets.Count; Index++)
        {
            if (string.Equals(Presets[Index].PresetName, PresetName, StringComparison.OrdinalIgnoreCase))
            {
                Presets.RemoveAt(Index);
                Save();
                return true;
            }
        }

        return false;
    }
}
