namespace TomasAI.IFM.Shared.EventSourcing;

public interface IAsyncEventHandler<TEvent> where TEvent : IEvent
{
    Task ExecuteAsync(TEvent e);
}

public interface IAsyncEventHandler<TEvent, TService> where TEvent : IEvent where TService:IEventService
{
    Task ExecuteAsync(TEvent e);
}

public interface IAsyncEventServiceHandler<TEvent, TService> where TEvent : IEvent where TService : IEventService
{
    Task ExecuteAsync(TEvent e, TService eventService);
}

public interface IEventHandler<TEvent> where TEvent : IEvent
{
    void Execute(TEvent e);
}
