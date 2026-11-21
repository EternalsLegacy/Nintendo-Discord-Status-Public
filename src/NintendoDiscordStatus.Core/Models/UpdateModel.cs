using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NintendoDiscordStatus.Core.Models
{
    public class GitHubReleaseModel
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public List<GitHubAssetModel> Assets { get; set; } = new List<GitHubAssetModel>();
    }

    public class GitHubAssetModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }

    public class UpdateInfoModel
    {
        public bool IsUpdateAvailable { get; set; }
        public Version? LatestVersion { get; set; }
        public string TagName { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string AssetFileName { get; set; } = string.Empty;
        public string HtmlUrl { get; set; } = string.Empty;
    }

    public enum UpdateExecutionStatus
    {
        SuccessLaunchedUpdater,
        NoDownloadUrl,
        SevenZipMissing,
        DownloadFailed,
        ExtractionFailed,
        ExecutableNotFound,
        UnexpectedError
    }

    public class UpdateExecutionResult
    {
        public UpdateExecutionStatus Status { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string ExtractedFilePath { get; set; } = string.Empty;
    }
}
