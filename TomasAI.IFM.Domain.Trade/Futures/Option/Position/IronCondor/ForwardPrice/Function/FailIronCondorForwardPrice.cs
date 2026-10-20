using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function;
public static class FailIronCondorForwardPrice
{
    /// <summary>Returns a typed failure without committing incomplete calculation state.</summary>
    /// <param name="input">The failed request and execution-stage diagnostics.</param>
    /// <param name="clock">Supplies the UTC failure time.</param>
    /// <returns>The typed failed terminal result.</returns>
    public static FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<ForwardPriceSessionId>> Fail(this FunctionEventContext<CalculateIronCondorForwardPriceCommand> input, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(input);
        var command = input.Request;
        var now = clock.GetUtcNow().UtcDateTime;
        var reason = input.IsConflict ? "IronCondorForwardPrice.COMMAND.CONFLICT" : $"IronCondorForwardPrice.{input.Stage.ToString().ToUpperInvariant()}.FAILED";
        var failed = new TradePlanFailedEvent<ForwardPriceSessionId>
        {
            Subject = command?.Subject ?? ActorSubject.Unknown,
            Id = Guid.CreateVersion7(clock.GetUtcNow()),
            EntityId = command?.EntityId ?? default,
            CommandId = command?.CommandId ?? Guid.Empty,
            AggregateId = command?.EntityId.Format() ?? string.Empty,
            EventSource = command?.EventSource ?? CalculateIronCondorForwardPriceCommand.Actor,
            ReceivedOn = now,
            ErrorDate = now,
            ErrorCode = command?.ErrorCode ?? 27121,
            ErrorMessage = input.Exception?.Message ?? "Forward-price request conflicts with its committed command.",
            ErrorData = reason,
            CommandName = command?.CommandName ?? nameof(CalculateIronCondorForwardPriceCommand)
        };
        return FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<ForwardPriceSessionId>>.Fail(failed);
    }
}
