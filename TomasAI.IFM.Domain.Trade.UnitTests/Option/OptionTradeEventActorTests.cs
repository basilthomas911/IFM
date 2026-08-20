using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Option.Event.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Option;

public sealed class OptionTradeEventActorTests : IClassFixture<TradeFixture>
{
    public OptionTradeEventActorTests(TradeFixture fixture)
        => _ = fixture;

    sealed class TestableOptionTradeEventActor(
        IActorSupervisor supervisor,
        IActorOptionPricerCommandApiFactory commandApiFactory,
        IStatusConsoleWriter statusConsole,
        ILogger<OptionTradeEventActor> logger)
        : OptionTradeEventActor(supervisor, commandApiFactory, statusConsole, logger)
    {
        public IEvent Parse(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public ValueTask Receive(IEventActorContext context, IEvent @event)
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

        var parsed = actor.Parse(Substitute.For<IEventActorContext>(), message);

        parsed.Should().BeOfType<OptionTradeEndOfDayProcessedEvent>()
            .Which.CommandId.Should().Be(source.CommandId);
    }

    [Fact]
    public async Task End_of_day_source_event_dispatches_one_correlated_unrealized_fund_transaction()
    {
        var source = SourceEvent();
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        context.RequestAsync<ProcessEndOfDayFundTransactionCommand, FundTransactionEntityId>(
                Arg.Any<ProcessEndOfDayFundTransactionCommand>())
            .Returns(ValueTask.FromResult<ServiceResult<GuidResult>>(
                new ServiceOk<GuidResult>(new GuidResult(source.CommandId))));

        await actor.Receive(context, source);

        await context.Received(1)
                .RequestAsync<ProcessEndOfDayFundTransactionCommand, FundTransactionEntityId>(
                Arg.Is<ProcessEndOfDayFundTransactionCommand>(command =>
                    command.CommandId != Guid.Empty
                    && command.CommandId != source.CommandId
                    && command.CorrelationId == source.CommandId
                    && command.PostEvents
                    && command.FundTransaction.FundId == source.FundId
                    && command.FundTransaction.OrderId == source.OrderId
                    && command.FundTransaction.TradeId == source.EntityId.TradeId
                    && command.FundTransaction.TradeType == source.EodKey.TradeType
                    && command.FundTransaction.ValueDate == source.EodKey.ValueDate
                    && command.FundTransaction.TransactionType == FundTransactionType.UnrealizedTradePnl
                    && command.FundTransaction.Amount == source.TradePnl
                    && command.FundTransaction.Description == source.Reference));
    }

    [Fact]
    public async Task Rejected_fund_continuation_fails_the_event_handler()
    {
        var source = SourceEvent();
        var actor = CreateActor();
        var context = Substitute.For<IEventActorContext>();
        context.RequestAsync<ProcessEndOfDayFundTransactionCommand, FundTransactionEntityId>(
                Arg.Any<ProcessEndOfDayFundTransactionCommand>())
            .Returns(ValueTask.FromResult<ServiceResult<GuidResult>>(
                new ServiceFailed<GuidResult>(2009, "fund continuation rejected")));

        var action = () => actor.Receive(context, source).AsTask();

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("fund continuation rejected");
    }

    static TestableOptionTradeEventActor CreateActor()
        => new(
            Substitute.For<IActorSupervisor>(),
            Substitute.For<IActorOptionPricerCommandApiFactory>(),
            Substitute.For<IStatusConsoleWriter>(),
            Substitute.For<ILogger<OptionTradeEventActor>>());

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
