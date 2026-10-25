using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Projects a candidate completed Function result synchronously with no queue, replay, or publication.</summary>
public interface IFunctionProjector<in TCompletedEvent>
    where TCompletedEvent : IEvent
{
    ValueTask ProjectAsync(TCompletedEvent completedEvent, CancellationToken cancellationToken = default);
}
