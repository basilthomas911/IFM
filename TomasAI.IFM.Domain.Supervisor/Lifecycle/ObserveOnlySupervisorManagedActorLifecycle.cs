using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Lifecycle;

/// <summary>
/// Safe initial lifecycle implementation. It permits no actor mutation until startup ordering, draining,
/// generation fencing, rollback, authorization, and fault-injection gates have passed.
/// </summary>
public sealed class ObserveOnlySupervisorManagedActorLifecycle : ISupervisorManagedActorLifecycle
{
    const string DisabledReason = "Supervisor actor mutation is disabled until lifecycle safety gates pass.";
    static readonly ActorThreadId EmptyTarget = new(ActorType.Command, "Supervisor", "system");

    /// <inheritdoc />
    public ValueTask<SupervisorActorsStartupResult> StartupActorsAsync(CancellationToken cancellationToken)
    {
        var outcome = cancellationToken.IsCancellationRequested
            ? SupervisorOperationOutcome.Cancelled
            : SupervisorOperationOutcome.Rejected;
        return ValueTask.FromResult(new SupervisorActorsStartupResult(
            outcome, Guid.NewGuid(), 0, 0, "ObserveOnly", DisabledReason));
    }

    /// <inheritdoc />
    public ValueTask<SupervisorActorsShutdownResult> ShutdownActorsAsync(CancellationToken cancellationToken)
    {
        var outcome = cancellationToken.IsCancellationRequested
            ? SupervisorOperationOutcome.Cancelled
            : SupervisorOperationOutcome.Rejected;
        return ValueTask.FromResult(new SupervisorActorsShutdownResult(
            outcome, Guid.NewGuid(), 0, 0, "ObserveOnly", DisabledReason));
    }

    /// <inheritdoc />
    public ValueTask<SupervisorActorOperationResult> ExecuteAsync(
        SupervisorActorOperationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return ValueTask.FromResult(new SupervisorActorOperationResult(
                    SupervisorOperationOutcome.Rejected, Guid.Empty, EmptyTarget, 0,
                    "Validation", "An operation request is required."));
            var outcome = cancellationToken.IsCancellationRequested
                ? SupervisorOperationOutcome.Cancelled
                : SupervisorOperationOutcome.Rejected;
            return ValueTask.FromResult(new SupervisorActorOperationResult(
                outcome, request.OperationId, request.Target, request.ExpectedGeneration,
                "ObserveOnly", DisabledReason));
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            return ValueTask.FromResult(new SupervisorActorOperationResult(
                SupervisorOperationOutcome.EmergencyFallback, Guid.Empty, EmptyTarget, 0,
                "FinalContainment", exception.GetType().Name));
        }
    }

    static bool IsRecoverable(Exception exception) =>
        exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException;
}
