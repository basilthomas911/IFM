using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

[Collection(MarketOutlookHotCacheTestCollection.Name)]
public sealed class MarketOutlookSnapshotHydratorTests
{
    [Fact]
    public async Task HydrateAsync_LoadsPersistedSignalsAndPublishesOneBaseline()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var id = new MarketOutlookEntityId("ESZ26", new DateOnly(2026, 9, 1));
        var db = Substitute.For<IMarketDataDbContext>();
        var factory = Substitute.For<IDbContextFactory>();
        factory.MarketDataDb.Returns(db);
        var securities = Substitute.For<ISecuritiesDbContext>();
        factory.SecuritiesDb.Returns(securities);
        var vxContract = new FuturesContractV2ReadModel
        {
            ContractId = "VXU26",
            Symbol = "VX",
            LastTradeDate = new DateOnly(2026, 9, 16),
            CurrentlyTraded = true
        };
        securities.GetCurrentlyTradedFuturesContractsAsync("VX", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ICollection<FuturesContractV2ReadModel>>([vxContract]));
        db.GetLastVixFuturesEodDataAsync(vxContract.ContractId, id.ValueDate)
            .Returns(new VixFuturesEodDataReadModel(
                vxContract.ContractId, id.ValueDate, 17m, 19m, 16m, 18.5m, 1_000));
        var rsi = new FuturesRsiSignalReadModel
        {
            ContractId = id.ContractId,
            ValueDate = id.ValueDate,
            TimePeriod = TimeFrameType.FifteenSeconds,
            PeriodLength = 14,
            Timestamp = new TimeOnly(13, 30),
            RSI = 57d,
            IsWarm = true
        };
        var tradeSignal = new FuturesTradeSignalV2ReadModel
        {
            ContractId = id.ContractId,
            ValueDate = id.ValueDate,
            TimePeriod = TimeFrameType.FifteenSeconds,
            FuturesPrice = 6_125d
        };
        db.GetLastFuturesRsiSignalAsync(
                id.ContractId, id.ValueDate, TimeFrameType.FifteenSeconds, 14,
                Arg.Any<CancellationToken>())
            .Returns(rsi);
        db.GetLastFuturesTradeSignalAsync(
                id.ContractId, id.ValueDate, Arg.Any<CancellationToken>())
            .Returns(tradeSignal);
        var hydrator = new MarketOutlookSnapshotHydrator(
            factory,
            runtime.Channel,
            runtime.Processor,
            runtime.Cache,
            Substitute.For<ILogger<MarketOutlookSnapshotHydrator>>());

        var result = await hydrator.HydrateAsync(id);

        result.Should().NotBeNull();
        result!.FuturesRsiSignal.Should().BeSameAs(rsi);
        result.FuturesTradeSignal.Should().NotBeNull();
        result.FuturesTradeSignal!.FuturesPrice.Should().Be(tradeSignal.FuturesPrice);
        result.VixFuturesPrice.Should().Be(18.5m);
        result.FuturesEodData.PriceVolatility.Should().Be(PriceVolatilityType.Rising);
        result.RefreshTrigger.Should().Be(MarketOutlookRefreshTrigger.PersistedBaseline);
        runtime.Processor.GetMetrics().Updates[MarketOutlookUpdateKind.Hydration].Applied
            .Should().Be(1);
    }

    [Fact]
    public async Task HydrateAsync_ReplacesExistingValueAtStartup()
    {
        await using var runtime = await MarketOutlookProcessorTestRuntime.StartAsync();
        var id = new MarketOutlookEntityId("ESZ26", new DateOnly(2026, 9, 1));
        var db = Substitute.For<IMarketDataDbContext>();
        var factory = Substitute.For<IDbContextFactory>();
        factory.MarketDataDb.Returns(db);
        var securities = Substitute.For<ISecuritiesDbContext>();
        factory.SecuritiesDb.Returns(securities);
        securities.GetCurrentlyTradedFuturesContractsAsync("VX", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ICollection<FuturesContractV2ReadModel>>([]));
        db.GetLastFuturesTradeSignalAsync(
                id.ContractId, id.ValueDate, Arg.Any<CancellationToken>())
            .Returns(new FuturesTradeSignalV2ReadModel
            {
                ContractId = id.ContractId,
                ValueDate = id.ValueDate,
                FuturesPrice = 6_100d
            });
        runtime.Channel.Submit(new TradeSignalMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(),
            EntityId = id,
            ReceivedAtUtc = DateTime.UtcNow,
            MarketDataAsOfUtc = DateTime.UtcNow,
            Signal = new FuturesTradeSignalV2ReadModel
            {
                ContractId = id.ContractId,
                ValueDate = id.ValueDate,
                FuturesPrice = 6_200d
            }
        });
        await runtime.DrainAsync();
        var hydrator = new MarketOutlookSnapshotHydrator(
            factory,
            runtime.Channel,
            runtime.Processor,
            runtime.Cache,
            Substitute.For<ILogger<MarketOutlookSnapshotHydrator>>());

        var result = await hydrator.HydrateAsync(id);

        result!.FuturesTradeSignal!.FuturesPrice.Should().Be(6_100d);
    }
}
