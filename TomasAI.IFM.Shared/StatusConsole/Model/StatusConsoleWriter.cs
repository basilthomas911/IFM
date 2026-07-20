using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.Events;

namespace TomasAI.IFM.Shared.StatusConsole.Model;

/// <summary>
/// status console writer
/// </summary>
/// <remarks>
/// status console writer constructor
/// </remarks>
/// <param name="statusConsole"></param>
public class StatusConsoleWriter(IStatusConsoleEventProducer statusConsole) 
    : IStatusConsoleWriter
{

    /// <summary>
    /// post event to status console using event producer
    /// </summary>
    IStatusConsoleEventProducer StatusConsole { get; } = IsArgumentNull.Set(statusConsole);

    /// <summary>
    /// write status to status console log
    /// </summary>
    /// <param name="errorCode"></param>
    /// <param name="errorMsg"></param>
    /// <returns></returns>
    public async Task WriteConsoleAsync(LogSourceType logSourceType, string statusMsg)
    {
        var logEvent = new StatusConsoleLoggedEvent
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Event, StatusConsoleLoggedEvent.Actor, StatusConsoleLoggedEvent.Verb, logSourceType.ToStringFast()),
            EntityId = new ActorEntityId(logSourceType.ToStringFast()),
            StatusConsoleLog = new StatusConsoleLogReadModel
            (
                StatusDate: DateTime.Now,
                StatusCode: 0,
                Source: logSourceType,
                Message: statusMsg
            )
        };
        if (!StatusConsole.IsRunning)
            await StatusConsole.StartAsync(logEvent.Subject.ActorId);
        await StatusConsole.SendAsync<StatusConsoleLoggedEvent, ActorEntityId>(logEvent.Subject, logEvent);
    }

    /// <summary>
    /// write status to status console log
    /// </summary>
    /// <param name="errorCode"></param>
    /// <param name="errorMsg"></param>
    /// <returns></returns>
    public async Task WriteConsoleAsync(LogSourceType logSourceType, int errorCode, string errorMsg, string dataType="", string data="")
    {
        var logEvent = new StatusConsoleLoggedEvent
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Event, StatusConsoleLoggedEvent.Actor, StatusConsoleLoggedEvent.Verb, logSourceType.ToStringFast()),
            EntityId = new ActorEntityId(logSourceType.ToStringFast()),
            StatusConsoleLog = new StatusConsoleLogReadModel
            (
                StatusDate: DateTime.Now,
                StatusCode: errorCode,
                Source: logSourceType,
                Message: $"{errorCode}: {errorMsg}",
                DataType: dataType,
                Data: data
            )
        };
        if (!StatusConsole.IsRunning)
            await StatusConsole.StartAsync(logEvent.Subject.ActorId);
        await StatusConsole.SendAsync<StatusConsoleLoggedEvent, ActorEntityId>(logEvent.Subject, logEvent);
    }
}
