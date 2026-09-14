using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function;

public static class FailVerticalSpreadTradePlan
{
    public static FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>> Fail(
        this FunctionEventContext<UpdateVerticalSpreadTradePlanCommand> input, TimeProvider clock)
    {
        var command = input.Request;
        var now = clock.GetUtcNow().UtcDateTime;
        var reason = input.IsConflict ? "PLAN.COMMAND.CONFLICT" : $"PLAN.{input.Stage.ToString().ToUpperInvariant()}.FAILED";
        var failed = new TradePlanFailedEvent<VerticalSpreadTradePlanId>
        {
            Subject = command?.Subject ?? ActorSubject.Unknown,
            Id = Guid.CreateVersion7(clock.GetUtcNow()),
            EntityId = command?.EntityId ?? default,
            CommandId = command?.CommandId ?? Guid.Empty,
            AggregateId = command?.EntityId.Format() ?? string.Empty,
            EventSource = command?.EventSource ?? UpdateVerticalSpreadTradePlanCommand.Actor,
            ReceivedOn = now,
            ErrorDate = now,
            ErrorCode = command?.ErrorCode ?? 27102,
            ErrorMessage = input.Exception?.Message ?? "Trade Plan request conflicts with its committed command.",
            ErrorData = reason,
            CommandName = command?.CommandName ?? nameof(UpdateVerticalSpreadTradePlanCommand)
        };
        return FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>.Fail(failed);
    }
}
