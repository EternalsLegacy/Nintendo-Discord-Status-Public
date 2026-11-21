using System;
using System.Collections.Generic;
using System.IO;

namespace NintendoDiscordStatus.Core.Constants;

public static class AppPaths
{
    public static string GetDefaultBaseDirectory() => AppDomain.CurrentDomain.BaseDirectory;

    public static string GetSettingsFilePath(string BaseDirectory) =>
        Path.Combine(BaseDirectory, AppConstants.SettingsFileName);

    public static string GetGameLibraryFilePath(string BaseDirectory) =>
        Path.Combine(BaseDirectory, AppConstants.GameLibraryFileName);

    public static string GetCustomGamesFilePath(string BaseDirectory) =>
        Path.Combine(BaseDirectory, AppConstants.CustomGamesFileName);

    public static string GetPresetsFilePath(string BaseDirectory) =>
        Path.Combine(BaseDirectory, AppConstants.PresetsFileName);

    public static string GetCrashLogFilePath(string BaseDirectory) =>
        Path.Combine(BaseDirectory, AppConstants.CrashLogFileName);

    public static IReadOnlyList<string> GetCandidateImagePaths(string BaseDirectory, string CurrentDirectory, string ImageFileName)
    {
        return new[]
        {
            Path.Combine(BaseDirectory, AppConstants.ImagesFolderName, ImageFileName),
            Path.Combine(BaseDirectory, "..", "..", "..", "..", AppConstants.ImagesFolderName, ImageFileName),
            Path.Combine(CurrentDirectory, AppConstants.ImagesFolderName, ImageFileName)
        };
    }
}
