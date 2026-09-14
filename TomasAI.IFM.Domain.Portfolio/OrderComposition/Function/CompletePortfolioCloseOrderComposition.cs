using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Function;

public static class CompletePortfolioCloseOrderComposition
{
    public static FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
        PortfolioCloseOrderCompositionFailedEvent> Complete(
        this FunctionEventContext<EvaluatePortfolioCloseOrderCompositionCommand> input,
        TimeProvider timeProvider)
    {
        if (input.Outcome is PortfolioCloseOrderCompositionCompletedEvent committed)
            return FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
                PortfolioCloseOrderCompositionFailedEvent>.Complete(committed);
        var request = input.Request ?? throw new InvalidOperationException("Completion requires a request.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,
            PortfolioCloseOrderCompositionFailedEvent>.Complete(new()
            {
                Id = Guid.NewGuid(), Subject = request.Subject, EntityId = request.EntityId,
                CommandId = request.CommandId, OperationId = request.OperationId,
                PortfolioId = request.PortfolioId, CorrelationId = request.CorrelationId,
                CausationId = request.CausationId, CommittedAtUtc = now, ReceivedOn = now,
                InputHash = request.InputSha256,
                Receipt = input.Outcome as PortfolioCloseOrderCompositionReceipt ?? new(),
                AggregateId = request.EntityId.Format()
            });
    }
}
