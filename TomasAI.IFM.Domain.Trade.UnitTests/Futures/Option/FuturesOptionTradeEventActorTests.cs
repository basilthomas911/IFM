using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Futures.Option.Event.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Futures.Option;

public sealed class FuturesOptionTradeEventActorTests : IClassFixture<TradeFixture>
{
    public FuturesOptionTradeEventActorTests(TradeFixture fixture)
        => _ = fixture;

    sealed class TestableOptionTradeEventActor(
        IActorSupervisor supervisor,
        IStatusConsoleWriter statusConsole,
        ILogger<FuturesOptionTradeEventActor> logger)
        : FuturesOptionTradeEventActor(
            new FuturesOptionTradeEventContext(supervisor, Substitute.For<IPortfolioTradeValuationApi>(), statusConsole, logger))
    {
        public IEvent Parse(
            IEventActorContext<FuturesOptionTradeEventActor> context,
            NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public ValueTask Receive(
            IEventActorContext<FuturesOptionTradeEventActor> context,
            IEvent @event)
            => ReceiveAsync(context, @event);
    }

    [Fact]
    public void End_of_day_source_event_is_parsed_by_the_option_trade_actor()
    {
        var source = SourceEvent();
        var actor = CreateActor();
        var message = new NatsMsg<byte[]>
        {
            Subject = source.Subject.ToString(),
            Data = ActorExtensions.DataSerializer!.Serialize(source)
        };

        var parsed = actor.Parse(
            Substitute.For<IEventActorContext<FuturesOptionTradeEventActor>>(), message);

        parsed.Should().BeOfType<OptionTradeEndOfDayProcessedEvent>()
            .Which.CommandId.Should().Be(source.CommandId);
    }

    [Fact]
    public async Task End_of_day_source_event_dispatches_one_canonical_portfolio_valuation()
    {
        var source = SourceEvent();
        var actor = CreateActor();
        var context = Substitute.For<IFuturesOptionTradeEventContext>();
        context.PortfolioValuation.PostAsync(
                Arg.Any<PortfolioTradeValuationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ServiceResult<Guid>>(
                new ServiceOk<Guid>(source.CommandId)));

        await actor.Receive(context, source);

        await context.PortfolioValuation.Received(1).PostAsync(
            Arg.Is<PortfolioTradeValuationRequest>(request =>
                request.FundId == source.FundId
                && request.OrderId == source.OrderId
                && request.TradeId == source.EntityId.TradeId
                && request.ValueDate == source.EodKey.ValueDate
                && request.AbsoluteUnrealizedPnl == source.TradePnl
                && request.Description == source.Reference
                && request.SourceEventId == source.Id
                && request.SourceSequence == 0
                && request.ObservedAtUtc.Kind == DateTimeKind.Utc),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejected_portfolio_valuation_fails_the_event_handler()
    {
        var source = SourceEvent();
        var actor = CreateActor();
        var context = Substitute.For<IFuturesOptionTradeEventContext>();
        context.PortfolioValuation.PostAsync(
                Arg.Any<PortfolioTradeValuationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ServiceResult<Guid>>(
                new ServiceFailed<Guid>(2009, "portfolio valuation rejected")));

        var action = () => actor.Receive(context, source).AsTask();

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("portfolio valuation rejected");
    }
    static TestableOptionTradeEventActor CreateActor()
        => new(
            Substitute.For<IActorSupervisor>(),
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger<FuturesOptionTradeEventActor>>());

    static OptionTradeEndOfDayProcessedEvent SourceEvent()
    {
        var entityId = new OptionTradeEntityId(2801, 501);
        var valueDate = new DateOnly(2026, 8, 18);
        return new OptionTradeEndOfDayProcessedEvent
        {
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Event,
                OptionTradeEndOfDayProcessedEvent.Actor,
                OptionTradeEndOfDayProcessedEvent.Verb,
                entityId.Format()),
            EntityId = entityId,
            FundId = 2201,
            OrderId = entityId.OrderId,
            EodKey = new TradePositionEntityId(
                entityId.OrderId,
                entityId.TradeId,
                valueDate,
                TradeType.ShortIronCondor,
                TradeStatus.EndOfDay,
                31),
            TradePnl = 42.50m,
            Reference = "G2-EOD",
            EventSource = "unit-test",
            UpdatedOn = DateTime.UtcNow,
            UpdatedBy = "unit-test"
        };
    }
}
