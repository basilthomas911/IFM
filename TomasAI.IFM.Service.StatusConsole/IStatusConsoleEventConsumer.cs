using TomasAI.IFM.Shared.StatusConsole.Events;

namespace TomasAI.IFM.Service.StatusConsole;

public interface IStatusConsoleEventConsumer
{
    ValueTask StartAsync(Action<StatusConsoleLoggedEvent> eventAction);
    ValueTask StopAsync();
}
