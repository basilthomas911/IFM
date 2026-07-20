namespace TomasAI.IFM.Shared.EventSourcing;

public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    void Execute(TCommand cmdParam);
}


