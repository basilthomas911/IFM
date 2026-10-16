using System.Net;
using System.Net.Http.Json;
using TomasAI.IFM.Application.ScheduledTask.Shared;
using TomasAI.IFM.Domain.SystemAdmin.Shared.MarketClose;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost.PortableTests;

public sealed class MarketCloseClientTests
{
    [Fact]
    public async Task Close_sends_empty_post_and_returns_backend_completion()
    {
        var expected = new MarketCloseResult(new(2026, 10, 15), Guid.NewGuid(), 12, false);
        using var handler = new Handler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/market-data/close", request.RequestUri!.AbsolutePath);
            Assert.True(request.Content is null || request.Content.Headers.ContentLength is null or 0);
            Assert.Empty(request.RequestUri.Query);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(expected) };
        });
        using var http = new HttpClient(handler) { BaseAddress = new("http://localhost:22543/") };
        Assert.Equal(expected, await new MarketCloseClient(http).CloseMarketAsync());
    }

    [Fact]
    public async Task Backend_failure_cannot_be_reported_as_successful_close()
    {
        using var handler = new Handler(_ => new(HttpStatusCode.Conflict) { Content = new StringContent("session is open") });
        using var http = new HttpClient(handler) { BaseAddress = new("http://localhost:22543/") };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new MarketCloseClient(http).CloseMarketAsync());
        Assert.Contains("session is open", error.Message);
    }

    sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
