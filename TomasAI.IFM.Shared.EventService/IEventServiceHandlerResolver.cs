namespace TomasAI.IFM.Shared.EventService;

public interface IEventServiceHandlerResolver
{
    object? ResolveEventHandler(Type eventType, Type eventServiceType);
    object? ResolveEventServiceHandler(Type eventType, Type eventServiceType);
}
