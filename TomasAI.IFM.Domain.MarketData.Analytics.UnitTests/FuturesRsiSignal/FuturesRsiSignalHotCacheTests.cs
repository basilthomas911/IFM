using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesRsiSignal;

public sealed class FuturesRsiSignalHotCacheTests
{
    [Fact]
    public async Task ExecuteAsync_ActiveFreshTrade_SendsPriceAndProvenanceToRsiCommand()
    {
        const string contractId = "ESU26";
        var valueDate = new DateOnly(2026, 8, 14);
        var entityId = new FuturesRsiSignalEntityId(
            contractId,
            valueDate,
            TimeFrameType.OneMinute,
            13);
        var started = new FuturesRsiSignalStartedEvent
        {
            EntityId = entityId,
            ValueDate = valueDate
        };
        var stopped = new FuturesRsiSignalStoppedEvent
        {
            EntityId = entityId,
            ValueDate = valueDate
        };
        var eventTimestamp = new DateTimeOffset(2026, 8, 14, 15, 30, 45, TimeSpan.Zero);
        var snapshot = new FuturesMarketPriceSnapshot(
            contractId,
            101,
            7,
            AssetTypeId.Futures,
            valueDate,
            null,
            new FuturesMarketTradeSnapshot(6425.25m, 4, 9001, eventTimestamp, eventTimestamp));
        var marketDataApi = Substitute.For<IMarketDataApi>();
        marketDataApi.IsTickDataStreamActive(contractId).Returns(true);
        marketDataApi.TryGetLastTickPrice(contractId, out Arg.Any<FuturesMarketPriceSnapshot>())
            .Returns(callInfo =>
            {
                callInfo[1] = snapshot;
                return true;
            });
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandApi = Substitute.For<IActorMarketDataAnalyticsCommandApi>();
        commandApi.GenerateFuturesRsiSignalAsync(
                Arg.Any<FuturesRsiSignalId>(),
                Arg.Any<decimal>(),
                Arg.Any<long>(),
                Arg.Any<DateTime>())
            .Returns(_ =>
            {
                sent.TrySetResult();
                return new ServiceOk<GuidResult>(new GuidResult(Guid.NewGuid()));
            });

        try
        {
            (await started.ExecuteAsync(
                Substitute.For<IEventActorContext>(),
                commandApi,
                marketDataApi,
                Substitute.For<IStatusConsoleWriter>(),
                Substitute.For<ILogger>())).Should().BeTrue();
            await sent.Task.WaitAsync(TimeSpan.FromSeconds(1));

            await commandApi.Received(1).GenerateFuturesRsiSignalAsync(
                Arg.Is<FuturesRsiSignalId>(id =>
                    id.ContractId == contractId
                    && id.TimePeriod == TimeFrameType.OneMinute
                    && id.Timestamp == TimeOnly.FromDateTime(eventTimestamp.UtcDateTime)),
                6425.25m,
                9001,
                eventTimestamp.UtcDateTime);
        }
        finally
        {
            await stopped.StopTimerAsync();
        }
    }
}
