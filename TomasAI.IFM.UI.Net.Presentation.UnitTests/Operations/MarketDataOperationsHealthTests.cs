using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using TomasAI.IFM.UI.Net.Models.MarketData;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.ViewModels.App;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Operations;

public sealed class MarketDataOperationsHealthTests
{
    static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Backend_json_contract_is_read_into_ui_owned_value_records()
    {
        var backend = new TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.MarketDataOperationsHealthReadModel
        {
            ObservedOnUtc = Now, OverallStatus = "Red",
            Stages = [new() { Stage = "MarketOutlookComposition", Status = "Red", Pending = 12,
                P99Latency = TimeSpan.FromMilliseconds(500), Reason = "Injected stall" }],
            Datasets = [new() { Dataset = "GLBX.MDP3", ProcessId = 1234, ForcedTermination = true }]
        };
        using var client = new HttpClient(new Reply(_ => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(backend) }));
        using var query = new MarketDataOperationsHealthQueryService(client, new Uri("http://localhost/health"), timeProvider: new FixedTime());
        var result = await query.GetAsync();
        Assert.True(result.IsSuccess);
        Assert.Equal(12, Assert.Single(result.Value!.Stages).Pending);
        Assert.Equal(TimeSpan.FromMilliseconds(500), result.Value.Stages[0].P99Latency);
        Assert.Equal(1234, Assert.Single(result.Value.Datasets).ProcessId);
        Assert.True(result.Value.Datasets[0].ForcedTermination);
    }

    [Theory]
    [InlineData("Green")]
    [InlineData("Yellow")]
    [InlineData("Orange")]
    [InlineData("Red")]
    [InlineData("Inactive")]
    public async Task Current_bounded_health_response_is_accepted(string status)
    {
        var result = await Read(new() { ObservedOnUtc = Now, OverallStatus = status });
        Assert.True(result.IsSuccess);
        Assert.Equal(status, result.Value!.OverallStatus);
    }

    [Theory]
    [InlineData(-16)]
    [InlineData(31)]
    public async Task Stale_or_future_clock_observation_cannot_display_green(int offset)
    {
        var result = await Read(new() { ObservedOnUtc = Now.AddSeconds(offset), OverallStatus = "Green" });
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Unsupported_schema_null_rows_and_unbounded_dimensions_are_rejected()
    {
        Assert.False((await Read(new() { ObservedOnUtc = Now, SchemaVersion = 2 })).IsSuccess);
        Assert.False((await Read(new() { ObservedOnUtc = Now, Stages = [null!] })).IsSuccess);
        Assert.False((await Read(new() { ObservedOnUtc = Now,
            Datasets = Enumerable.Range(0, 17).Select(_ => new MarketDataDatasetHealthSnapshot()).ToArray() })).IsSuccess);
        Assert.False((await Read(new() { ObservedOnUtc = Now, OverallStatus = "InventedHealthy" })).IsSuccess);
    }

    [Fact]
    public async Task Unconfigured_endpoint_makes_no_network_request_and_caller_cancellation_propagates()
    {
        using var client = new HttpClient(new Reply(_ => throw new InvalidOperationException("Must not send")));
        using var service = new MarketDataOperationsHealthQueryService(client, null);
        Assert.False((await service.GetAsync()).IsSuccess);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(cancellation.Token));
    }

    [Fact]
    public async Task Network_failure_clears_previous_green_view_model_snapshot()
    {
        var query = Substitute.For<IMarketDataOperationsHealthQueryService>();
        query.GetAsync(Arg.Any<CancellationToken>()).Returns(
            UiOperationResult<MarketDataOperationsHealthSnapshot>.Success(new() { ObservedOnUtc = Now, OverallStatus = "Green" }),
            UiOperationResult<MarketDataOperationsHealthSnapshot>.Failure(9610, "offline"));
        await using var model = new MarketDataOperationsHealthViewModel(query);
        await model.RefreshAsync();
        Assert.Equal("Green", model.Status);
        await model.RefreshAsync();
        Assert.Null(model.Snapshot);
        Assert.Equal("Unavailable", model.Status);
        Assert.Empty(model.Stages);
        Assert.Contains("offline", model.Summary);
    }

    [Fact]
    public async Task Simultaneous_refreshes_share_one_read_only_request()
    {
        var result = new TaskCompletionSource<UiOperationResult<MarketDataOperationsHealthSnapshot>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var query = Substitute.For<IMarketDataOperationsHealthQueryService>();
        query.GetAsync(Arg.Any<CancellationToken>()).Returns(result.Task);
        await using var model = new MarketDataOperationsHealthViewModel(query);
        var first = model.RefreshAsync();
        var second = model.RefreshAsync();
        result.SetResult(UiOperationResult<MarketDataOperationsHealthSnapshot>.Success(new() { ObservedOnUtc = Now }));
        await Task.WhenAll(first, second);
        await query.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    static async Task<UiOperationResult<MarketDataOperationsHealthSnapshot>> Read(MarketDataOperationsHealthSnapshot snapshot)
    {
        using var client = new HttpClient(new Reply(_ => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(snapshot) }));
        using var query = new MarketDataOperationsHealthQueryService(client, new Uri("http://localhost/health"), timeProvider: new FixedTime());
        return await query.GetAsync();
    }

    sealed class Reply(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(reply(request));
    }
    sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
