using System.Runtime.ExceptionServices;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Preserves the first lifecycle failure while containing and linking cleanup failures.</summary>
static class ActorLifecycleGuard
{
    internal static async ValueTask StopAsync(
        SupervisorRuntimeContext? runtime,
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
                primaryId = failureId;
                SupervisorRuntimeContext.MarkRecorded(exception, failureId);
            }
        }

        if (primary is not null)
            ExceptionDispatchInfo.Capture(primary).Throw();
    }
}
