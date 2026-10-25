using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Shared.EventModelActor;
/// <summary>Retains the transport-neutral reply capability, never a provider message.</summary>
public readonly struct ActorMessageInfo
{
    public ActorMessageInfo(IActorMessage message, ICommand command) { Message=message; Command=command; }
    public ActorMessageInfo(IActorMessage message, IEvent value) { Message=message; Event=value; }
    public ActorMessageInfo(IActorMessage message, IQuery query) { Message=message; Query=query; }
    public IActorMessage Message { get; }
    public ICommand Command { get; }
    public IEvent Event { get; }
    public IQuery Query { get; }
}
