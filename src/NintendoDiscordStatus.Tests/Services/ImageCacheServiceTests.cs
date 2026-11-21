using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NintendoDiscordStatus.Core.Constants;
using NintendoDiscordStatus.Core.Services;
using NintendoDiscordStatus.Tests.Fakes;
using Xunit;

namespace NintendoDiscordStatus.Tests.Services;

public class ImageCacheServiceTests
{
    [Fact]
    public async Task GetImageBytesAsync_HandlesInvalidEmptyAndCancelledRequests()
    {
        ImageCacheService DefaultService = new ImageCacheService();
        Assert.Equal(0, DefaultService.CachedItemCount);

        Assert.Null(await DefaultService.GetImageBytesAsync(null));
        Assert.Null(await DefaultService.GetImageBytesAsync("   "));

        using CancellationTokenSource Cts = new CancellationTokenSource();
        Cts.Cancel();
        Assert.Null(await DefaultService.GetImageBytesAsync("zelda.png", Cts.Token));
    }

    [Fact]
    public async Task GetImageBytesAsync_DownloadsOnceAndServesFromMemoryCache()
    {
        byte[] PngPayload = { 137, 80, 78, 71 };
        int NetworkCalls = 0;

        FakeHttpMessageHandler Handler = new FakeHttpMessageHandler(Request =>
        {
            NetworkCalls++;
            string Url = Request.RequestUri?.ToString() ?? string.Empty;

            if (Url.EndsWith("missing.png"))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (Url.EndsWith("empty.png"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Array.Empty<byte>())
                };
            }

            if (Url.EndsWith("throws.png"))
            {
                throw new HttpRequestException("Network failure");
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(PngPayload)
            };
        });

        using TempTestDirectory TempDir = new TempTestDirectory();
        using HttpClient Client = new HttpClient(Handler);
        ImageCacheService Cache = new ImageCacheService(Client, TempDir.DirectoryPath, TempDir.DirectoryPath);

        Assert.Null(await Cache.GetImageBytesAsync("missing.png"));
        Assert.Null(await Cache.GetImageBytesAsync("empty.png"));
        Assert.Null(await Cache.GetImageBytesAsync("throws.png"));
        Assert.Equal(0, Cache.CachedItemCount);

        byte[]? FirstResult = await Cache.GetImageBytesAsync("botw");
        byte[]? SecondResult = await Cache.GetImageBytesAsync("BOTW.PNG");

        Assert.Equal(PngPayload, FirstResult);
        Assert.Equal(PngPayload, SecondResult);
        Assert.Equal(AppConstants.GitHubImagesBaseUrl + "botw.png", Handler.RequestedUris[3]?.ToString());
        Assert.Equal(4, NetworkCalls);

        byte[]? DirectUrlBytes = await Cache.GetImageBytesAsync("https://cdn.example.com/custom.png");
        Assert.Equal(PngPayload, DirectUrlBytes);
        Assert.Equal(2, Cache.CachedItemCount);

        Cache.ClearCache();
        Assert.Equal(0, Cache.CachedItemCount);
    }

    [Fact]
    public async Task GetImageBytesAsync_ReadsDirectAndCandidateLocalFilesAndHandlesIoFailure()
    {
        using TempTestDirectory TempDir = new TempTestDirectory();
        string ImagesDir = Path.Combine(TempDir.DirectoryPath, AppConstants.ImagesFolderName);
        Directory.CreateDirectory(ImagesDir);

        byte[] LocalBytes = { 1, 2, 3, 4, 5 };
        string DirectFile = Path.Combine(TempDir.DirectoryPath, "direct.png");
        string CandidateFile = Path.Combine(ImagesDir, "candidate.png");
        string LockedFile = Path.Combine(TempDir.DirectoryPath, "locked.png");

        File.WriteAllBytes(DirectFile, LocalBytes);
        File.WriteAllBytes(CandidateFile, LocalBytes);
        File.WriteAllBytes(LockedFile, LocalBytes);

        FakeHttpMessageHandler FailIfNetworkCalled = new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("HTTP should not be called for local images."));

        using HttpClient Client = new HttpClient(FailIfNetworkCalled);
        ImageCacheService Cache = new ImageCacheService(Client, TempDir.DirectoryPath, TempDir.DirectoryPath);

        Assert.Equal(LocalBytes, await Cache.GetImageBytesAsync(DirectFile));
        Assert.Equal(LocalBytes, await Cache.GetImageBytesAsync("candidate.png"));
        Assert.Empty(FailIfNetworkCalled.RequestedUris);

        using FileStream LockStream = new FileStream(LockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Null(await Cache.GetImageBytesAsync(LockedFile));
    }

    [Fact]
    public async Task GetImageBytesAsync_DoesNotCacheWhenTokenCancelledDuringDownload()
    {
        using CancellationTokenSource Cts = new CancellationTokenSource();

        FakeHttpMessageHandler Handler = new FakeHttpMessageHandler((Request, Token) =>
        {
            Cts.Cancel();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 9, 8, 7 })
            });
        });

        using HttpClient Client = new HttpClient(Handler);
        using TempTestDirectory TempDir = new TempTestDirectory();
        ImageCacheService Cache = new ImageCacheService(Client, TempDir.DirectoryPath, TempDir.DirectoryPath);

        byte[]? Result = await Cache.GetImageBytesAsync("https://cdn.example.com/slow.png", Cts.Token);

        Assert.Null(Result);
        Assert.Equal(0, Cache.CachedItemCount);
    }
}
