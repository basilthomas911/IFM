namespace TomasAI.IFM.Shared.EventSourcing;

public abstract class BaseBoundedContextCommandHandler<TState>  where TState : class,IBoundedContextState 
{
    protected TState? State { get; private set; }
    public void SetState(TState state) => State = state;
}
