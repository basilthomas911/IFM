namespace TomasAI.IFM.Shared.EventSourcing
{
    public interface IEventService
    {
        Task ExecuteAsync(IEvent @event);
        Task ExecuteAsync(IEvent @event, IEventService eventService);
    }
}
