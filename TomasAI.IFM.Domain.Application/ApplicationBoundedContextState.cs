using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application;

public class ApplicationBoundedContextState : BaseBoundedContextState<ApplicationBoundedContextState>
{
    /// <summary>
    /// apply state change event in derived state object
    /// </summary>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    protected override bool Apply(IEvent domainEvent) => false;

}
