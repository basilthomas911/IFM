namespace TomasAI.IFM.Shared.EventSourcing;

public interface IEventDenormalizer
{
    Task ExecuteAsync(DomainEventCollection domainEvents);
    Task DenormalizeEventsAsync(DomainEventCollection domainEvents);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2326", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
public interface IEventDenormalizer<TBoundedContextState> : IEventDenormalizer where TBoundedContextState : IBoundedContextState
{
}
