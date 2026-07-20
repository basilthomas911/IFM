using TomasAI.IFM.Contracts;
using TomasAI.IFM.Models;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole;

namespace TomasAI.IFM.ViewModels;

/// <summary>
/// base editor view model
/// </summary>
public class BaseEditorViewModel
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
    public Action<EventModel, string> ShowWaitView = null!;

    protected async Task ExecuteCommandAsync(
        Func<Task<ServiceResult<Guid>>> commandFunc,
        Action<Guid> onSuccess,
        Action<ServiceResult<Guid>> onFailure)
    {
        var serviceResult = await commandFunc();
        if (serviceResult.Success)
            onSuccess(serviceResult.Value);
        else
            onFailure(serviceResult);
    }

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
    public void WriteStatusConsole(LogSourceType logSourceType, string statusMsg)
        => _appRoot.GetModel<StatusConsoleModel>().Execute(async model => {
            model.OnError((errorCode, errorMsg) => this.OnError?.Invoke(errorCode, errorMsg));
            await model.WriteConsoleAsync(logSourceType, statusMsg);
         });
    

    /// <summary>
    /// write error status
    /// </summary>
    /// <param name="errorCode"></param>
    /// <param name="errorMsg"></param>
    public void WriteStatusConsole(LogSourceType logSourceType,int errorCode, string errorMsg)
        => _appRoot.GetModel<StatusConsoleModel>().Execute(async model =>
        {
            model.OnError((errCode, errMsg) => this.OnError?.Invoke(errCode, errMsg));
            await model.WriteConsoleAsync(logSourceType, errorCode, errorMsg);
            OnError?.Invoke(errorCode, errorMsg);
        });
}
