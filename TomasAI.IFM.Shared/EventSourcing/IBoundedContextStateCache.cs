
namespace TomasAI.IFM.Shared.EventSourcing;

public interface IBoundedContextStateCache
{
    bool Exists<TState>(long key) where TState : class, IBoundedContextState;
    void Add<TState>(long key, TState value) where TState : class, IBoundedContextState;
    void Remove<TState>(long key) where TState : class, IBoundedContextState;
    TState Get<TState>(long key) where TState : class, IBoundedContextState;
}
