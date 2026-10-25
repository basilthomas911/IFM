using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.StatusConsole.ServiceApi;

public interface IStatusConsoleEventProducer : IActorProducer
{
    /// <summary>Retains a console message before acknowledging its publication.</summary>
    ValueTask PublishConsoleAsync(TomasAI.IFM.Shared.StatusConsole.Events.StatusConsoleLoggedEvent message);
}
