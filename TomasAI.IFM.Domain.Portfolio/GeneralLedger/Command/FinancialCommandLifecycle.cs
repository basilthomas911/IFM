using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
public static partial class FinancialCommandLifecycle
{
    [LoggerMessage(EventId = 8920, Level = LogLevel.Error, Message = "Financial Command failed before a confirmed receipt could be returned.")]
    private static partial void FinancialCommandFailed(ILogger logger, Exception exception);

    /// <summary>An audit reservation is not a financial receipt. Re-enter validated handling to recover the original operation.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="token">The token business input.</param>
    /// <returns>The operation result.</returns>
    public static ValueTask<bool> ReconcileFinancialDuplicateAsync(this ICommand command, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(command is IFinancialRequest);
    }
    /// <summary>Handles Load Financial State Async at the command boundary.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <returns>The operation result.</returns>
    public static ValueTask<IActorState> LoadFinancialStateAsync(this ICommand command)
        
    {
        ArgumentNullException.ThrowIfNull(command);
        return ValueTask.FromResult<IActorState>(new FinancialCommandState(command.Subject.ThreadId));
    }
    /// <summary>Handles Financial Command Failure at the command boundary.</summary>
    /// <param name="exception">The exception business input.</param>
    /// <param name="logger">The logger business input.</param>
    /// <returns>The operation result.</returns>
    public static ValueTask<ServiceResult<GuidResult>> FinancialCommandFailure(this Exception exception, ILogger? logger = null)
    {
        if (exception is CommandAuditPayloadConflictException)
            return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(
                FinancialReasons.RequestMismatch, "Financial command ID is already associated with different request content."));
        if (exception is not FinancialOperationException && logger is not null)
            FinancialCommandFailed(logger, exception);
        var financial = exception as FinancialOperationException;
        var unknown = exception is FunctionCommitOutcomeUnknownException;
        var invalid = exception is TomasAI.IFM.Shared.Exceptions.CommandValidationException;
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(
            (unknown) switch
{
    true => FinancialReasons.CommitUnknown,
    false => financial?.Code ?? (invalid ? FinancialReasons.InvalidContract : FinancialReasons.PersistenceFailed)},
            (unknown) switch
{
    true => "FIN.COMMIT.UNKNOWN: query the original OperationId before any replacement attempt.",
    false => financial?.Message ?? (invalid ? exception.Message : "Financial operation could not complete.")})
        { Value = financial?.ExistingOperationId is { } existing ? new GuidResult(existing) : null });
    }
}
