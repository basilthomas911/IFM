using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

public interface IQueryActorState<TState> : IActorState<TState>
    where TState : IActorState
{
}



    
