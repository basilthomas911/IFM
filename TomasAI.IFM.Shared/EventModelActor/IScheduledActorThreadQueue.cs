using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Internal scheduling and lifetime contract implemented by the V2 mailbox queue.
/// The public queue contract remains available for compatibility with the legacy scheduler.
/// </summary>
internal interface IScheduledActorThreadQueue
{
    bool IsRetired { get; }
    bool TryWrite(IActorMessage message, CancellationToken cancellationToken);
    ValueTask<bool> TryWriteAsync(IActorMessage message, CancellationToken cancellationToken);
    bool TryRead(out IActorMessage? message);
    bool TrySchedule();
    bool CompleteDrain();
    bool TryRetire();
}
