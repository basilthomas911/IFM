using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Function;

/// <summary>Returns classified non-durable refusals; uncertainty never claims that capacity was released.</summary>
public static class FailCapacityReservation
{
    public static FunctionResult<CapacityReservationCompletedEvent, CapacityReservationFailedEvent> Fail(this FunctionEventContext<ReservePortfolioTradeRiskCommand> input, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(input);
        var request = input.Request; var error = input.Exception as FinancialOperationException;
        var unknown = input.Exception is FunctionCommitOutcomeUnknownException;
        var code = (unknown) switch
{
    true => FinancialReasons.CommitUnknown,
    false => error?.Code ?? ((input.IsConflict) switch
    {
        true => FinancialReasons.RequestMismatch,
        false => (input.Exception is TimeoutException) switch
        {
            true => FinancialReasons.TimeExpired,
            false => input.Stage is FunctionFailureStage.Parsing or FunctionFailureStage.Validation ? FinancialReasons.InvalidContract : FinancialReasons.PersistenceFailed
        }})};
        var now = clock.GetUtcNow().UtcDateTime;
        return FunctionResult<CapacityReservationCompletedEvent, CapacityReservationFailedEvent>.Fail(new()
        {
            Id = Guid.NewGuid(),
            Subject = new(ActorType.Function, ReservePortfolioTradeRiskCommand.Actor, nameof(CapacityReservationFailedEvent), request?.EntityId.Format() ?? string.Empty),
            EntityId = request?.EntityId ?? new(0, Guid.Empty),
            CommandId = request?.CommandId ?? Guid.Empty,
            OperationId = request?.OperationId ?? Guid.Empty,
            PortfolioId = request?.PortfolioId ?? 0,
            CorrelationId = request?.CorrelationId ?? Guid.Empty,
            CausationId = request?.CausationId ?? Guid.Empty,
            FailedAtUtc = now,
            ReceivedOn = now,
            ErrorCode = code,
            ReasonCode = FinancialReasons.Name(code),
            ErrorType = ErrorType.Command,
            FailureClass = input.Stage.ToString(),
            CommitDisposition = unknown ? FinancialCommitDisposition.OutcomeUnknown : error?.Disposition ?? FinancialCommitDisposition.NotCommitted,
            ExpectedRevision = request?.ExpectedFinancialRevision ?? 0,
            ExistingOperationId = error?.ExistingOperationId,
            Message = error?.Message ?? (unknown ? "Commit outcome is unknown; recover using the same operation identity." : "Financial operation could not complete."),
            AggregateId = request?.EntityId.Format() ?? string.Empty,
            CommandName = nameof(ReservePortfolioTradeRiskCommand)
        });
    }
}

