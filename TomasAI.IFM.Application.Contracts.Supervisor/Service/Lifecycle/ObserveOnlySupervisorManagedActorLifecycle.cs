using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Lifecycle;

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
    public ValueTask<SupervisorActorOperationResult> PauseAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken)
        => RejectAsync(request, SupervisorActorOperationKind.Pause, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SupervisorActorOperationResult> DrainAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken)
        => RejectAsync(request, SupervisorActorOperationKind.Drain, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SupervisorActorOperationResult> ResumeAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken)
        => RejectAsync(request, SupervisorActorOperationKind.Resume, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SupervisorActorOperationResult> StopAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken)
        => RejectAsync(request, SupervisorActorOperationKind.Stop, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SupervisorActorOperationResult> RestartAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken)
        => RejectAsync(request, SupervisorActorOperationKind.Restart, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SupervisorActorOperationResult> QuarantineAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken)
        => RejectAsync(request, SupervisorActorOperationKind.Quarantine, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SupervisorActorOperationResult> RetireAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken)
        => RejectAsync(request, SupervisorActorOperationKind.Retire, cancellationToken);

    /// <inheritdoc />
    public ValueTask<SupervisorActorOperationResult> RecycleAsync(SupervisorActorOperationRequest request, CancellationToken cancellationToken)
        => RejectAsync(request, SupervisorActorOperationKind.Recycle, cancellationToken);

    static ValueTask<SupervisorActorOperationResult> RejectAsync(
        SupervisorActorOperationRequest request, SupervisorActorOperationKind expectedOperation,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request is null)
                return ValueTask.FromResult(new SupervisorActorOperationResult(
                    SupervisorOperationOutcome.Rejected, Guid.Empty, EmptyTarget, 0,
                    "Validation", "An operation request is required."));
            if (request.Operation != expectedOperation)
                return ValueTask.FromResult(new SupervisorActorOperationResult(
                    SupervisorOperationOutcome.Rejected, request.OperationId, request.Target,
                    request.ExpectedGeneration, "Validation", "The named lifecycle operation does not match the request."));
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

    /// <inheritdoc />
    public ValueTask<SupervisorRecoveryResult> ReconcileAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var reason = cancellationToken.IsCancellationRequested ? "Supervisor reconciliation was cancelled." : DisabledReason;
        return ValueTask.FromResult(new SupervisorRecoveryResult(Guid.NewGuid(), false, 0, 0, 0, 1,
            [new("Supervisor", SupervisorActorHealth.Unknown, "ObserveOnly", reason)], reason));
    }

    static bool IsRecoverable(Exception exception) =>
        exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException;
}
