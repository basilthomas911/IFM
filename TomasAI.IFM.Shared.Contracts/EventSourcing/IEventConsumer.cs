namespace TomasAI.IFM.Shared.EventSourcing;

public interface IEventConsumer
{
    ValueTask StartAsync();
    ValueTask StopAsync();
}
