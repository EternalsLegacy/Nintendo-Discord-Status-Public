using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;

namespace NintendoDiscordStatus.Core.Services;

public class PresetService
{
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
            List<PresetModel>? Loaded = JsonSerializer.Deserialize<List<PresetModel>>(JsonText);
            if (Loaded == null)
            {
                return;
            }

            foreach (PresetModel Item in Loaded)
            {
                if (string.IsNullOrWhiteSpace(Item.PresetName))
                {
                    Item.PresetName = $"{Item.ConsoleName} - {Item.GameName}".Trim(' ', '-');
                }

                Presets.Add(Item);
            }
        }
        catch
        {
            Presets.Clear();
        }
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
