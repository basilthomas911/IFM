namespace TomasAI.IFM.Application.EventProjector.Contracts;

public interface IEventProjectorReadiness
{
    EventProjectorReadinessSnapshot GetSnapshot(string projectorName);
}
