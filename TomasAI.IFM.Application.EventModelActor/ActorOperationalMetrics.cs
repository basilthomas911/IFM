using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.EventModelActor;

static class ActorOperationalMetrics
{
    internal static ActorFailureStage MapStage(string stage) => stage switch
    {
        ActorRuntimeMetrics.ParsingStage => ActorFailureStage.Parsing,
        ActorRuntimeMetrics.ValidationStage => ActorFailureStage.Validation,
        ActorRuntimeMetrics.DeduplicationStage => ActorFailureStage.Deduplication,
        ActorRuntimeMetrics.ReplayStage => ActorFailureStage.StateReplay,
        ActorRuntimeMetrics.ExecutionStage => ActorFailureStage.Execution,
        ActorRuntimeMetrics.PersistenceStage => ActorFailureStage.Persistence,
        ActorRuntimeMetrics.PublicationStage => ActorFailureStage.Publication,
        ActorRuntimeMetrics.ReplyStage => ActorFailureStage.Reply,
        _ => ActorFailureStage.Unknown
    };

    internal static void RecordHandledFailure(
        IActorMailbox? mailbox,
        ActorThreadId threadId,
        Exception exception)
    {
        if (mailbox?.Metrics is not ActorMetricsStore store
            || !mailbox.ThreadQueues.TryGetThreadQueue(threadId, out var queue)
            || queue is null)
            return;

        store.GetOrRegister(threadId, queue).RecordHandledFailure(exception);
    }

    internal static Guid RecordHandledFailure(
        IActorMailbox? mailbox,
        ISupervisorRuntimeContext? runtime,
        ActorThreadId threadId,
        string verb,
        ActorFailureStage stage,
        Exception exception,
        Guid? primaryFailureId = null)
    {
        RecordHandledFailure(mailbox, threadId, exception);
        return runtime?.RecordFailure(
            threadId.MailboxId, threadId, verb, stage, exception, primaryFailureId,
            ActorMessageOutcomeType.HandledFailure) ?? Guid.Empty;
    }
}
