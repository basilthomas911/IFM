namespace TomasAI.IFM.Shared.EventSourcing;

public interface IBoundedContextCommandHandler< TCommand, TState>  where TCommand : ICommand where TState : IBoundedContextState
{
    bool Execute( TCommand command, TState state);

    ValueTask<bool> ExecuteAsync(TCommand command, TState state)
        => ValueTask.FromResult(Execute(command, state));
}
