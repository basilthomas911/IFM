using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventService;

/// <summary>
/// base event service handler constructor
/// </summary>
/// <param name="statusConsole"></param>
public abstract class BaseEventServiceHandler(IStatusConsoleWriter statusConsoleWriter)
{
    IStatusConsoleWriter StatusConsole { get; } = statusConsoleWriter;

    /// <summary>
    /// write status message to status console log
    /// </summary>
    /// <param name="logSourceType"></param>
    /// <param name="statusMsg"></param>
    /// <returns></returns>
    protected async Task WriteConsoleAsync(LogSourceType logSourceType, string statusMsg) 
        => await StatusConsole.WriteConsoleAsync(logSourceType, statusMsg);

    /// <summary>
    /// write error message to status console log
    /// </summary>
    /// <param name="logSourceType"></param>
    /// <param name="errorCode"></param>
    /// <param name="errorMsg"></param>
    /// <returns></returns>
    protected async Task WriteConsoleAsync(LogSourceType logSourceType, int errorCode, string errorMsg)
        => await StatusConsole.WriteConsoleAsync(logSourceType, errorCode, errorMsg);

    /// <summary>
    /// write error message to status console log
    /// </summary>
    /// <param name="logSourceType"></param>
    /// <param name="errorCode"></param>
    /// <param name="errorMsg"></param>
    /// <returns></returns>
    protected async Task WriteConsoleAsync(LogSourceType logSourceType, Exception ex)
        => await StatusConsole.WriteConsoleAsync(logSourceType, ex.GetErrorMessage());

}
