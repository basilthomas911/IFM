using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function;

public static class FailFuturesTradePlan
{
    public static FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>> Fail(
        this FunctionEventContext<UpdateFuturesTradePlanCommand> input, TimeProvider clock)
    {
        var command = input.Request;
        var now = clock.GetUtcNow().UtcDateTime;
        var reason = input.IsConflict ? "PLAN.COMMAND.CONFLICT" : $"PLAN.{input.Stage.ToString().ToUpperInvariant()}.FAILED";
        var failed = new TradePlanFailedEvent<FuturesTradePlanId>
        {
            Subject = command?.Subject ?? ActorSubject.Unknown,
            Id = Guid.CreateVersion7(clock.GetUtcNow()),
            EntityId = command?.EntityId ?? default,
            CommandId = command?.CommandId ?? Guid.Empty,
            AggregateId = command?.EntityId.Format() ?? string.Empty,
            EventSource = command?.EventSource ?? UpdateFuturesTradePlanCommand.Actor,
            ReceivedOn = now,
            ErrorDate = now,
            ErrorCode = command?.ErrorCode ?? 27103,
            ErrorMessage = input.Exception?.Message ?? "Trade Plan request conflicts with its committed command.",
            ErrorData = reason,
            CommandName = command?.CommandName ?? nameof(UpdateFuturesTradePlanCommand)
        };
        return FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>.Fail(failed);
    }
}
