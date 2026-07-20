namespace TomasAI.IFM.Shared.EventSourcing;

public interface IBoundedContext
{
       void Execute(ICommand command);
}

public interface IBoundedContext<TBoundedContextState> 
    : IBoundedContext where TBoundedContextState : IBoundedContextState
{
    IBoundedContextState<TBoundedContextState> State { get; }
}
