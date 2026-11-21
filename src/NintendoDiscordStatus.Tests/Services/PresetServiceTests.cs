using System.IO;
using System.Text.Json;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Models;
using NintendoDiscordStatus.Core.Services;
using NintendoDiscordStatus.Tests.Fakes;
using Xunit;

namespace NintendoDiscordStatus.Tests.Services;

public class PresetServiceTests
{
    [Fact]
    public void Load_HandlesMissingNullCorruptAndLegacyPresetsWithoutName()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        PresetService DefaultService = new PresetService();
        Assert.Empty(DefaultService.Presets);

        PresetService Service = new PresetService(TempDir.DirectoryPath);
        Service.Load();
        Assert.Empty(Service.Presets);

        string PathToPresets = AppPaths.GetPresetsFilePath(TempDir.DirectoryPath);

        File.WriteAllText(PathToPresets, "null");
        Service.Load();
        Assert.Empty(Service.Presets);

        File.WriteAllText(PathToPresets, "[broken");
        Service.Load();
        Assert.Empty(Service.Presets);

        PresetModel[] LegacyPresets =
        {
            new PresetModel
            {
                PresetName = "",
                ConsoleName = "Nintendo Switch",
                GameName = "Splatoon 3",
                Details = "Ranked"
            },
            new PresetModel
            {
                PresetName = "Raid Night",
                ConsoleName = "Nintendo Switch",
                GameName = "Pokemon Scarlet",
                Details = "Tera Raid",
                State = "Multiplayer"
            }
        };

        File.WriteAllText(PathToPresets, JsonSerializer.Serialize(LegacyPresets));
        Service.Load();

        Assert.Equal(2, Service.Presets.Count);
        Assert.Equal("Nintendo Switch - Splatoon 3", Service.Presets[0].PresetName);
        Assert.Equal("Raid Night", Service.Presets[1].PresetName);
        Assert.Equal("Tera Raid", Service.Presets[1].Details);

        const string VersionOneJson = """
            [
              {
                "PresetName": "Nintendo Switch - Super Mario Galaxy 2",
                "Console": " Nintendo Switch ",
                "Game": " Super Mario Galaxy 2 ",
                "Details": " Searching Green Stars ",
                "State": ""
              },
              {
                "PresetName": "Nintendo Wii U - Mario Kart 8",
                "ConsoleName": "",
                "GameName": ""
              },
              {
                "PresetName": null,
                "Console": "Nintendo Wii",
                "Game": "   ",
                "Details": null,
                "State": null
              },
              {
                "PresetName": "Custom Only",
                "Console": null,
                "Game": null
              }
            ]
            """;

        File.WriteAllText(PathToPresets, VersionOneJson);
        Service.Load();

        Assert.Equal(4, Service.Presets.Count);
        Assert.Equal("Nintendo Switch", Service.Presets[0].ConsoleName);
        Assert.Equal("Super Mario Galaxy 2", Service.Presets[0].GameName);
        Assert.Equal("Searching Green Stars", Service.Presets[0].Details);
        Assert.Equal("Nintendo Wii U", Service.Presets[1].ConsoleName);
        Assert.Equal("Mario Kart 8", Service.Presets[1].GameName);
        Assert.Equal("Nintendo Wii", Service.Presets[2].PresetName);
        Assert.Equal("Nintendo Wii", Service.Presets[2].ConsoleName);
        Assert.Equal(string.Empty, Service.Presets[2].GameName);
        Assert.Equal("Custom Only", Service.Presets[3].PresetName);
        Assert.Equal(string.Empty, Service.Presets[3].ConsoleName);
    }

    [Fact]
    public void SaveOrUpdateAndDeletePreset_ModifyCollectionAndPersist()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        string NestedDir = Path.Combine(TempDir.DirectoryPath, "PresetsFolder");
        PresetService Service = new PresetService(NestedDir);

        Assert.False(Service.SaveOrUpdatePreset(new PresetModel { PresetName = "   " }));
        Assert.False(Service.DeletePreset("NonExistent"));

        Assert.True(Service.SaveOrUpdatePreset(new PresetModel
        {
            PresetName = "Mario Night",
            ConsoleName = "Nintendo Switch",
            GameName = "Mario Kart 8 Deluxe",
            Details = "150cc"
        }));

        Assert.True(Service.SaveOrUpdatePreset(new PresetModel
        {
            PresetName = "  mario night ",
            ConsoleName = "Nintendo Switch",
            GameName = "Mario Kart 8 Deluxe",
            Details = "200cc"
        }));

        Assert.Single(Service.Presets);
        Assert.Equal("mario night", Service.Presets[0].PresetName);
        Assert.Equal("200cc", Service.Presets[0].Details);

        Assert.True(Service.SaveOrUpdatePreset(new PresetModel
        {
            PresetName = "Zelda Session",
            ConsoleName = "Nintendo Switch",
            GameName = "The Legend of Zelda: Tears of the Kingdom",
            Details = "Exploring Hyrule"
        }));

        PresetService Reloaded = new PresetService(NestedDir);
        Reloaded.Load();
        Assert.Equal(2, Reloaded.Presets.Count);
        Assert.Equal("200cc", Reloaded.Presets[0].Details);
        Assert.Equal("Zelda Session", Reloaded.Presets[1].PresetName);

        Assert.False(Service.DeletePreset("Different Name"));
        Assert.True(Service.DeletePreset("MARIO NIGHT"));
        Assert.True(Service.DeletePreset("zelda session"));
        Assert.Empty(Service.Presets);
        Assert.False(Service.DeletePreset("MARIO NIGHT"));
    }
}
