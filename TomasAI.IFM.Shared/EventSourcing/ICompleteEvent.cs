using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

public interface ICompleteEvent : IEvent { }
public interface ICompleteEvent<TEntityId> : IEvent<TEntityId>, ICompleteEvent
    where TEntityId : IActorEntityId
{
}
