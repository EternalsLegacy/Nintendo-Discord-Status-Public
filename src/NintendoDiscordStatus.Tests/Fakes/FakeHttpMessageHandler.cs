using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NintendoDiscordStatus.Tests.Fakes;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> HandlerFunc;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> HandlerFunc)
    {
        this.HandlerFunc = HandlerFunc;
    }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> SyncHandlerFunc)
    {
        HandlerFunc = (Request, Token) =>
        {
            Token.ThrowIfCancellationRequested();
            return Task.FromResult(SyncHandlerFunc(Request));
        };
    }

    public List<Uri?> RequestedUris { get; } = new List<Uri?>();
    public List<HttpRequestMessage> ReceivedRequests { get; } = new List<HttpRequestMessage>();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage Request, CancellationToken Token)
    {
        RequestedUris.Add(Request.RequestUri);
        ReceivedRequests.Add(Request);
        return await HandlerFunc(Request, Token);
    }
}
