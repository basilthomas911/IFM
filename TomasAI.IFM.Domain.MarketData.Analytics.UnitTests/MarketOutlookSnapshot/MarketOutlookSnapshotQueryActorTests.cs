using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
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
    public void UnknownQueryVerb_IsAVisibleStrictMappingError()
    {
        var scenario = CreateScenario();
        var query = Query();
        var message = Message(query, $"Query.{GetMarketOutlookSnapshotQuery.Actor}.Unknown.{query.EntityId.Format()}");

        var action = () => scenario.Actor.Parse(scenario.ReceiveContext, message);

        action.Should().Throw<InvalidOperationException>();
    }

    Scenario CreateScenario()
    {
        var db = Substitute.For<IMarketDataDbContext>();
        var factory = Substitute.For<IDbContextFactory>();
        factory.MarketDataDb.Returns(db);
        var context = new MarketOutlookSnapshotQueryContext(
            Substitute.For<IActorSupervisor>(),
            factory,
            Substitute.For<ILogger<MarketOutlookSnapshotQueryActor>>());
        var receive = Substitute.For<IQueryActorContext<MarketOutlookSnapshotQueryActor>>();
        receive.SetMessageInfo(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Any<ActorMessageInfo>())
            .Returns(true);
        return new(new TestActor(context), db, receive);
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
        IQueryActorContext<MarketOutlookSnapshotQueryActor> ReceiveContext);
}
