namespace TomasAI.IFM.Shared.EventSourcing;

public interface IEventProducer
{
    Task PostEventAsync(IEvent @event);
}
