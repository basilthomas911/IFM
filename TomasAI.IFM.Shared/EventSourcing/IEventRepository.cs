
namespace TomasAI.IFM.Shared.EventSourcing;

public interface IEventRepository<TState> where TState : IBoundedContextState<TState>
{
    Task<IBoundedContext<TState>> LoadBoundedContextAsync(ICommand command);
    Task SaveBoundedContextAsync(IBoundedContextState<TState> boundedContextState, ICommand command);
}
