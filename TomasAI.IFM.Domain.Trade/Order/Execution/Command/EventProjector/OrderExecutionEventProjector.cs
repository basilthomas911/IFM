using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Futures.Option.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Command.Actor;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Order.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.EventProjector;

public sealed class OrderExecutionEventProjector
    : ConventionalEventProjector<OrderExecutionCommandActor>
{
    readonly IOrderExecutionCommandContext context;
    readonly ImmutableArray<EventProjectionDescriptor> descriptors;

    public OrderExecutionEventProjector(
        ICommandActorContext<OrderExecutionCommandActor> actorContext,
        EventProjectorReliabilityOptions? options = null)
        : base(
            Typed(actorContext).DurableReplayQueue,
            Typed(actorContext).DbEventSource,
            Typed(actorContext).BlackboardService,
            Typed(actorContext).Logger,
            options)
    {
        context = Typed(actorContext);
        descriptors =
        [
            DescribeNotification<OrderExecutionChangedEvent, OrderExecutionId>(ProjectAsync)
        ];
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(OrderExecutionChangedEvent)];

    async Task ProjectAsync(OrderExecutionChangedEvent changed)
    {
        await context.DbFactory.TradeDb.UpsertOrderExecutionAsync(changed.State).ConfigureAwait(false);

        if (changed.State.Status is OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected &&
            changed.State.Fills.Length == 0)
        {
            await ReleaseTradeOrderAsync(changed).ConfigureAwait(false);
            return;
        }

        if (changed.State.Status != OrderExecutionStatus.Filled) return;

        foreach (var trade in changed.CreatedTrades)
            await EstablishAsync(trade).ConfigureAwait(false);
        foreach (var closedPosition in changed.ClosedPositions)
            await CloseAsync(closedPosition).ConfigureAwait(false);

        var complete = new CompleteTradeOrderCommand
        {
            CommandId = TradeHandoffIdentity.Create(
                "complete-order",
                changed.State.TradeOrderId.Format(),
                changed.State.ExecutionAttemptId.ToString("N")),
            Subject = new ActorSubject(
                ActorType.Command,
                TradeOrderCommandActor.ActorName,
                CompleteTradeOrderCommand.Verb,
                changed.State.TradeOrderId.Format()),
            EntityId = changed.State.TradeOrderId
        };
        Ensure(await context.ActorService
            .SendAsync<CompleteTradeOrderCommand, TradeOrderId>(complete, complete.EntityId)
            .ConfigureAwait(false));
    }

    async ValueTask ReleaseTradeOrderAsync(OrderExecutionChangedEvent changed)
    {
        var release = new ReleaseTradeOrderExecutionCommand
        {
            CommandId = TradeHandoffIdentity.Create(
                "release-order-execution",
                changed.State.TradeOrderId.Format(),
                changed.State.ExecutionAttemptId.ToString("N")),
            Subject = new ActorSubject(
                ActorType.Command,
                TradeOrderCommandActor.ActorName,
                ReleaseTradeOrderExecutionCommand.Verb,
                changed.State.TradeOrderId.Format()),
            EntityId = changed.State.TradeOrderId,
            ExecutionAttemptId = changed.State.ExecutionAttemptId,
            ZeroExposureConfirmed = true,
            EffectiveAtUtc = changed.ReceivedOn.Kind == DateTimeKind.Utc
                ? changed.ReceivedOn
                : DateTime.SpecifyKind(changed.ReceivedOn, DateTimeKind.Utc)
        };
        Ensure(await context.ActorService
            .SendAsync<ReleaseTradeOrderExecutionCommand, TradeOrderId>(release, release.EntityId)
            .ConfigureAwait(false));
    }

    async ValueTask EstablishAsync(EstablishedTradeDefinition trade)
    {
        if (trade.AssetFamily == TradeAssetFamily.Futures)
        {
            var command = new CreateFuturesTradeCommand
            {
                CommandId = TradeHandoffIdentity.Create("futures-trade", trade.Id.Format()),
                Subject = new ActorSubject(
                    ActorType.Command,
                    FuturesTradeCommandActor.ActorName,
                    CreateFuturesTradeCommand.Verb,
                    trade.Id.Format()),
                EntityId = trade.Id,
                Trade = trade
            };
            Ensure(await context.ActorService
                .SendAsync<CreateFuturesTradeCommand, TradeEntityId>(command, command.EntityId)
                .ConfigureAwait(false));
            return;
        }

        var optionCommand = new CreateOptionTradeCommand
        {
            CommandId = TradeHandoffIdentity.Create("option-trade", trade.Id.Format()),
            Subject = new ActorSubject(
                ActorType.Command,
                FuturesOptionTradeCommandActor.ActorName,
                CreateOptionTradeCommand.Verb,
                trade.Id.Format()),
            EntityId = trade.Id,
            Trade = trade
        };
        Ensure(await context.ActorService
            .SendAsync<CreateOptionTradeCommand, TradeEntityId>(optionCommand, optionCommand.EntityId)
            .ConfigureAwait(false));
    }

    async ValueTask CloseAsync(PositionCloseExecution close)
    {
        var tradeId = close.PositionId.Trade;
        if (close.StrategyKind == TradeStrategyKind.FuturesOutright)
        {
            var begin = new BeginCloseFuturesTradeCommand
            {
                CommandId = TradeHandoffIdentity.Create("begin-close-futures-trade", tradeId.Format(),
                    close.ExecutionAttemptId.ToString("N")),
                Subject = new(ActorType.Command, FuturesTradeCommandActor.ActorName,
                    BeginCloseFuturesTradeCommand.Verb, tradeId.Format()),
                EntityId = tradeId
            };
            Ensure(await context.ActorService.SendAsync<BeginCloseFuturesTradeCommand, TradeEntityId>(
                begin, begin.EntityId).ConfigureAwait(false));
            var closeTrade = new CloseFuturesTradeCommand
            {
                CommandId = TradeHandoffIdentity.Create("close-futures-trade", tradeId.Format(),
                    close.ExecutionAttemptId.ToString("N")),
                Subject = new(ActorType.Command, FuturesTradeCommandActor.ActorName,
                    CloseFuturesTradeCommand.Verb, tradeId.Format()),
                EntityId = tradeId,
                ClosingFills = close.Fills,
                ClosedAtUtc = close.CompletedAtUtc
            };
            Ensure(await context.ActorService.SendAsync<CloseFuturesTradeCommand, TradeEntityId>(
                closeTrade, closeTrade.EntityId).ConfigureAwait(false));
            var closePosition = new CloseFuturesPositionCommand
            {
                CommandId = TradeHandoffIdentity.Create("close-futures-position", close.PositionId.Format(),
                    close.ExecutionAttemptId.ToString("N")),
                Subject = new(ActorType.Command, FuturesPositionActorNames.Command,
                    CloseFuturesPositionCommand.Verb, close.PositionId.Format()),
                EntityId = close.PositionId,
                EffectiveAtUtc = close.CompletedAtUtc
            };
            Ensure(await context.ActorService.SendAsync<CloseFuturesPositionCommand, StrategyPositionId>(
                closePosition, closePosition.EntityId).ConfigureAwait(false));
            return;
        }

        var beginOption = new BeginCloseOptionTradeCommand
        {
            CommandId = TradeHandoffIdentity.Create("begin-close-option-trade", tradeId.Format(),
                close.ExecutionAttemptId.ToString("N")),
            Subject = new(ActorType.Command, FuturesOptionTradeCommandActor.ActorName,
                BeginCloseOptionTradeCommand.Verb, tradeId.Format()),
            EntityId = tradeId
        };
        Ensure(await context.ActorService.SendAsync<BeginCloseOptionTradeCommand, TradeEntityId>(
            beginOption, beginOption.EntityId).ConfigureAwait(false));
        var closeOption = new CloseOptionTradeCommand
        {
            CommandId = TradeHandoffIdentity.Create("close-option-trade", tradeId.Format(),
                close.ExecutionAttemptId.ToString("N")),
            Subject = new(ActorType.Command, FuturesOptionTradeCommandActor.ActorName,
                CloseOptionTradeCommand.Verb, tradeId.Format()),
            EntityId = tradeId,
            ClosingFills = close.Fills,
            ClosedAtUtc = close.CompletedAtUtc
        };
        Ensure(await context.ActorService.SendAsync<CloseOptionTradeCommand, TradeEntityId>(
            closeOption, closeOption.EntityId).ConfigureAwait(false));

        if (close.StrategyKind == TradeStrategyKind.IronCondor)
        {
            var closePosition = new CloseIronCondorPositionCommand
            {
                CommandId = TradeHandoffIdentity.Create("close-iron-condor-position", close.PositionId.Format(),
                    close.ExecutionAttemptId.ToString("N")),
                Subject = new(ActorType.Command, PositionActorNames.IronCondorCommand,
                    CloseIronCondorPositionCommand.Verb, close.PositionId.Format()),
                EntityId = close.PositionId,
                EffectiveAtUtc = close.CompletedAtUtc
            };
            Ensure(await context.ActorService.SendAsync<CloseIronCondorPositionCommand, StrategyPositionId>(
                closePosition, closePosition.EntityId).ConfigureAwait(false));
            return;
        }

        if (close.StrategyKind == TradeStrategyKind.VerticalSpread)
        {
            var closePosition = new CloseVerticalSpreadPositionCommand
            {
                CommandId = TradeHandoffIdentity.Create("close-vertical-spread-position", close.PositionId.Format(),
                    close.ExecutionAttemptId.ToString("N")),
                Subject = new(ActorType.Command, PositionActorNames.VerticalSpreadCommand,
                    CloseVerticalSpreadPositionCommand.Verb, close.PositionId.Format()),
                EntityId = close.PositionId,
                EffectiveAtUtc = close.CompletedAtUtc
            };
            Ensure(await context.ActorService.SendAsync<CloseVerticalSpreadPositionCommand, StrategyPositionId>(
                closePosition, closePosition.EntityId).ConfigureAwait(false));
            return;
        }

        throw new InvalidOperationException($"ORDER_EXECUTION.CLOSE_STRATEGY_UNSUPPORTED;{close.StrategyKind}");
    }

    static void Ensure(ServiceResult<Guid> result)
    {
        if (!result.Success)
            throw new InvalidOperationException(
                $"ORDER_EXECUTION.HANDOFF_FAILED;{result.ErrorCode};{result.ErrorMessage}");
    }

    static IOrderExecutionCommandContext Typed(
        ICommandActorContext<OrderExecutionCommandActor> actorContext) =>
        actorContext as IOrderExecutionCommandContext ??
        throw new ArgumentException("Typed Order Execution context required.");
}
