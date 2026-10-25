namespace TomasAI.IFM.Shared.EventSourcing;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3246", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711", Justification = "The established public actor queue/handler contract name describes its role and must remain compatible.")]
public interface IAsyncEventHandler<TEvent> where TEvent : IEvent
{
    Task ExecuteAsync(TEvent e);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2326", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3246", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711", Justification = "The established public actor queue/handler contract name describes its role and must remain compatible.")]
public interface IAsyncEventHandler<TEvent, TService> where TEvent : IEvent where TService : IEventService
{
    Task ExecuteAsync(TEvent e);
}
