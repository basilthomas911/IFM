namespace TomasAI.IFM.Shared.EventSourcing;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3246", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
public interface IAsyncEventServiceHandler<TEvent, TService> where TEvent : IEvent where TService : IEventService
{
    Task ExecuteAsync(TEvent e, TService eventService);
}
