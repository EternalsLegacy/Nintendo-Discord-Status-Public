using System;
using System.IO;

namespace NintendoDiscordStatus.Tests.Fakes;

public sealed class TempTestDirectory : IDisposable
{
    public TempTestDirectory()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), $"NDS_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, true);
        }
    }
}
