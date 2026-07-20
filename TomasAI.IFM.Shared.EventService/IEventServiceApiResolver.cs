namespace TomasAI.IFM.Shared.EventService;

public interface IEventServiceApiResolver
{
    TApi? ResolveApi<TApi>() where TApi : class;
}
