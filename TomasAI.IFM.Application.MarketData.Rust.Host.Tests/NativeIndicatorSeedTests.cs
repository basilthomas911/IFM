using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.Rust.Host.Seeding;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
using Xunit;

namespace TomasAI.IFM.Application.MarketData.Rust.Host.Tests;

/// <summary>Qualifies original minute coverage and the no-purchase ceiling in the actual native Host seed implementation.</summary>
public sealed class NativeIndicatorSeedTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 1, 22, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(TimeFrameType.FifteenMinutes)]
    [InlineData(TimeFrameType.OneHour)]
    [InlineData(TimeFrameType.FourHours)]
    public async Task Original_minutes_reconstruct_all_48_calendar_intervals(TimeFrameType frame)
    {
        var provider = Provider(0m);
        provider.OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new Reader(Minutes(call.Arg<HistoricalProviderRequest>())));
        var bars = await Create(provider).ReadAsync("ES20261218", frame, default);
        Assert.Equal(48, bars.Length);
        Assert.All(bars, bar => { Assert.Equal(frame, bar.TimeFrame); Assert.True(bar.IsComplete && bar.IsValid); Assert.Equal(101m, bar.High); Assert.Equal(99m, bar.Low); Assert.Equal(100m, bar.Close); });
        Assert.Equal(RsiHistoricalSeedWindowModel.Create(frame, 48, Now, new CmeFuturesMarketSessionCalendar()).Select(w => w.EndUtc), bars.Select(b => b.IntervalEndUtc));
    }

    [Fact]
    public async Task Any_nonzero_estimate_blocks_the_download()
    {
        var provider = Provider(0.01m);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Create(provider).ReadAsync("ES20261218", TimeFrameType.OneHour, default));
        Assert.Contains("no data was purchased", error.Message);
        await provider.DidNotReceive().OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_original_minute_cannot_qualify_a_complete_seed()
    {
        var provider = Provider(0m);
        provider.OpenRangeAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => new Reader(Minutes(call.Arg<HistoricalProviderRequest>()).Skip(1).ToArray()));
        Assert.Empty(await Create(provider).ReadAsync("ES20261218", TimeFrameType.OneHour, default));
    }

    static IMarketDataHistoricalProvider Provider(decimal cost)
    {
        var provider = Substitute.For<IMarketDataHistoricalProvider>();
        provider.EstimateAsync(Arg.Any<HistoricalProviderRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HistoricalProviderEstimate(cost, 1000, 100, Now));
        return provider;
    }

    static NativeFuturesSeedReplay Create(IMarketDataHistoricalProvider provider) => new(null!, new()
    {
        FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3") with { DataSource = FeedDataSourceMode.DatabentoLive },
        Contracts = [new() { DomainContractId = "ES20261218", ProviderContractName = "ESZ6", AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3" }]
    }, new CmeFuturesMarketSessionCalendar(), new Clock(), historical: provider);

    static HistoricalProviderRecord[] Minutes(HistoricalProviderRequest request) => Enumerable.Range(0, (int)(request.EndUtc - request.StartUtc).TotalMinutes)
        .Select(i => new HistoricalProviderRecord { Kind = HistoricalProviderRecordKind.Ohlcv, Symbol = "ESZ6", InstrumentId = "42", EventTimestampUtc = request.StartUtc.AddMinutes(i), Open = 100, High = 101, Low = 99, CloseOrPrice = 100, VolumeOrSize = 10 }).ToArray();

    sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    sealed class Reader(HistoricalProviderRecord[] records) : IHistoricalRecordReader
    {
        bool read;
        public ValueTask<HistoricalProviderRecordBatch?> ReadNextAsync(CancellationToken cancellationToken)
        {
            if (read) return ValueTask.FromResult<HistoricalProviderRecordBatch?>(null);
            read = true;
            return ValueTask.FromResult<HistoricalProviderRecordBatch?>(new(records, 0, "fixture", true));
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
