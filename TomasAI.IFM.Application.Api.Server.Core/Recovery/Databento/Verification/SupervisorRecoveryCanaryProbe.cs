using TomasAI.IFM.Domain.Supervisor.Recovery;
using TomasAI.IFM.Domain.Supervisor.Recovery.Event.Projection;
using TomasAI.IFM.Domain.Supervisor.Shared.Recovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.Verification;

public sealed record SupervisorRecoveryCanaryResult(bool Qualified, Guid CorrelationId,
    Guid GenerationId, string Dataset, string Detail, DateTime? ProjectedUtc)
{
    /// <summary>The canary traverses only Supervisor-owned control-plane components.</summary>
    public RecoveryProofScope ProofScope => RecoveryProofScope.SupervisorControlPlane;

    /// <summary>A control-plane canary never authorizes market-data publication admission.</summary>
    public bool QualifiesMarketDataAdmission => false;
}

/// <summary>Proves NATS command, Supervisor actor, JetStream event, and projector progress without market-data admission.</summary>
public sealed class SupervisorRecoveryCanaryProbe(
    IActorSupervisor supervisor,
    RecoveryCanaryProjectionStore projection,
    TimeProvider time)
{
    public async Task<SupervisorRecoveryCanaryResult> ProbeAsync(Guid correlationId,
        Guid generationId, DateOnly valueDate, string dataset, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (correlationId == Guid.Empty || generationId == Guid.Empty || valueDate == default
            || string.IsNullOrWhiteSpace(dataset) || dataset.Length > 64
            || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(2))
            throw new ArgumentException("Recovery canary identity and timeout must be bounded.");
        if (!supervisor.IsReady)
            return new(false, correlationId, generationId, dataset, "Supervisor actor intake is not ready.", null);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var subject = new ActorSubject(ActorType.Command, RecoveryCanaryCommand.Actor,
            RecoveryCanaryCommand.Verb, correlationId.ToString("N"));
        var command = new RecoveryCanaryCommand(Guid.NewGuid(), subject, correlationId,
            generationId, valueDate, dataset, time.GetUtcNow().UtcDateTime);
        try
        {
            var acknowledgement = await supervisor.GetProducer(subject.ActorId)
                .RequestAsync<RecoveryCanaryCommand, ActorEntityId, GuidResult>(subject, command,
                    command.EntityId, deadline.Token).ConfigureAwait(false);
            if (!acknowledgement.Success || acknowledgement.Value?.Guid != command.CommandId)
                return new(false, correlationId, generationId, dataset,
                    acknowledgement.ErrorMessage ?? "Supervisor recovery canary command failed.", null);
            while (true)
            {
                if (projection.TryGet(correlationId, generationId, valueDate, dataset, out var projected))
                    return new(true, correlationId, generationId, dataset,
                        "Supervisor recovery canary reached the projector.", projected.ProjectedUtc);
                await Task.Delay(TimeSpan.FromMilliseconds(25), time, deadline.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, correlationId, generationId, dataset,
                "Supervisor recovery canary did not reach the projector before its deadline.", null);
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            var detail = exception.GetType().Name;
            return new(false, correlationId, generationId, dataset, detail, null);
        }
    }
}
