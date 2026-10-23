using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Service.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.Events;

namespace TomasAI.IFM.UI.Net.Services.Application;

/// <summary>Provides the StatusConsoleService UI service boundary.</summary>
public class StatusConsoleService : UiServiceBase<StatusConsoleService>
{
    readonly IStatusConsoleWriter _statusConsole;
    readonly IStatusConsoleEventConsumer _statusConsoleEventConsumer;

    /// <summary>
    /// create staus console controller
    /// </summary>
    /// <param name="statusConsole"></param>
    /// <param name="statusConsoleEventConsumer"></param>
    public StatusConsoleService(IStatusConsoleWriter statusConsole, IStatusConsoleEventConsumer statusConsoleEventConsumer)
    {
        _statusConsole = statusConsole ?? throw new ArgumentNullException(nameof(statusConsole));
        _statusConsoleEventConsumer = statusConsoleEventConsumer ?? throw new ArgumentNullException(nameof(statusConsoleEventConsumer));
    }

    /// <summary>
    /// start listening for status console log updates
    /// </summary>
    /// <param name="listenerAction"></param>
    /// <param name="siteId"></param>
    public async Task StartStatusConsoleLogListenerAsync(
        Func<StatusConsoleLoggedEvent, ValueTask> listenerAction,
        Guid siteId)
    {
        await _statusConsoleEventConsumer.StartAsync(listenerAction);
    }

    /// <summary>
    /// stop listening for status console log updates
    /// </summary>
    /// <param name="siteId"></param>
    public async Task StopStatusConsoleLogListener(Guid siteId)
    {
        await _statusConsoleEventConsumer.StopAsync();
    }

    /// <summary>
    /// write status to status console log
    /// </summary>
    /// <param name="statusSourceType"></param>
    /// <param name="statusMsg"></param>
    public async Task WriteConsoleAsync(LogSourceType statusSourceType, string statusMsg)
    {
        await _statusConsole.WriteConsoleAsync(statusSourceType, statusMsg);
    }

    /// <summary>
    /// write status to status console log
    /// </summary>
    /// <param name="statusSourceType"></param>
    /// <param name="errorCode"></param>
    /// <param name="errorMsg"></param>
    public async Task WriteConsoleAsync(LogSourceType statusSourceType, int errorCode, string errorMsg)
    {
        await _statusConsole.WriteConsoleAsync(statusSourceType, errorCode, errorMsg);
    }
}
