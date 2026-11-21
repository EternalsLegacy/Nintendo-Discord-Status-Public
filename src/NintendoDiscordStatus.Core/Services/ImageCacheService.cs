using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Helpers;

namespace NintendoDiscordStatus.Core.Services;

public class ImageCacheService
{
    private readonly ConcurrentDictionary<string, byte[]> ByteCache =
        new ConcurrentDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

    private readonly HttpClient HttpClientInstance;
    private readonly string BaseDirectory;
    private readonly string CurrentDirectory;

    public ImageCacheService(
        HttpClient? HttpClientInstance = null,
        string? BaseDirectory = null,
        string? CurrentDirectory = null)
    {
        this.HttpClientInstance = HttpClientInstance ?? CreateDefaultHttpClient();
        this.BaseDirectory = string.IsNullOrWhiteSpace(BaseDirectory)
            ? AppPaths.GetDefaultBaseDirectory()
            : BaseDirectory;
        this.CurrentDirectory = string.IsNullOrWhiteSpace(CurrentDirectory)
            ? Directory.GetCurrentDirectory()
            : CurrentDirectory;
    }

    public int CachedItemCount => ByteCache.Count;

    public void ClearCache()
    {
        ByteCache.Clear();
    }

    public async Task<byte[]?> GetImageBytesAsync(string? ImageKey, CancellationToken Token = default)
    {
        if (string.IsNullOrWhiteSpace(ImageKey) || Token.IsCancellationRequested)
        {
            return null;
        }

        string NormalizedKey = PresenceFormatter.NormalizeAssetFileName(ImageKey);
        if (ByteCache.TryGetValue(NormalizedKey, out byte[]? CachedBytes))
        {
            return CachedBytes;
        }

        byte[]? LoadedBytes = await ResolveBytesAsync(NormalizedKey, Token);
        if (LoadedBytes == null || LoadedBytes.Length == 0 || Token.IsCancellationRequested)
        {
            return null;
        }

        ByteCache[NormalizedKey] = LoadedBytes;
        return LoadedBytes;
    }

    private async Task<byte[]?> ResolveBytesAsync(string NormalizedKey, CancellationToken Token)
    {
        if (PresenceFormatter.IsHttpUrl(NormalizedKey))
        {
            return await DownloadBytesAsync(NormalizedKey, Token);
        }

        string? LocalPath = ResolveExistingLocalFilePath(NormalizedKey);
        if (!string.IsNullOrEmpty(LocalPath))
        {
            try
            {
                return await File.ReadAllBytesAsync(LocalPath, Token);
            }
            catch
            {
                return null;
            }
        }

        string RemoteUrl = AppConstants.GitHubImagesBaseUrl + NormalizedKey;
        return await DownloadBytesAsync(RemoteUrl, Token);
    }

    private string? ResolveExistingLocalFilePath(string NormalizedKey)
    {
        if (File.Exists(NormalizedKey))
        {
            return NormalizedKey;
        }

        IReadOnlyList<string> Candidates = AppPaths.GetCandidateImagePaths(
            BaseDirectory,
            CurrentDirectory,
            NormalizedKey);

        foreach (string CandidatePath in Candidates)
        {
            if (File.Exists(CandidatePath))
            {
                return CandidatePath;
            }
        }

        return null;
    }

    private async Task<byte[]?> DownloadBytesAsync(string Url, CancellationToken Token)
    {
        try
        {
            using HttpResponseMessage Response = await HttpClientInstance.GetAsync(Url, Token);
            if (!Response.IsSuccessStatusCode)
            {
                return null;
            }

            return await Response.Content.ReadAsByteArrayAsync(Token);
        }
        catch
        {
            return null;
        }
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        return new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(AppConstants.ImageRequestTimeoutSeconds)
        };
    }
}
