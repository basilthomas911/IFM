using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using System.Collections.Immutable;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Trade.Order.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Command.EventProjector;

public sealed class TradeOrderEventProjector : ConventionalEventProjector<TradeOrderCommandActor>
{
    readonly ITradeOrderCommandContext context;
    readonly ImmutableArray<EventProjectionDescriptor> descriptors;
    public TradeOrderEventProjector(ICommandActorContext<TradeOrderCommandActor> actorContext,
        EventProjectorReliabilityOptions? options = null)
        : base(Typed(actorContext).DurableReplayQueue,Typed(actorContext).DbEventSource,
            Typed(actorContext).BlackboardService,Typed(actorContext).Logger,options)
    {
        context = Typed(actorContext);
        descriptors = [DescribeNotification<TradeOrderChangedEvent, TomasAI.IFM.Domain.Trade.Shared.TradeOrderId>(ProjectAsync)];
    }
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(TradeOrderChangedEvent)];
    async Task ProjectAsync(TradeOrderChangedEvent changed)
    {
        await context.DbFactory.TradeDb.UpsertTradeOrderAsync(changed.State).ConfigureAwait(false);
        if (changed.State.Status != TomasAI.IFM.Domain.Trade.Shared.TradeOrderStatus.Executing || changed.ExecutionAttemptId == Guid.Empty) return;
        var executionId = new TomasAI.IFM.Domain.Trade.Shared.OrderExecutionId(
            changed.State.Id, changed.ExecutionAttemptId);
        var command = new StartOrderExecutionCommand
        {
            CommandId = TradeHandoffIdentity.Create("order-execution",changed.State.Id.Format(),changed.ExecutionAttemptId.ToString("N")),
            Subject = new ActorSubject(ActorType.Command,OrderExecutionActorNames.Command,StartOrderExecutionCommand.Verb,executionId.Format()),
            EntityId = executionId, Order = changed.State, ExecutionAttemptId = changed.ExecutionAttemptId,
            Channel = changed.ExecutionChannel, EffectiveAtUtc = changed.ReceivedOn.Kind == DateTimeKind.Utc ? changed.ReceivedOn : DateTime.UtcNow
        };
        var result = await context.ActorService.SendAsync<StartOrderExecutionCommand, TomasAI.IFM.Domain.Trade.Shared.OrderExecutionId>(command,command.EntityId).ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException($"TRADE_ORDER.HANDOFF_FAILED;{result.ErrorCode};{result.ErrorMessage}");
    }
    static ITradeOrderCommandContext Typed(ICommandActorContext<TradeOrderCommandActor> c) => c as ITradeOrderCommandContext ?? throw new ArgumentException("Typed Trade Order command context required.");
}
