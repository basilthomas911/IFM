using FluentAssertions;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.MarketOutlookSnapshot;

public sealed class MarketOutlookSnapshotQueryActorTests(MarketDataAnalyticsTestFixture fixture)
    : IClassFixture<MarketDataAnalyticsTestFixture>
{
    sealed class TestActor(
        IQueryActorContext<MarketOutlookSnapshotQueryActor> context)
        : MarketOutlookSnapshotQueryActor(context)
    {
        internal IQuery Parse(
            IQueryActorContext<MarketOutlookSnapshotQueryActor> context,
            NatsMsg<byte[]> message) => ParseMessage(context, message);

        internal ValueTask Receive(
            IQueryActorContext<MarketOutlookSnapshotQueryActor> context,
            IQuery query) => ReceiveAsync(context, query);
    }

    [Fact]
    public async Task LatestQuery_ReadsDurableSnapshotAndRepliesOnce()
    {
        var scenario = CreateScenario();
        var query = Query();
        var expected = new MarketOutlookReadModel
        {
            ContractId = SampleData.ContractId,
            ValueDate = SampleData.ValueDate,
            UpdatedAtUtc = DateTime.UtcNow,
            FuturesEodData = SampleData.EodData
        };
        scenario.Db.GetMarketOutlookSnapshotAsync(
                query.ContractId, query.ValueDate, Arg.Any<CancellationToken>())
            .Returns(expected);

        await scenario.Actor.Receive(scenario.ReceiveContext, query);

        await scenario.Db.Received(1).GetMarketOutlookSnapshotAsync(
            query.ContractId, query.ValueDate, Arg.Any<CancellationToken>());
        await scenario.ReceiveContext.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetMarketOutlookSnapshotQuery.Verb,
            Arg.Is<ServiceResult<MarketOutlookReadModel>>(result =>
                result.Success && result.Value == expected));
    }

    [Fact]
    public async Task LatestQuery_MissingRowReturnsTypedFailureWithoutPlaceholder()
    {
        var scenario = CreateScenario();
        var query = Query();
        scenario.Db.GetMarketOutlookSnapshotAsync(
                query.ContractId, query.ValueDate, Arg.Any<CancellationToken>())
            .Returns((MarketOutlookReadModel?)null);

        await scenario.Actor.Receive(scenario.ReceiveContext, query);

        await scenario.ReceiveContext.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetMarketOutlookSnapshotQuery.Verb,
            Arg.Is<ServiceResult<MarketOutlookReadModel>>(result =>
                !result.Success
                && result.ErrorCode == GetMarketOutlookSnapshotQuery.ErrorId
                && result.Value == null));
    }

    [Fact]
    public async Task LiveQuery_SkipsSyntheticSnapshotAndReturnsEarlierLiveSnapshot()
    {
        var scenario = CreateScenario(new(RejectSyntheticSnapshots: true));
        var query = Query();
        var synthetic = new MarketOutlookReadModel
        {
            ContractId = query.ContractId,
            ValueDate = query.ValueDate,
            SnapshotSource = MarketOutlookSnapshotSource.Synthetic,
            FuturesEodData = SampleData.EodData
        };
        var earlierCutoff = synthetic.ValueDate.AddDays(-1);
        var live = synthetic with
        {
            ValueDate = earlierCutoff,
            SnapshotSource = MarketOutlookSnapshotSource.DatabentoLive
        };
        scenario.Db.GetMarketOutlookSnapshotAsync(
                query.ContractId, query.ValueDate, Arg.Any<CancellationToken>())
            .Returns(synthetic);
        scenario.Db.GetMarketOutlookSnapshotAsync(
                query.ContractId, earlierCutoff, Arg.Any<CancellationToken>())
            .Returns(live);

        await scenario.Actor.Receive(scenario.ReceiveContext, query);

        await scenario.ReceiveContext.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetMarketOutlookSnapshotQuery.Verb,
            Arg.Is<ServiceResult<MarketOutlookReadModel>>(result =>
                result.Success && result.Value == live));
    }

    [Fact]
    public async Task LiveQuery_ReturnsTypedFailureWhenOnlySyntheticDataExists()
    {
        var scenario = CreateScenario(new(RejectSyntheticSnapshots: true));
        var query = Query();
        var synthetic = new MarketOutlookReadModel
        {
            ContractId = query.ContractId,
            ValueDate = query.ValueDate,
            SnapshotSource = MarketOutlookSnapshotSource.Synthetic,
            FuturesEodData = SampleData.EodData
        };
        scenario.Db.GetMarketOutlookSnapshotAsync(
                query.ContractId, query.ValueDate, Arg.Any<CancellationToken>())
            .Returns(synthetic);
        scenario.Db.GetMarketOutlookSnapshotAsync(
                query.ContractId,
                synthetic.ValueDate.AddDays(-1),
                Arg.Any<CancellationToken>())
            .Returns((MarketOutlookReadModel?)null);

        await scenario.Actor.Receive(scenario.ReceiveContext, query);

        await scenario.ReceiveContext.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetMarketOutlookSnapshotQuery.Verb,
            Arg.Is<ServiceResult<MarketOutlookReadModel>>(result =>
                !result.Success
                && result.ErrorCode == GetMarketOutlookSnapshotQuery.ErrorId
                && result.Value == null));
    }

    [Fact]
    public async Task BollingerHistoryQuery_CalculatesPriorDaysFromRawDatabentoEodAndRepliesOnce()
    {
        var scenario = CreateScenario();
        var query = new GetFuturesBollingerBandHistoryQuery("ES", SampleData.ValueDate, 40)
        {
            Subject = new(
                ActorType.Query,
                GetFuturesBollingerBandHistoryQuery.Actor,
                GetFuturesBollingerBandHistoryQuery.Verb,
                $"ES.{SampleData.ValueDate:yyyyMMdd}.40")
        };
        var identity = MarketSeriesIdentity.ForFuturesSeries(
            new FuturesSeriesId("ES", "calendar-front", "unadjusted", 1));
        var observations = Enumerable.Range(0, 80)
            .Select(index => HistoricalObservation(
                identity,
                query.ValueDate.AddDays(index - 80),
                5_000m + index))
            .ToArray();
        scenario.HistoricalObservations.GetRawEodRangeAsync(
                Arg.Is<MarketSeriesIdentity>(value => value == identity),
                query.ValueDate.AddDays(-365),
                query.ValueDate.AddDays(-1),
                Arg.Any<CancellationToken>())
            .Returns(observations);

        await scenario.Actor.Receive(scenario.ReceiveContext, query);

        await scenario.ReceiveContext.Received(1).ReplyAsync(
            query.Subject.ThreadId,
            GetFuturesBollingerBandHistoryQuery.Verb,
            Arg.Is<ServiceResult<FuturesBbSignalReadModel[]>>(result =>
                result.Success
                && result.Value != null
                && result.Value.Length == 40
                && result.Value.All(signal =>
                    signal.Metadata.ValueDate < query.ValueDate
                    && signal.Ema20Center.HasValue
                    && signal.Upper20.HasValue
                    && signal.Lower20.HasValue)
                && result.Value[result.Value.Length - 1].Metadata.ValueDate == query.ValueDate.AddDays(-1)));
    }

    static FuturesEodObservationReadModel HistoricalObservation(
        MarketSeriesIdentity identity,
        DateOnly valueDate,
        decimal close)
    {
        var start = new DateTimeOffset(
            valueDate.ToDateTime(new TimeOnly(14, 30), DateTimeKind.Utc));
        var end = start.AddHours(6).AddMinutes(30);
        return new()
        {
            MarketSeriesIdentity = identity,
            ContractId = "ES-HISTORICAL",
            ValueDate = valueDate,
            SessionStartUtc = start,
            SessionEndUtc = end,
            Open = close - 2m,
            High = close + 5m,
            Low = close - 5m,
            Close = close,
            Volume = 1_000m,
            TradeCount = 100,
            PriceVolumeSum = close * 1_000m,
            ObservationId = new(Guid.NewGuid()),
            FirstSourceSequence = valueDate.DayNumber * 100L,
            LastSourceSequence = valueDate.DayNumber * 100L + 99L,
            FirstMarketEventUtc = start,
            LastMarketEventUtc = end.AddMilliseconds(-1),
            IsComplete = true,
            IsValid = true
        };
    }

    [Fact]
    public void UnknownQueryVerb_IsAVisibleStrictMappingError()
    {
        var scenario = CreateScenario();
        var query = Query();
        var message = Message(query, $"Query.{GetMarketOutlookSnapshotQuery.Actor}.Unknown.{query.EntityId.Format()}");

        var action = () => scenario.Actor.Parse(scenario.ReceiveContext, message);

        action.Should().Throw<InvalidOperationException>();
    }

    Scenario CreateScenario(MarketOutlookSnapshotQueryPolicy? policy = null)
    {
        var db = Substitute.For<IMarketDataDbContext>();
        var factory = Substitute.For<IDbContextFactory>();
        factory.MarketDataDb.Returns(db);
        var historicalObservations = Substitute.For<IHistoricalObservationStore>();
        var context = new MarketOutlookSnapshotQueryContext(
            Substitute.For<IActorSupervisor>(),
            factory,
            historicalObservations,
            Substitute.For<ILogger<MarketOutlookSnapshotQueryActor>>(),
            policy);
        var receive = Substitute.For<IQueryActorContext<MarketOutlookSnapshotQueryActor>>();
        receive.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>())
            .Returns(true);
        return new(new TestActor(context), db, historicalObservations, receive);
    }

    static GetMarketOutlookSnapshotQuery Query()
    {
        var id = new MarketOutlookEntityId(SampleData.ContractId, SampleData.ValueDate);
        return new(id.ContractId, id.ValueDate)
        {
            Subject = new(
                ActorType.Query,
                GetMarketOutlookSnapshotQuery.Actor,
                GetMarketOutlookSnapshotQuery.Verb,
                id.Format())
        };
    }

    NatsMsg<byte[]> Message(GetMarketOutlookSnapshotQuery query, string subject) => new(
        subject,
        string.Empty,
        0,
        default!,
        fixture.DataSerializer.Serialize(query),
        default!,
        NatsMsgFlags.None);

    sealed record Scenario(
        TestActor Actor,
        IMarketDataDbContext Db,
        IHistoricalObservationStore HistoricalObservations,
        IQueryActorContext<MarketOutlookSnapshotQueryActor> ReceiveContext);
}
