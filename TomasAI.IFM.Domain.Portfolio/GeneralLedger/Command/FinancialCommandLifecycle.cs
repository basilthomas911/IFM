using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;

/// <summary>Command state is only routing identity; authority is never cached outside its enlisted transaction.</summary>
public sealed class FinancialCommandState(ActorThreadId id) : IActorState
{
    public ActorThreadId Id { get; set; }=id;
}
public static class FinancialCommandLifecycle
{
    /// <summary>An audit reservation is not a financial receipt. Re-enter validated handling to recover the original operation.</summary>
    public static ValueTask<bool> ReconcileFinancialDuplicateAsync(this ICommand command,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(command is IFinancialRequest);
    }
    public static ValueTask<IActorState> LoadFinancialStateAsync(this ICommand command)
        =>ValueTask.FromResult<IActorState>(new FinancialCommandState(command.Subject.ThreadId));
    public static ValueTask<ServiceResult<GuidResult>> FinancialCommandFailure(this Exception exception,ILogger? logger=null)
    {
        if(exception is not FinancialOperationException) logger?.LogError(exception,"Financial Command failed before a confirmed receipt could be returned.");
        var financial=exception as FinancialOperationException;
        var unknown=exception is FunctionCommitOutcomeUnknownException;
        var invalid=exception is TomasAI.IFM.Shared.Exceptions.CommandValidationException;
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(
            unknown?FinancialReasons.CommitUnknown:financial?.Code??(invalid?FinancialReasons.InvalidContract:FinancialReasons.PersistenceFailed),
            unknown?"FIN.COMMIT.UNKNOWN: query the original OperationId before any replacement attempt.":financial?.Message??(invalid?exception.Message:"Financial operation could not complete."))
            { Value=financial?.ExistingOperationId is { } existing?new GuidResult(existing):null });
    }
}
