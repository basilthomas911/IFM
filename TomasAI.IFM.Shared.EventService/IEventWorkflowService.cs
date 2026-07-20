using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventService;

public interface IEventWorkflowService
{
    Task<bool> HandleAsync(IEvent @event);
}
