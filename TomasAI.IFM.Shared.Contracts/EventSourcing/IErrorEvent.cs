using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

public interface IErrorEvent : IEvent
{
    DateTime ErrorDate { get; }
    int ErrorCode { get; init; }
    string ErrorMessage { get; init; }
    public ErrorType ErrorType { get; init; }
    public string ErrorData { get; init; }
    public string CommandName { get; init; }
    public string CommandData { get; init; }
}

public interface IErrorEvent<TEntityId> : IEvent<TEntityId>, IErrorEvent
    where TEntityId : IActorEntityId
{
}
