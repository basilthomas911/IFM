using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function;

public static class FailPortfolioCloseOrderComposition
{
    public static FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
        PortfolioCloseOrderCompositionFailedEvent> Fail(
        this FunctionEventContext<EvaluatePortfolioCloseOrderCompositionCommand> input,
        TimeProvider timeProvider)
    {
        var request = input.Request;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var detail = input.Exception?.ToString() ??
            (input.IsConflict ? "A completed operation exists with different input." :
                "Portfolio close-order composition failed.");
        return FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
            PortfolioCloseOrderCompositionFailedEvent>.Fail(new()
            {
                Id = Guid.NewGuid(),
                Subject = new(ActorType.Function, EvaluatePortfolioCloseOrderCompositionCommand.Actor,
                    nameof(PortfolioCloseOrderCompositionFailedEvent),
                    request?.EntityId.Format() ?? string.Empty),
                EntityId = request?.EntityId ?? new(0, Guid.Empty),
                CommandId = request?.CommandId ?? Guid.Empty,
                ErrorDate = now, ReceivedOn = now,
                ErrorCode = request?.ErrorCode ?? 34131,
                ErrorMessage = input.Exception?.Message ?? "Portfolio close-order composition failed.",
                ErrorType = ErrorType.Command, ErrorData = detail,
                CommandName = nameof(EvaluatePortfolioCloseOrderCompositionCommand),
                AggregateId = request?.EntityId.Format() ?? string.Empty
            });
    }
}
