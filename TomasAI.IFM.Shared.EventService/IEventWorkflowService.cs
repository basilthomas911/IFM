using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventService;

public interface IEventWorkflowService
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing published actor member name.")]
    Task<bool> HandleAsync(IEvent @event);
}
