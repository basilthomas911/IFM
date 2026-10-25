using System.Runtime.ExceptionServices;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

/// <summary>Preserves the first lifecycle failure while containing and linking cleanup failures.</summary>
static class ActorLifecycleGuard
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Shutdown/recovery must isolate arbitrary dependency failures so remaining actors and resources are still stopped.")]
    internal static async ValueTask StopAsync(
        ISupervisorRuntimeContext? runtime,
        ActorMailboxId actorId,
        Func<ValueTask> stopTransport,
        Func<ValueTask> shutdownActor,
        CancellationToken cancellationToken)
    {
        var threadId = new ActorThreadId(actorId.ActorType, actorId.Name, "lifecycle");
        Exception? primary = null;
        Guid primaryId = Guid.Empty;
        try
        {
            await stopTransport().ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            primary = exception;
        }
        catch (Exception exception)
        {
            primary = exception;
            primaryId = runtime?.RecordFailure(
                actorId, threadId, "Stop", ActorFailureStage.Shutdown, exception) ?? Guid.Empty;
            SupervisorRuntimeContext.MarkRecorded(exception, primaryId);
        }

        try
        {
            await shutdownActor().ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            primary ??= exception;
        }
        catch (Exception exception)
        {
            var failureId = runtime?.RecordFailure(
                actorId, threadId, "Stop", ActorFailureStage.Cleanup, exception,
                primaryId == Guid.Empty ? null : primaryId,
                primary is null
                    ? ActorMessageOutcomeType.EscapedFailure
                    : ActorMessageOutcomeType.HandledFailure) ?? Guid.Empty;
            if (primary is null)
            {
                primary = exception;
                SupervisorRuntimeContext.MarkRecorded(exception, failureId);
            }
        }

        if (primary is not null)
            ExceptionDispatchInfo.Capture(primary).Throw();
    }
}
