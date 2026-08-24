using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels;

/// <summary>
/// base editor view model
/// </summary>
public class BaseEditorViewModel : ObservableObject
{
    readonly IAppRoot _appRoot;

    /// <summary>
    /// base editor view model constructor
    /// </summary>
    /// <param name="appRoot"></param>
    public BaseEditorViewModel(IAppRoot appRoot)
    {
        _appRoot = appRoot;
    }

    public IAppRoot AppRoot => _appRoot;
    public Action<int, string> OnError = null!;
    public Action<string> ShowStatus = null!;
    public Action ClearError = null!;
    public Action StartWaitIndicator = null!;
    public Action StopWaitIndicator = null!;
    public Action<CommandResponseEventService, string> ShowWaitView = null!;

    /// <summary>
    /// set event source for all consumer events
    /// </summary>
    /// <param name="eventSource"></param>
    /// <param name="events"></param>
    /// <returns></returns>
    protected void SetConsumerEvents(EventTopic eventTopic, ICollection<IEvent> @events)
    {
        var eventSource = $"{eventTopic}";
        if (!@events.Any(o => o is CommandExceptionEvent))
            @events.Add(new CommandExceptionEvent {}.SetEventSource(eventSource));
    }


    /// <summary>
    /// write status console
    /// </summary>
    public Task WriteStatusConsole(LogSourceType logSourceType, string statusMsg)
        => _appRoot.Services.StatusConsole.ExecuteAsync(async model => {
            model.OnError((errorCode, errorMsg) => this.OnError?.Invoke(errorCode, errorMsg));
            await model.WriteConsoleAsync(logSourceType, statusMsg);
         });


    /// <summary>
    /// write error status
    /// </summary>
    /// <param name="errorCode"></param>
    /// <param name="errorMsg"></param>
    public Task WriteStatusConsole(LogSourceType logSourceType,int errorCode, string errorMsg)
        => _appRoot.Services.StatusConsole.ExecuteAsync(async model =>
        {
            model.OnError((errCode, errMsg) => this.OnError?.Invoke(errCode, errMsg));
            await model.WriteConsoleAsync(logSourceType, errorCode, errorMsg);
            OnError?.Invoke(errorCode, errorMsg);
        });
}
