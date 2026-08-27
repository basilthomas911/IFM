using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Recovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesVwapSignal;

/// <summary>Verifies private historical-trade delivery to the VWAP command stream.</summary>
public sealed class FuturesVwapHistoricalReplayPublisherTests
{
    /// <summary>Proves bounded historical batches remain private commands and finish each known stream.</summary>
    [Fact]
    public async Task PublishAsync_MapsTradesAndCompletesPreviouslySeenEntity()
    {
        var actorService = Substitute.For<IActorService>();
        var calendar = Substitute.For<IMarketSessionCalendar>();
        var valueDate = new DateOnly(2026, 8, 26);
        var session = new MarketSessionBounds(
            valueDate,
            new DateTimeOffset(2026, 8, 25, 22, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 26, 21, 0, 0, TimeSpan.Zero));
        calendar.GetSession(valueDate).Returns(session);
        var commands = new List<RecoverFuturesVwapSignalCommand>();
        actorService.RequestAsync<RecoverFuturesVwapSignalCommand, FuturesVwapSignalEntityId>(
                Arg.Do<RecoverFuturesVwapSignalCommand>(commands.Add))
            .Returns(new ValueTask<ServiceResult<Guid>>(new ServiceOk<Guid>(Guid.NewGuid())));
        var publisher = new FuturesVwapHistoricalReplayPublisher(actorService, calendar);
        var attemptId = Guid.NewGuid();
        var trade = new NormalizedHistoricalTrade
        {
            ContractId = "ESU26",
            ValueDate = valueDate,
            Price = 6500.25m,
            Size = 3,
            EventTimestampUtc = session.StartUtc.AddMinutes(1),
            SourceSequence = 42,
            Action = NormalizedTradeAction.New,
            Side = NormalizedTradeSide.Unspecified,
            Conditions = NormalizedTradeConditionFlags.None,
            ProviderInstrumentId = "12345"
        };

        await publisher.PublishAsync(Batch(attemptId, 0, [trade], false), CancellationToken.None);
        await publisher.PublishAsync(Batch(attemptId, 1, [], true), CancellationToken.None);

        commands.Should().HaveCount(2);
        commands[0].IsFirstBatch.Should().BeTrue();
        commands[0].IsFinalBatch.Should().BeFalse();
        commands[0].BatchOrdinal.Should().Be(0);
        commands[0].Trades.Should().ContainSingle();
        commands[0].Trades[0].Conditions.Should().HaveFlag(FuturesVwapTradeConditionFlags.Replay);
        commands[0].Trades[0].StreamEpochId.Should().Be(attemptId);
        commands[0].Trades[0].SessionStartUtc.Should().Be(session.StartUtc);
        commands[1].IsFinalBatch.Should().BeTrue();
        commands[1].BatchOrdinal.Should().Be(1);
        commands[1].Trades.Should().BeEmpty();
    }

    static NormalizedHistoricalBatch Batch(
        Guid attemptId,
        long ordinal,
        IReadOnlyList<NormalizedHistoricalTrade> trades,
        bool isFinal) => new(
            attemptId,
            "provider-file",
            ordinal,
            ordinal.ToString(),
            [],
            trades,
            "sha256",
            isFinal);
}
