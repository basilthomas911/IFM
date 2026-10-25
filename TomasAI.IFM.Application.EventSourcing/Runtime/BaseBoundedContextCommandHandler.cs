using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventSourcing;

public abstract class BaseBoundedContextCommandHandler<TState> where TState : class, IBoundedContextState
{
    protected TState? State { get; private set; }
    public void SetState(TState state) => State = state;
}
