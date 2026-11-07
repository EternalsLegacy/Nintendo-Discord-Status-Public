#region Usings

using System;

#endregion

namespace NintendoDiscordStatus.Models;

#region Public Classes

public class UpdateModel
{
    #region Public Properties

    public string TagName { get; set; } = string.Empty;
    public string ReleaseName { get; set; } = string.Empty;
    public string Changelog { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public bool IsZipArchive => AssetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    public Version? NewVersion { get; set; }

    #endregion
}

#endregion
